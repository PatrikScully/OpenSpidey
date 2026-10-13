using Godot;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Effects;

/// <summary>Actual CKnottedWebSplat surface blob, distinct from an impact-web CSplat.</summary>
public partial class GodotKnottedWebSplats : Node3D
{
    private readonly List<WebImpactSplat> splats = [];
    private readonly ImmediateMesh mesh = new();
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private ImageTexture texture = null!;
    private StandardMaterial3D material = null!;
    private bool disposed;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    public int Count => splats.Count;
    internal IReadOnlyList<WebImpactSplat> Splats => splats;
    internal ImmediateMesh RenderMesh => mesh;
    internal ImageTexture SourceTexture => texture;
    internal StandardMaterial3D RenderMaterial => material;
    public GodotKnottedWebSplats() => TopLevel = true;

    public static GodotKnottedWebSplats Create(byte[] bitsBytes)
    {
        DecodedTexture decoded = PsxTextureReader.Read(bitsBytes).Single(texture => texture.Checksum == WebImpactSplat.TextureChecksum);
        using var image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.RgbaPixels);
        var result = new GodotKnottedWebSplats { Name = "OriginalKnottedWebSplats", texture = ImageTexture.CreateFromImage(image) };
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
        Name = "OriginalKnottedSurfaceBlobs", Mesh = mesh,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
    });

    public void Emit(FixedVector3 position, ActorAngles normal, int fireVariant, Func<int, int> random)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(random);
        var splat = new WebImpactSplat(position, new(normal.X, normal.Y, normal.Z), random(4096));
        if (fireVariant != 0) splat.SetTint(255, 123, 0);
        else splat.SetTint(128, 128, 128);
        splats.Insert(0, splat);
        SourceBitCreated?.Invoke(new(splat, _ => splat.Step(),
            () => !disposed && splats.Contains(splat) && !splat.Finished,
            () => { splats.Remove(splat); Rebuild(); }));
        Rebuild();
    }

    public void AdvanceBits()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (WebImpactSplat splat in splats) splat.Step();
        splats.RemoveAll(splat => splat.Finished);
        Rebuild();
    }

    public override void _Process(double delta) => Rebuild();
    public void Rebuild()
    {
        if (disposed || !IsInsideTree()) return;
        mesh.ClearSurfaces();
        if (splats.Count == 0) return;
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, material);
        foreach (WebImpactSplat splat in splats)
        {
            mesh.SurfaceSetColor(GodotGlowFlash.SourceColour(splat.ColourTint, 0, brightness));
            foreach (int corner in new[] { 0, 1, 2, 1, 3, 2 })
            {
                mesh.SurfaceSetUV(new((corner & 1) != 0 ? .99f : .01f, (corner & 2) != 0 ? .99f : .01f));
                mesh.SurfaceAddVertex(PsxCoordinates.FixedWorldPosition(splat.Corners[corner]));
            }
        }
        mesh.SurfaceEnd();
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        if (disposed) return;
        disposed = true;
        splats.Clear(); mesh.Dispose(); material?.Dispose(); texture?.Dispose();
    }
}
