using Godot;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Effects;

/// <summary>Original CSplat world surface quad and PC alpha blend; distinct from CKnottedWebSplat.</summary>
public partial class GodotWebSurfaceSplats : Node3D
{
    private readonly List<WebSurfaceSplat> splats = [];
    private readonly ImmediateMesh mesh = new();
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private ImageTexture texture = null!;
    private StandardMaterial3D material = null!;
    private bool disposed;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    public int Count => splats.Count;
    internal IReadOnlyList<WebSurfaceSplat> Splats => splats;
    internal ImmediateMesh RenderMesh => mesh;
    internal ImageTexture SourceTexture => texture;
    internal StandardMaterial3D RenderMaterial => material;
    public GodotWebSurfaceSplats() => TopLevel = true;

    public static GodotWebSurfaceSplats Create(byte[] shellPsxBytes)
    {
        if (shellPsxBytes.Length >= 2 && shellPsxBytes[0] == (byte)'0' && shellPsxBytes[1] == (byte)'x')
            throw new InvalidDataException("CSplat requires original data/shell_dc.psx image packets; shell_dc.tex only maps names to checksums.");
        DecodedTexture decoded = PsxTextureReader.Read(shellPsxBytes).Single(t => t.Checksum == WebSurfaceSplat.TextureChecksum);
        using var image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.RgbaPixels);
        var result = new GodotWebSurfaceSplats { Name = "OriginalWebSurfaceSplats", texture = ImageTexture.CreateFromImage(image) };
        result.material = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled, VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true, AlbedoColor = Colors.White, AlbedoTexture = result.texture,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear, TextureRepeat = false
        };
        return result;
    }
    public override void _Ready() => AddChild(new MeshInstance3D { Name = "OriginalCSplatQuads", Mesh = mesh,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    public void Emit(FixedVector3 position, ActorAngles normal)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var splat = new WebSurfaceSplat(position, normal);
        splats.Insert(0, splat);
        SourceBitCreated?.Invoke(new(splat, _ => splat.AdvanceBits(),
            () => !disposed && splats.Contains(splat) && splat.Alive,
            () => { splats.Remove(splat); Rebuild(); }));
        Rebuild();
    }
    public void AdvanceBits()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (WebSurfaceSplat splat in splats) splat.AdvanceBits();
        splats.RemoveAll(splat => !splat.Alive); Rebuild();
    }
    public override void _Process(double delta) => Rebuild();
    public void Rebuild()
    {
        if (disposed || !IsInsideTree()) return;
        mesh.ClearSurfaces();
        if (splats.Count == 0) return;
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
        foreach (WebSurfaceSplat splat in splats)
        {
            mesh.SurfaceSetColor(GodotGlowFlash.SourceColour(splat.Tint, 0, brightness));
            foreach (int index in new[] { 0, 1, 2, 1, 3, 2 })
            {
                mesh.SurfaceSetUV(new((index & 1) != 0 ? .99f : .01f, (index & 2) != 0 ? .99f : .01f));
                mesh.SurfaceAddVertex(PsxCoordinates.FixedWorldPosition(splat.Corners[index]));
            }
        }
        mesh.SurfaceEnd();
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        if (disposed) return;
        disposed = true; splats.Clear(); mesh.Dispose(); material?.Dispose(); texture?.Dispose();
    }
}
