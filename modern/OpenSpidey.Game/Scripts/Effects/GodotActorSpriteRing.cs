using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Effects;

/// <summary>Original SMOKE flat-bit quads; motion advances only on an explicit source bit update.</summary>
public partial class GodotActorSpriteRing : Node3D
{
    internal sealed record RenderedSprite(SpriteRingParticleMotion Motion, int Depth, Vector2[] LogicalCorners);
    private const int LogicalWidth = 512, LogicalHeight = 240;
    private readonly List<SpriteRingParticleMotion> particles = [];
    private readonly List<RenderedSprite> rendered = [];
    private readonly ImmediateMesh mesh = new();
    private readonly List<ImageTexture> textures = [];
    private readonly List<StandardMaterial3D> materials = [];
    private SpriteAnimationFrame[] frames = [];
    private byte[] brightness = [];
    private bool disposed;
    public int Count => particles.Count;
    public int VisibleSprites => rendered.Count;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    public event Action<object>? SourceBitRemoved;
    // M3dInit_InitAtStart's fog near6000+2048 is the initial flat-bit far gate.
    public ushort FarDepth { get; set; } = 8048;
    internal IReadOnlyList<SpriteAnimationFrame> SourceFrames => frames;
    internal IReadOnlyList<ImageTexture> Textures => textures;
    internal IReadOnlyList<StandardMaterial3D> Materials => materials;
    internal IReadOnlyList<SpriteRingParticleMotion> LiveParticles => particles;
    internal IReadOnlyList<RenderedSprite> RenderedSprites => rendered;
    internal ImmediateMesh RenderMesh => mesh;
    internal bool ResourcesDisposed => disposed;

    public GodotActorSpriteRing() => TopLevel = true;

    public static GodotActorSpriteRing Create(byte[] bitsBytes, byte brightnessIndex = 4)
    {
        SpriteAnimation animation = SpriteAnimationReader.Read(bitsBytes)
            .Single(animation => animation.Name.Equals("SMOKE", StringComparison.OrdinalIgnoreCase));
        if (animation.Frames.Count is <= 0 or > byte.MaxValue)
            throw new InvalidDataException("Original flat-bit animation frame count does not fit its source byte.");
        IReadOnlyList<DecodedTexture> decoded = PsxTextureReader.Read(bitsBytes);
        byte[] brightnessTable = Brightness(brightnessIndex);
        var effect = new GodotActorSpriteRing { Name = "OriginalSmokeRing", frames = animation.Frames.ToArray(), brightness = brightnessTable };
        try
        {
            foreach (SpriteAnimationFrame frame in effect.frames)
            {
                DecodedTexture? texture = decoded.FirstOrDefault(texture => texture.Checksum == frame.TextureChecksum);
                if (texture is null || frame.Width == 0 || frame.Height == 0)
                    throw new InvalidDataException("Original SMOKE frame has no decoded texture or flat-bit rectangle.");
                using var image = Image.CreateFromData(texture.Width, texture.Height, false, Image.Format.Rgba8, texture.RgbaPixels);
                ImageTexture uploaded = ImageTexture.CreateFromImage(image);
                effect.textures.Add(uploaded);
                effect.materials.Add(new()
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                    VertexColorUseAsAlbedo = true,
                    VertexColorIsSrgb = true,
                    TextureRepeat = false,
                    AlbedoColor = new(1, 1, 1, 128 / 255f),
                    AlbedoTexture = uploaded,
                    TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear
                });
            }
            return effect;
        }
        catch { effect.Release(); effect.Free(); throw; }
    }

    public override void _Ready()
    {
        AddChild(new MeshInstance3D { Name = "SourceFlatBitQuads", Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        Rebuild();
    }

    public void Emit(ActorSpriteRingParticle particle)
    {
        if (disposed) throw new ObjectDisposedException(nameof(GodotActorSpriteRing));
        if (particle.AnimationIndex != 1) throw new NotSupportedException("The ring emitter owns the original SMOKE quick animation only.");
        // CFlatBit::AttachTo inserts at the head of the original flat-bit list.
        var motion = new SpriteRingParticleMotion(particle, checked((byte)frames.Length));
        particles.Insert(0, motion);
        SourceBitCreated?.Invoke(new(motion, _ => motion.Advance(),
            () => !disposed && particles.Contains(motion) && motion.Alive,
            () => Remove(motion)));
        if (IsInsideTree()) Rebuild();
    }

    public void Advance()
    {
        if (disposed) throw new ObjectDisposedException(nameof(GodotActorSpriteRing));
        foreach (SpriteRingParticleMotion particle in particles) particle.Advance();
        foreach (SpriteRingParticleMotion particle in particles.Where(particle => !particle.Alive).ToArray()) Remove(particle);
        if (IsInsideTree()) Rebuild();
    }

    public override void _Process(double delta) => Rebuild();

    private void Remove(SpriteRingParticleMotion particle)
    {
        if (!particles.Remove(particle)) return;
        SourceBitRemoved?.Invoke(particle);
        Rebuild();
    }

    public void Rebuild()
    {
        if (disposed || !IsInsideTree()) return;
        mesh.ClearSurfaces();
        rendered.Clear();
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        foreach (SpriteRingParticleMotion particle in particles)
        {
            Vector3 center = PsxCoordinates.FixedWorldPosition(particle.Position);
            float worldDepth = -camera.GlobalBasis.Z.Dot(center - camera.GlobalPosition);
            if (worldDepth <= 0) continue;
            int depth = SourceDepth(camera, particle.Position);
            int zoom = camera is PlayerFollowCamera follow ? follow.ScriptRig.Zoom.Value :
                (int)Math.Round(Math.Tan(Mathf.DegToRad(camera.Fov) / 2.0) * 4096 * CameraProjection.OriginalAspect);
            int pixelAspectY = LogicalWidth * 0x3000 / (LogicalHeight << 2);
            int halfProjection = (((LogicalWidth << 11) / zoom << 12) / pixelAspectY) >> 1;
            if (depth < halfProjection || depth < 200 || depth > FarDepth) continue;
            SpriteAnimationFrame frame = frames[particle.Frame];
            Vector2 screen = camera.UnprojectPosition(center);
            float scaleY = viewport.Y / LogicalHeight, scaleX = scaleY * 0.625f;
            float logicalSpan = viewport.X / scaleX;
            Vector2 logical = new(screen.X / scaleX, screen.Y / scaleY);
            Vector2[]? corners = Project(frame, particle.Scale, depth, logical, logicalSpan);
            if (corners is null) continue;
            Color color = GodotGlowFlash.SourceColour(particle.CodeRgb, 0, brightness);
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, materials[particle.Frame]);
            mesh.SurfaceSetColor(color);
            foreach (int corner in new[] { 0, 1, 2, 1, 3, 2 })
            {
                Vector2 pixel = new(corners[corner].X * scaleX, corners[corner].Y * scaleY);
                mesh.SurfaceSetUV(new((corner & 1) != 0 ? 0.99f : 0.01f, (corner & 2) != 0 ? 0.99f : 0.01f));
                mesh.SurfaceAddVertex(camera.ProjectPosition(pixel, worldDepth));
            }
            mesh.SurfaceEnd();
            rendered.Add(new(particle, depth, corners));
        }
    }

    private static int SourceDepth(Camera3D camera, FixedVector3 position)
    {
        FixedJointTransform rotation = PsxCoordinates.OriginalRotation(camera.GlobalBasis);
        var view = new FixedJointTransform(rotation.M00, rotation.M10, rotation.M20,
            rotation.M01, rotation.M11, rotation.M21, rotation.M02, rotation.M12, rotation.M22, 0, 0, 0);
        FixedVector3 relative = (position >> 12) - (PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12);
        return FixedRotation.Transform(view, relative).Z;
    }

    // Source angle0 sizing/offsets. The native anchor stays at the camera projection;
    // Hor+ extends the logical horizontal span while retaining the original4:3 pixel aspect.
    private static Vector2[]? Project(SpriteAnimationFrame frame, short scale, int depth, Vector2 center, float logicalSpan)
    {
        FlatSpriteRectangle? source = FlatSpriteProjection.Project(frame, unchecked((ushort)scale), depth);
        if (source is not { } rectangle) return null;
        float left = center.X + rectangle.X, top = center.Y + rectangle.Y;
        if (left < -logicalSpan / 2 || left > logicalSpan * 1.5f || top < -256 || top > 360) return null;
        return [new(left, top), new(left + rectangle.Width, top), new(left, top + rectangle.Height),
            new(left + rectangle.Width, top + rectangle.Height)];
    }

    private static byte[] Brightness(byte index)
    {
        float[] powers = [0.8f, 0.85f, 0.9f, 0.95f, 1f, 1.05f, 1.1f, 1.15f];
        if (index >= powers.Length) throw new ArgumentOutOfRangeException(nameof(index));
        var table = new byte[256];
        float reciprocal = 1f / powers[index];
        for (int i = 0; i < table.Length; i++)
        {
            float value = (float)Math.Pow(i / 255f, reciprocal);
            int mapped = GameWord.TruncateToInt32(value * 255f + 0.5f);
            table[i] = (byte)(mapped >= 128 ? 255 : mapped * 2);
        }
        return table;
    }

    private void Release()
    {
        if (disposed) return;
        disposed = true;
        foreach (SpriteRingParticleMotion particle in particles.ToArray()) SourceBitRemoved?.Invoke(particle);
        particles.Clear(); rendered.Clear();
        mesh.Dispose();
        foreach (StandardMaterial3D material in materials) material.Dispose();
        foreach (ImageTexture texture in textures) texture.Dispose();
    }

    public override void _Notification(int what)
    { if (what == NotificationPredelete) Release(); }
}
