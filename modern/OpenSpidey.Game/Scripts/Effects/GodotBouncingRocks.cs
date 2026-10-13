using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Effects;

/// <summary>Original animation14 texture-backed flat rocks, once-per-phase motion and proportional viewport projection.</summary>
public partial class GodotBouncingRocks : Node3D
{
    private readonly List<BouncingRockState> rocks = [];
    private readonly ImmediateMesh mesh = new();
    private readonly Dictionary<int, ImageTexture> textures = [];
    private readonly Dictionary<int, StandardMaterial3D> materials = [];
    private readonly Dictionary<int, SpriteAnimationFrame> frames = [];
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private bool disposed;
    public int Count => rocks.Count;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    internal bool ResourcesDisposed => disposed;
    internal StandardMaterial3D SourceMaterial(int frame) => materials[frame];
    public static int GetGroundY(GodotActorWorldQuery world, FixedVector3 position)
    {
        ArgumentNullException.ThrowIfNull(world);
        FixedVector3 end = position with { Y = unchecked(position.Y + 0x1388000) };
        return world.Trace(position, end)?.Position.Y ?? end.Y;
    }
    public ushort FarDepth { get; set; } = 8048;
    internal ImmediateMesh RenderMesh => mesh;
    internal IReadOnlyList<BouncingRockState> Rocks => rocks;
    internal ImageTexture SourceTexture(int frame) => textures[frame];
    internal SpriteAnimationFrame SourceFrame(int frame) => frames[frame];
    public GodotBouncingRocks() => TopLevel = true;

    public static GodotBouncingRocks Create(byte[] bitsBytes)
    {
        IReadOnlyList<SpriteAnimation> animations = SpriteAnimationReader.Read(bitsBytes);
        if (animations.Count <= 14 || animations[14].Frames.Count <= 5) throw new InvalidDataException("Original rock animation14 requires texture slots2 and5.");
        var effect = new GodotBouncingRocks();
        IReadOnlyList<DecodedTexture> decodedTextures = PsxTextureReader.Read(bitsBytes);
        foreach (int slot in new[] { 2, 5 })
        {
            DecodedTexture decoded = decodedTextures.First(texture => texture.Checksum == animations[14].Frames[slot].TextureChecksum);
            if (decoded.Width > 255 || decoded.Height > 255) throw new InvalidDataException("Rock texture cannot fit original byte frame dimensions.");
            // SetTexture constructs a NEW centered full-texture frame, discarding the animation's rectangle.
            effect.frames[slot] = new(unchecked((sbyte)(-decoded.Width / 2)), unchecked((sbyte)(-decoded.Height / 2)), (byte)decoded.Width, (byte)decoded.Height, decoded.Checksum);
            using var image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.RgbaPixels);
            ImageTexture texture = ImageTexture.CreateFromImage(image);
            effect.textures[slot] = texture;
            effect.materials[slot] = new()
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
                AlphaScissorThreshold = 0.5f,
                BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
                VertexColorUseAsAlbedo = true,
                VertexColorIsSrgb = true,
                AlbedoColor = Colors.White,
                AlbedoTexture = texture,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
                TextureRepeat = false
            };
        }
        return effect;
    }

    public override void _Ready()
    {
        AddChild(new MeshInstance3D { Name = "OriginalBouncingRocks", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        Rebuild();
    }
    public void Emit(FixedVector3 position, int groundY, uint selector, Func<int, int> random)
    {
        CheckAlive();
        BouncingRockState rock = new(position, groundY, selector, random);
        rocks.Insert(0, rock);
        SourceBitCreated?.Invoke(new(rock, _ => rock.Advance(),
            () => !disposed && rocks.Contains(rock) && rock.Alive,
            () => { rocks.Remove(rock); Rebuild(); }));
        Rebuild();
    }
    /// <summary>Called once in the original flat-bit update phase, independent of global clock delta.</summary>
    public void Advance()
    {
        CheckAlive();
        foreach (BouncingRockState rock in rocks) rock.Advance();
        rocks.RemoveAll(rock => !rock.Alive);
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
        foreach (BouncingRockState rock in rocks)
        {
            Vector3 center = PsxCoordinates.FixedWorldPosition(rock.Position);
            float worldDepth = -camera.GlobalBasis.Z.Dot(center - camera.GlobalPosition);
            int depth = SourceDepth(camera, rock.Position);
            if (worldDepth <= 0 || depth < halfProjection || depth > FarDepth) continue;
            Vector2 logical = camera.UnprojectPosition(center) / pixelScale;
            if (logical.X < -span / 2 || logical.X > span * 1.5f || logical.Y < -256 || logical.Y > 360) continue;
            ThugPingPixel[]? corners = ThugPingProjection.Project(frames[rock.TextureFrame], unchecked((ushort)rock.Scale), rock.Angle, depth, (int)logical.X, (int)logical.Y, rock.PostScale);
            if (corners is null) continue;
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, materials[rock.TextureFrame]);
            mesh.SurfaceSetColor(GodotGlowFlash.SourceColour(rock.CodeBgr, 0, brightness));
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
    private void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(GodotBouncingRocks)); }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true; rocks.Clear(); mesh.Dispose(); foreach (var material in materials.Values) material.Dispose(); foreach (var texture in textures.Values) texture.Dispose(); materials.Clear(); textures.Clear();
    }
}
