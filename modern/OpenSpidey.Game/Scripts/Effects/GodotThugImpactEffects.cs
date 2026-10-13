using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Effects;

/// <summary>Original HitPing frame1 for world ricochet planes and animated-hook flat pings.</summary>
public partial class GodotThugImpactEffects : Node3D
{
    private readonly List<ThugLaserPingState> laser = [];
    private readonly List<ThugAttachedPingState> attached = [];
    private readonly ImmediateMesh mesh = new();
    private ImageTexture? texture;
    private StandardMaterial3D? material;
    private SpriteAnimationFrame frame;
    private bool disposed;
    public int LaserCount => laser.Count;
    public int AttachedCount => attached.Count;
    public event Action<SourceParticleList, SourceParticleRegistration>? SourceBitCreated;
    public event Action<object>? SourceBitRemoved;
    public ushort FarDepth { get; set; } = 8048;
    internal ImmediateMesh RenderMesh => mesh;
    internal ImageTexture SourceTexture => texture!;
    internal SpriteAnimationFrame SourceFrame => frame;
    public GodotThugImpactEffects() => TopLevel = true;

    public static GodotThugImpactEffects Create(byte[] bitsBytes)
    {
        SpriteAnimation animation = SpriteAnimationReader.Read(bitsBytes).Single(animation => animation.Name.Equals("HitPing", StringComparison.OrdinalIgnoreCase));
        if (animation.Frames.Count <= 1) throw new InvalidDataException("Original HitPing animation has no required frame1.");
        SpriteAnimationFrame frame = animation.Frames[1];
        DecodedTexture decoded = PsxTextureReader.Read(bitsBytes).First(texture => texture.Checksum == frame.TextureChecksum);
        using var image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.RgbaPixels);
        var effect = new GodotThugImpactEffects { frame = frame, texture = ImageTexture.CreateFromImage(image) };
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
        AddChild(new MeshInstance3D { Name = "OriginalHitPingQuads", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        Rebuild();
    }
    public void EmitLaserPing(ThugLaserPingState ping)
    {
        CheckAlive();
        laser.Insert(0, ping);
        SourceBitCreated?.Invoke(SourceParticleList.Quad, new(ping, _ => ping.Advance(),
            () => !disposed && laser.Contains(ping) && ping.Alive, () => Remove(ping)));
        Rebuild();
    }
    public void EmitAttachedPing(ThugAttachedPingState ping)
    {
        CheckAlive();
        attached.Insert(0, ping);
        SourceBitCreated?.Invoke(SourceParticleList.Flat, new(ping, _ => ping.Advance(),
            () => !disposed && attached.Contains(ping) && ping.Alive, () => Remove(ping)));
        Rebuild();
    }
    public void Advance()
    {
        CheckAlive();
        foreach (ThugLaserPingState ping in laser) ping.Advance();
        foreach (ThugAttachedPingState ping in attached) ping.Advance();
        foreach (ThugLaserPingState ping in laser.Where(ping => !ping.Alive).ToArray()) Remove(ping);
        foreach (ThugAttachedPingState ping in attached.Where(ping => !ping.Alive).ToArray()) Remove(ping);
        Rebuild();
    }
    private void Remove(ThugLaserPingState ping)
    {
        if (!laser.Remove(ping)) return;
        SourceBitRemoved?.Invoke(ping);
        Rebuild();
    }
    private void Remove(ThugAttachedPingState ping)
    {
        if (!attached.Remove(ping)) return;
        SourceBitRemoved?.Invoke(ping);
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
        foreach (ThugLaserPingState ping in laser)
        {
            if (!ping.Alive || ping.Spread == 0) continue;
            FixedVector3[] source = [ping.Position, ping.PositionB, ping.PositionC, ping.PositionD];
            var vertices = new Vector3[4]; bool visible = true;
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = PsxCoordinates.FixedWorldPosition(source[i]);
                float depth = -camera.GlobalBasis.Z.Dot(corner - camera.GlobalPosition);
                if (depth <= 0 || SourceDepth(camera, source[i]) < 100) { visible = false; break; }
                // Original DisplayQuadBitList multiplies reciprocal depth by1.03 while preserving its screen corner.
                vertices[i] = camera.ProjectPosition(camera.UnprojectPosition(corner), depth / 1.03f);
            }
            if (visible) Quad(vertices, Brightness(ping.Tint), true);
        }
        foreach (ThugAttachedPingState ping in attached)
        {
            if (!ping.Alive) continue;
            Vector3 center = PsxCoordinates.FixedWorldPosition(ping.Position);
            float worldDepth = -camera.GlobalBasis.Z.Dot(center - camera.GlobalPosition);
            int depth = SourceDepth(camera, ping.Position);
            int zoom = camera is PlayerFollowCamera follow ? follow.ScriptRig.Zoom.Value :
                (int)Math.Round(Math.Tan(Mathf.DegToRad(camera.Fov) / 2.0) * 4096 * CameraProjection.OriginalAspect);
            int halfProjection = (((512 << 11) / zoom << 12) / (512 * 0x3000 / (240 << 2))) >> 1;
            if (worldDepth <= 0 || depth < halfProjection || depth > FarDepth) continue;
            Vector2 logical = camera.UnprojectPosition(center) / pixelScale;
            float span = viewport.X / pixelScale.X;
            if (logical.X < -span / 2 || logical.X > span * 1.5f || logical.Y < -256 || logical.Y > 360) continue;
            ThugPingPixel[]? corners = ThugPingProjection.Project(frame, (ushort)ping.Scale, ping.Angle, depth, (int)logical.X, (int)logical.Y);
            if (corners is null) continue;
            var vertices = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 pixel = (new Vector2(corners[i].X, corners[i].Y) + logical - new Vector2((int)logical.X, (int)logical.Y)) * pixelScale;
                vertices[i] = camera.ProjectPosition(pixel, worldDepth);
            }
            Quad(vertices, Brightness(ping.CodeRgb), false);
        }
    }

    private void Quad(Vector3[] vertices, Color color, bool doublePass)
    {
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
        mesh.SurfaceSetColor(color);
        int[] order = doublePass ? [0, 1, 2, 1, 3, 2, 0, 2, 1, 2, 3, 1] : [0, 1, 2, 1, 3, 2];
        foreach (int index in order)
        {
            mesh.SurfaceSetUV(new((index & 1) != 0 ? 0.99f : 0.01f, (index & 2) != 0 ? 0.99f : 0.01f));
            mesh.SurfaceAddVertex(vertices[index]);
        }
        mesh.SurfaceEnd();
    }
    private static Color Brightness(uint code) => new(Channel((byte)code), Channel((byte)(code >> 8)), Channel((byte)(code >> 16)));
    private static float Channel(byte channel) => (channel >= 128 ? 255 : channel * 2) / 255f;
    private static int SourceDepth(Camera3D camera, FixedVector3 position)
    {
        FixedJointTransform rotation = PsxCoordinates.OriginalRotation(camera.GlobalBasis);
        var view = new FixedJointTransform(rotation.M00, rotation.M10, rotation.M20, rotation.M01, rotation.M11, rotation.M21,
            rotation.M02, rotation.M12, rotation.M22, 0, 0, 0);
        return FixedRotation.Transform(view, (position >> 12) - (PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12)).Z;
    }
    private void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(GodotThugImpactEffects)); }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        foreach (ThugLaserPingState ping in laser.ToArray()) SourceBitRemoved?.Invoke(ping);
        foreach (ThugAttachedPingState ping in attached.ToArray()) SourceBitRemoved?.Invoke(ping);
        laser.Clear(); attached.Clear(); mesh.Dispose(); material?.Dispose(); texture?.Dispose();
    }
}
