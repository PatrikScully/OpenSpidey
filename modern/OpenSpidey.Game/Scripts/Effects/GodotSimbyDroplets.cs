using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Effects;

/// <summary>Original gAnimTable[24] ("symdrop" in data/symbits.psx) frame1 texture for CSimbyDroplet flat quads.</summary>
public partial class GodotSimbyDroplets : Node3D
{
    private readonly List<SimbyDropletState> droplets = [];
    private readonly ImmediateMesh mesh = new();
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private ImageTexture texture = null!;
    private StandardMaterial3D material = null!;
    private SpriteAnimationFrame frame;
    private bool disposed;
    public int Count => droplets.Count;
    public ushort FarDepth { get; set; } = 8048;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    internal IReadOnlyList<SimbyDropletState> Droplets => droplets;
    internal ImmediateMesh RenderMesh => mesh;
    internal SpriteAnimationFrame SourceFrame => frame;
    internal ImageTexture SourceTexture => texture;
    public GodotSimbyDroplets() => TopLevel = true;

    public static GodotSimbyDroplets Create(byte[] symbitsBytes)
    {
        SpriteAnimation symDrop = SpriteAnimationReader.Read(symbitsBytes).Single(animation =>
            animation.Name.Trim().Equals("symdrop", StringComparison.OrdinalIgnoreCase));
        if (symDrop.Frames.Count <= 1)
            throw new InvalidDataException("CSimbyDroplet needs SymDrop frame1 from data/symbits.psx (gAnimTable[24]).");
        SpriteAnimationFrame sourceFrame = symDrop.Frames[1];
        DecodedTexture pixels = PsxTextureReader.Read(symbitsBytes).First(texture => texture.Checksum == sourceFrame.TextureChecksum);
        byte width = unchecked((byte)(pixels.Width - 1)), height = unchecked((byte)(pixels.Height - 1));
        var centered = new SpriteAnimationFrame(unchecked((sbyte)(width / -2)), unchecked((sbyte)(height / -2)),
            width, height, pixels.Checksum);
        using var image = Image.CreateFromData(pixels.Width, pixels.Height, false, Image.Format.Rgba8, pixels.RgbaPixels);
        var result = new GodotSimbyDroplets { Name = "OriginalSimbyDroplets", frame = centered, texture = ImageTexture.CreateFromImage(image) };
        result.material = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            AlbedoColor = new(1, 1, 1, 128 / 255f),
            AlbedoTexture = result.texture,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
            TextureRepeat = false
        };
        return result;
    }

    public override void _Ready() => AddChild(new MeshInstance3D
    {
        Name = "OriginalSimbyDropletFlatBits", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
    });

    public SimbyDropletState Emit(SimbyDropletState droplet)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(droplet);
        droplets.Insert(0, droplet);
        SourceBitCreated?.Invoke(new(droplet, _ => droplet.Move(),
            () => !disposed && droplets.Contains(droplet) && droplet.Alive,
            () => { droplets.Remove(droplet); Rebuild(); }));
        Rebuild();
        return droplet;
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
        foreach (SimbyDropletState droplet in droplets)
        {
            if (!droplet.Alive) continue;
            Vector3 center = PsxCoordinates.FixedWorldPosition(droplet.Position);
            float worldDepth = -camera.GlobalBasis.Z.Dot(center - camera.GlobalPosition);
            int depth = FixedRotation.Transform(FixedRotation.TransposeRotation(PsxCoordinates.OriginalRotation(camera.GlobalBasis)),
                (droplet.Position >> 12) - (PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12)).Z;
            if (worldDepth <= 0 || depth < near || depth > FarDepth) continue;
            Vector2 logical = camera.UnprojectPosition(center) / pixelScale;
            ThugPingPixel[]? corners = ThugPingProjection.Project(frame, unchecked((ushort)droplet.Scale), droplet.Angle,
                depth, (int)logical.X, (int)logical.Y, droplet.PostScale);
            if (corners is null) continue;
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
            mesh.SurfaceSetColor(GodotGlowFlash.SourceColour(droplet.CodeBgr, SimbyDropletState.SemiTransparencyRate, brightness));
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
        disposed = true;
        droplets.Clear();
        mesh.Dispose();
        material?.Dispose();
        texture?.Dispose();
    }
}
