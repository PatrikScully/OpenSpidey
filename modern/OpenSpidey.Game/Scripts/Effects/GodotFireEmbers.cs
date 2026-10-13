using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Effects;

/// <summary>Actual simby5 CEmber flat bits, using Misc frame5's texture and CFT4Bit's synthetic texture rectangle.</summary>
public partial class GodotFireEmbers : Node3D
{
    private readonly List<FireEmber> embers = [];
    private readonly ImmediateMesh mesh = new();
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private ImageTexture texture = null!;
    private StandardMaterial3D material = null!;
    private SpriteAnimationFrame frame;
    private bool disposed;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    public int Count => embers.Count;
    public ushort FarDepth { get; set; } = 8048;
    internal IReadOnlyList<FireEmber> Embers => embers;
    internal ImmediateMesh RenderMesh => mesh;
    internal SpriteAnimationFrame SourceFrame => frame;
    internal ImageTexture SourceTexture => texture;
    public GodotFireEmbers() => TopLevel = true;

    public static GodotFireEmbers Create(byte[] bitsBytes)
    {
        SpriteAnimation misc = SpriteAnimationReader.Read(bitsBytes).Single(animation => animation.Name.Equals("Misc", StringComparison.OrdinalIgnoreCase));
        if (misc.Frames.Count <= 5) throw new InvalidDataException("CEmber needs actual Misc frame5's source texture pointer.");
        DecodedTexture pixels = PsxTextureReader.Read(bitsBytes).First(texture => texture.Checksum == misc.Frames[5].TextureChecksum);
        // Spool PC texture UVs are u0/v0=0,u1=width-1,v2=height-1. SetTexture creates this one-frame rectangle.
        byte width = unchecked((byte)(pixels.Width - 1)), height = unchecked((byte)(pixels.Height - 1));
        var frame = new SpriteAnimationFrame(unchecked((sbyte)(width / -2)), unchecked((sbyte)(height / -2)),
            width, height, pixels.Checksum);
        using var image = Image.CreateFromData(pixels.Width, pixels.Height, false, Image.Format.Rgba8, pixels.RgbaPixels);
        var result = new GodotFireEmbers { Name = "OriginalFireWebEmbers", frame = frame, texture = ImageTexture.CreateFromImage(image) };
        result.material = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
            VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
            AlbedoColor = new(1, 1, 1, 128 / 255f), AlbedoTexture = result.texture,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear, TextureRepeat = false
        };
        return result;
    }

    public override void _Ready() => AddChild(new MeshInstance3D
    {
        Name = "OriginalMiscTextureFireEmbers", Mesh = mesh,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
    });

    public FireEmber Emit(FixedVector3 position, int scale, Func<int, int> random)
    {
        ObjectDisposedException.ThrowIf(disposed, this); ArgumentNullException.ThrowIfNull(random);
        var ember = new FireEmber(position, scale, random);
        embers.Insert(0, ember);
        SourceBitCreated?.Invoke(new(ember, _ => ember.AdvanceBits(random),
            () => !disposed && embers.Contains(ember) && ember.Alive,
            () => { embers.Remove(ember); Rebuild(); }));
        Rebuild(); return ember;
    }
    public void AdvanceBits(Func<int, int> random)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (FireEmber ember in embers) ember.AdvanceBits(random);
        embers.RemoveAll(ember => !ember.Alive); Rebuild();
    }
    public override void _Process(double delta) => Rebuild();

    public void Rebuild()
    {
        if (disposed || !IsInsideTree()) return;
        mesh.ClearSurfaces();
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null) return;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (size.X <= 0 || size.Y <= 0) return;
        Vector2 pixelScale = new(size.Y / 240 * .625f, size.Y / 240);
        int zoom = camera is PlayerFollowCamera follow ? follow.ScriptRig.Zoom.Value :
            (int)Math.Round(Math.Tan(Mathf.DegToRad(camera.Fov) / 2.0) * 4096 * CameraProjection.OriginalAspect);
        int near = (((512 << 11) / zoom << 12) / (512 * 0x3000 / (240 << 2))) >> 1;
        foreach (FireEmber ember in embers)
        {
            if (!ember.Alive) continue;
            Vector3 center = PsxCoordinates.FixedWorldPosition(ember.Position);
            float worldDepth = -camera.GlobalBasis.Z.Dot(center - camera.GlobalPosition);
            int depth = FixedRotation.Transform(FixedRotation.TransposeRotation(PsxCoordinates.OriginalRotation(camera.GlobalBasis)),
                (ember.Position >> 12) - (PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12)).Z;
            if (worldDepth <= 0 || depth < near || depth > FarDepth) continue;
            Vector2 logical = camera.UnprojectPosition(center) / pixelScale;
            ThugPingPixel[]? corners = ThugPingProjection.Project(frame, unchecked((ushort)ember.Scale), ember.Angle,
                depth, (int)logical.X, (int)logical.Y, ember.PostScale);
            if (corners is null) continue;
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
            mesh.SurfaceSetColor(GodotGlowFlash.SourceColour(ember.CodeRgb, 0, brightness));
            foreach (int corner in new[] { 0, 1, 2, 1, 3, 2 })
            {
                Vector2 pixel = (new Vector2(corners[corner].X, corners[corner].Y) + logical -
                    new Vector2((int)logical.X, (int)logical.Y)) * pixelScale;
                mesh.SurfaceSetUV(new((corner & 1) != 0 ? .99f : .01f, (corner & 2) != 0 ? .99f : .01f));
                mesh.SurfaceAddVertex(camera.ProjectPosition(pixel, worldDepth));
            }
            mesh.SurfaceEnd();
        }
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        if (disposed) return;
        disposed = true; embers.Clear(); mesh.Dispose(); material?.Dispose(); texture?.Dispose();
    }
}
