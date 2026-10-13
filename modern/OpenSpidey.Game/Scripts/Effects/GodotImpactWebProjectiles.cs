using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Effects;

/// <summary>Original moving CImpactWeb flat bits, using ImpactWb frame0 and the original source projection.</summary>
public partial class GodotImpactWebProjectiles : Node3D
{
    private readonly List<ImpactWebProjectile> shots = [];
    private readonly ImmediateMesh mesh = new();
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private IImpactWebHost host = null!;
    private Func<int, int> random = null!;
    private ImageTexture texture = null!;
    private StandardMaterial3D material = null!;
    private SpriteAnimationFrame frame;
    private bool disposed;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    public int Count => shots.Count;
    public ushort FarDepth { get; set; } = 8048;
    internal IReadOnlyList<ImpactWebProjectile> Shots => shots;
    internal ImmediateMesh RenderMesh => mesh;
    internal SpriteAnimationFrame SourceFrame => frame;
    internal ImageTexture SourceTexture => texture;
    internal StandardMaterial3D RenderMaterial => material;

    public GodotImpactWebProjectiles() => TopLevel = true;

    public static GodotImpactWebProjectiles Create(byte[] bitsBytes, IImpactWebHost host, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(random);
        SpriteAnimation animation = SpriteAnimationReader.Read(bitsBytes).Single(animation =>
            animation.Name.Equals("ImpactWb", StringComparison.OrdinalIgnoreCase));
        if (animation.Frames.Count == 0) throw new InvalidDataException("Original impact-web projectile requires ImpactWb frame0.");
        SpriteAnimationFrame frame = animation.Frames[0];
        DecodedTexture decoded = PsxTextureReader.Read(bitsBytes).First(texture => texture.Checksum == frame.TextureChecksum);
        using var image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.RgbaPixels);
        var effect = new GodotImpactWebProjectiles
        {
            Name = "OriginalImpactWebProjectiles", host = host, random = random,
            frame = frame, texture = ImageTexture.CreateFromImage(image)
        };
        effect.material = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
            VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
            AlbedoColor = Colors.White, AlbedoTexture = effect.texture,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear, TextureRepeat = false
        };
        return effect;
    }

    public override void _Ready()
    {
        AddChild(new MeshInstance3D
        {
            Name = "OriginalImpactWebFlightSprites", Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        Rebuild();
    }

    public ImpactWebProjectile Emit(PlayerImpactWebRequest request, uint clock)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var projectile = new ImpactWebProjectile(request.Position, request.Direction, request.Speed,
            request.Damage, request.Lifetime, clock, host, random);
        shots.Insert(0, projectile);
        SourceBitCreated?.Invoke(new(projectile, tick => projectile.Advance(tick, host, random),
            () => !disposed && shots.Contains(projectile) && projectile.Alive,
            () => { shots.Remove(projectile); Rebuild(); }));
        Rebuild();
        return projectile;
    }

    /// <summary>Move each already-present source flat bit once; render frames never advance its simulation.</summary>
    public void AdvanceBits(uint clock)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (ImpactWebProjectile projectile in shots.ToArray()) projectile.Advance(clock, host, random);
        shots.RemoveAll(projectile => !projectile.Alive);
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
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        Vector2 pixelScale = new(viewport.Y / 240 * .625f, viewport.Y / 240);
        float span = viewport.X / pixelScale.X;
        int zoom = camera is PlayerFollowCamera follow ? follow.ScriptRig.Zoom.Value :
            (int)Math.Round(Math.Tan(Mathf.DegToRad(camera.Fov) / 2.0) * 4096 * CameraProjection.OriginalAspect);
        int halfProjection = (((512 << 11) / zoom << 12) / (512 * 0x3000 / (240 << 2))) >> 1;
        foreach (ImpactWebProjectile shot in shots)
        {
            if (!shot.Alive || shot.Scale == 0) continue;
            Vector3 center = PsxCoordinates.FixedWorldPosition(shot.Position);
            float worldDepth = -camera.GlobalBasis.Z.Dot(center - camera.GlobalPosition);
            int depth = SourceDepth(camera, shot.Position);
            if (worldDepth <= 0 || depth < halfProjection || depth > FarDepth) continue;
            Vector2 logical = camera.UnprojectPosition(center) / pixelScale;
            if (logical.X < -span / 2 || logical.X > span * 1.5f || logical.Y < -256 || logical.Y > 360) continue;
            ThugPingPixel[]? corners = ThugPingProjection.Project(frame, unchecked((ushort)shot.Scale),
                shot.Angle, depth, (int)logical.X, (int)logical.Y, shot.PostScale);
            if (corners is null) continue;
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
            mesh.SurfaceSetColor(GodotGlowFlash.SourceColour(0x2C808080, 0, brightness));
            foreach (int index in new[] { 0, 1, 2, 1, 3, 2 })
            {
                Vector2 pixel = (new Vector2(corners[index].X, corners[index].Y) + logical -
                    new Vector2((int)logical.X, (int)logical.Y)) * pixelScale;
                mesh.SurfaceSetUV(new((index & 1) != 0 ? .99f : .01f, (index & 2) != 0 ? .99f : .01f));
                mesh.SurfaceAddVertex(camera.ProjectPosition(pixel, worldDepth));
            }
            mesh.SurfaceEnd();
        }
    }

    private static int SourceDepth(Camera3D camera, FixedVector3 position)
    {
        FixedJointTransform rotation = PsxCoordinates.OriginalRotation(camera.GlobalBasis);
        return FixedRotation.Transform(FixedRotation.TransposeRotation(rotation),
            (position >> 12) - (PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12)).Z;
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        if (disposed) return;
        disposed = true;
        shots.Clear(); mesh.Dispose(); material?.Dispose(); texture?.Dispose();
    }
}
