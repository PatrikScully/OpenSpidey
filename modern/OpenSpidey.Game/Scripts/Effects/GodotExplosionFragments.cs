using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Effects;

/// <summary>Actual Exp_Frag animation6 particles, source tint/additive projection and retained frame0 texture.</summary>
public partial class GodotExplosionFragments : Node3D
{
    private readonly List<ExplosionFragment> fragments = [];
    private readonly ImmediateMesh mesh = new();
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private SpriteAnimation animation = null!;
    private ImageTexture texture = null!;
    private StandardMaterial3D material = null!;
    private bool disposed;
    public int Count => fragments.Count;
    public ushort FarDepth { get; set; } = 8048;
    public IReadOnlyList<ExplosionFragment> SourceFragmentOrder => fragments.AsReadOnly();
    internal SpriteAnimation SourceAnimation => animation;
    internal ImageTexture SourceTexture => texture;
    internal StandardMaterial3D SourceMaterial => material;
    internal ImmediateMesh RenderMesh => mesh;
    public event Action<ExplosionFragment>? FragmentCreated;
    public event Action<ExplosionFragment>? FragmentRemoved;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    public event Action<object>? SourceBitRemoved;

    public GodotExplosionFragments() => TopLevel = true;

    public static GodotExplosionFragments Create(byte[] bitsBytes)
    {
        SpriteAnimation animation = SpriteAnimationReader.Read(bitsBytes).Single(animation =>
            animation.Name.Equals("WebSplat", StringComparison.OrdinalIgnoreCase));
        if (animation.Frames.Count > 255 || animation.Frames[0].Width == 0 || animation.Frames[0].Height == 0)
            throw new InvalidDataException("Original CFrag requires a byte-sized WebSplat animation with its real first rectangle.");
        DecodedTexture decoded = PsxTextureReader.Read(bitsBytes).First(texture =>
            texture.Checksum == animation.Frames[0].TextureChecksum);
        using var image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.RgbaPixels);
        var owner = new GodotExplosionFragments { Name = "OriginalExplosionFragments", animation = animation,
            texture = ImageTexture.CreateFromImage(image) };
        owner.material = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            AlbedoColor = new(1, 1, 1, 128 / 255f),
            AlbedoTexture = owner.texture,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
            TextureRepeat = false
        };
        return owner;
    }

    public override void _Ready()
    {
        AddChild(new MeshInstance3D { Name = "OriginalCFragSprites", Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        Rebuild();
    }

    public void Emit(ExplosionFragmentParameters parameters, Func<int, int> random)
    {
        CheckAlive();
        if (parameters.Animation != 6) throw new NotSupportedException("Exp_Frag's source adapter requires original animation6 WebSplat.");
        var fragment = new ExplosionFragment(parameters, animation.Frames.Count, random);
        fragments.Insert(0, fragment);
        SourceBitCreated?.Invoke(new(fragment, _ => AdvanceCapturedFragment(fragment),
            () => Contains(fragment) && fragment.Alive, () => RemoveFragment(fragment)));
        FragmentCreated?.Invoke(fragment);
        Rebuild();
    }

    public void AdvanceBits()
    {
        CheckAlive();
        foreach (ExplosionFragment fragment in fragments.ToArray()) AdvanceCapturedFragment(fragment);
        foreach (ExplosionFragment fragment in fragments.Where(fragment => !fragment.Alive).ToArray()) RemoveFragment(fragment);
        Rebuild();
    }
    public void AdvanceCapturedFragment(ExplosionFragment fragment)
    {
        CheckAlive();
        if (fragments.Contains(fragment) && fragment.Alive) fragment.AdvanceBits();
    }
    public bool Contains(ExplosionFragment fragment) => !disposed && fragments.Contains(fragment);
    public void RemoveFragment(ExplosionFragment fragment)
    {
        if (!fragments.Remove(fragment)) return;
        SourceBitRemoved?.Invoke(fragment);
        FragmentRemoved?.Invoke(fragment);
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
        SpriteAnimationFrame frame = animation.Frames[0];
        foreach (ExplosionFragment fragment in fragments)
        {
            Vector3 center = PsxCoordinates.FixedWorldPosition(fragment.Position);
            float worldDepth = -camera.GlobalBasis.Z.Dot(center - camera.GlobalPosition);
            int depth = SourceDepth(camera, fragment.Position);
            if (worldDepth <= 0 || depth < halfProjection || depth > FarDepth || fragment.Scale < 0) continue;
            Vector2 logical = camera.UnprojectPosition(center) / pixelScale;
            if (logical.X < -span / 2 || logical.X > span * 1.5f || logical.Y < -256 || logical.Y > 360) continue;
            ThugPingPixel[]? corners = ThugPingProjection.Project(frame, unchecked((ushort)fragment.Scale), 0,
                depth, (int)logical.X, (int)logical.Y, fragment.PostScale);
            if (corners is null) continue;
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
            mesh.SurfaceSetColor(GodotGlowFlash.SourceColour(fragment.CodeRgb, 0, brightness));
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
        var view = new FixedJointTransform(rotation.M00, rotation.M10, rotation.M20, rotation.M01, rotation.M11,
            rotation.M21, rotation.M02, rotation.M12, rotation.M22, 0, 0, 0);
        return FixedRotation.Transform(view, (position >> 12) -
            (PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12)).Z;
    }

    private void CheckAlive() => ObjectDisposedException.ThrowIf(disposed, this);
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        foreach (ExplosionFragment fragment in fragments.ToArray())
        {
            SourceBitRemoved?.Invoke(fragment);
            FragmentRemoved?.Invoke(fragment);
        }
        fragments.Clear();
        mesh.Dispose();
        material?.Dispose();
        texture?.Dispose();
    }
}
