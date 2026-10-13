using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Effects;

/// <summary>Original HITSPRIT frame0 combat rings, source clock growth and proportional viewport projection.</summary>
public partial class GodotThugCombatRings : Node3D
{
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private readonly List<ThugCombatImpactRing> rings = [];
    private readonly ImmediateMesh mesh = new();
    private ImageTexture? texture;
    private StandardMaterial3D? material;
    private SpriteAnimationFrame frame;
    private bool disposed;
    public int Count => rings.Count;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    public ushort FarDepth { get; set; } = 8048;
    internal ImmediateMesh RenderMesh => mesh;
    internal IReadOnlyList<ThugCombatImpactRing> Rings => rings;
    internal ImageTexture SourceTexture => texture!;
    internal SpriteAnimationFrame SourceFrame => frame;
    public GodotThugCombatRings() => TopLevel = true;

    public static GodotThugCombatRings Create(byte[] bitsBytes)
    {
        SpriteAnimation animation = SpriteAnimationReader.Read(bitsBytes).Single(animation => animation.Name.Equals("HITSPRIT", StringComparison.OrdinalIgnoreCase));
        if (animation.Frames.Count != 1) throw new InvalidDataException("Original HITSPRIT requires its single source frame.");
        SpriteAnimationFrame frame = animation.Frames[0];
        DecodedTexture decoded = PsxTextureReader.Read(bitsBytes).First(texture => texture.Checksum == frame.TextureChecksum);
        using var image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.RgbaPixels);
        var effect = new GodotThugCombatRings { frame = frame, texture = ImageTexture.CreateFromImage(image) };
        effect.material = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            AlbedoColor = new(1, 1, 1, 128 / 255f),
            AlbedoTexture = effect.texture,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
            TextureRepeat = false
        };
        return effect;
    }

    public override void _Ready()
    {
        AddChild(new MeshInstance3D { Name = "OriginalCombatImpactRings", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        Rebuild();
    }
    public void Emit(int actorType, FixedVector3 position, uint clock, Func<int, int> random)
    {
        CheckAlive();
        foreach (ThugCombatImpactRing ring in ThugCombatImpactRing.Create(actorType, position, clock, random)) Attach(ring);
        Rebuild();
    }
    public void Emit(ThugCombatImpactRing ring)
    {
        CheckAlive();
        ArgumentNullException.ThrowIfNull(ring);
        Attach(ring);
        Rebuild();
    }
    private void Attach(ThugCombatImpactRing ring)
    {
        rings.Insert(0, ring);
        SourceBitCreated?.Invoke(new(ring, ring.Advance,
            () => !disposed && rings.Contains(ring) && ring.Alive,
            () => { rings.Remove(ring); Rebuild(); }));
    }
    public void Advance(uint clock)
    {
        CheckAlive();
        foreach (ThugCombatImpactRing ring in rings) ring.Advance(clock);
        rings.RemoveAll(ring => !ring.Alive);
        Rebuild();
    }
    public override void _Process(double delta) => Rebuild();

    public void Rebuild()
    {
        if (disposed || !IsInsideTree()) return;
        mesh.ClearSurfaces();
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        Vector2 pixelScale = new(viewport.Y / 240 * 0.625f, viewport.Y / 240);
        float span = viewport.X / pixelScale.X;
        int zoom = camera is PlayerFollowCamera follow ? follow.ScriptRig.Zoom.Value :
            (int)Math.Round(Math.Tan(Mathf.DegToRad(camera.Fov) / 2.0) * 4096 * CameraProjection.OriginalAspect);
        int halfProjection = (((512 << 11) / zoom << 12) / (512 * 0x3000 / (240 << 2))) >> 1;
        foreach (ThugCombatImpactRing ring in rings)
        {
            Vector3 center = PsxCoordinates.FixedWorldPosition(ring.Position);
            float worldDepth = -camera.GlobalBasis.Z.Dot(center - camera.GlobalPosition);
            int depth = SourceDepth(camera, ring.Position);
            if (worldDepth <= 0 || depth < halfProjection || depth > FarDepth) continue;
            Vector2 logical = camera.UnprojectPosition(center) / pixelScale;
            if (logical.X < -span / 2 || logical.X > span * 1.5f || logical.Y < -256 || logical.Y > 360) continue;
            ThugPingPixel[]? corners = ThugPingProjection.Project(frame, unchecked((ushort)ring.Scale), ring.Angle, depth, (int)logical.X, (int)logical.Y, ring.PostScale);
            if (corners is null) continue;
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
            mesh.SurfaceSetColor(GodotGlowFlash.SourceColour(ring.CodeRgb, 0, brightness));
            foreach (int index in new[] { 0, 1, 2, 1, 3, 2 })
            {
                Vector2 pixel = (new Vector2(corners[index].X, corners[index].Y) + logical - new Vector2((int)logical.X, (int)logical.Y)) * pixelScale;
                mesh.SurfaceSetUV(new((index & 1) != 0 ? 0.99f : 0.01f, (index & 2) != 0 ? 0.99f : 0.01f));
                mesh.SurfaceAddVertex(camera.ProjectPosition(pixel, worldDepth));
            }
            mesh.SurfaceEnd();
        }
    }
    private static int SourceDepth(Camera3D camera, FixedVector3 position)
    {
        FixedJointTransform rotation = PsxCoordinates.OriginalRotation(camera.GlobalBasis);
        var view = new FixedJointTransform(rotation.M00, rotation.M10, rotation.M20, rotation.M01, rotation.M11, rotation.M21,
            rotation.M02, rotation.M12, rotation.M22, 0, 0, 0);
        return FixedRotation.Transform(view, (position >> 12) - (PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12)).Z;
    }
    private void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(GodotThugCombatRings)); }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true; rings.Clear(); mesh.Dispose(); material?.Dispose(); texture?.Dispose();
    }
}
