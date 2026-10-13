using System.Buffers.Binary;
using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Effects;

/// <summary>Real CItem::Burst/CItemFrag source quad particles, original face textures and shared QuadBitList ownership.</summary>
public partial class GodotItemBurstFragments : Node3D, IItemBurstHost
{
    private sealed record Texture(DecodedTexture Source, ImageTexture Image, StandardMaterial3D Material);
    private readonly List<ItemBurstFragment> fragments = [];
    private readonly Dictionary<uint, Texture> textures = [];
    private readonly ImmediateMesh mesh = new();
    private readonly byte[] brightness = GodotGlowFlash.SourceBrightness();
    private readonly StandardMaterial3D untextured = Material();
    private Func<FixedVector3, int> ground = null!;
    private Func<int, int> random = null!;
    private bool disposed;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    public int Count => fragments.Count;
    internal IReadOnlyList<ItemBurstFragment> Fragments => fragments;
    internal ImmediateMesh RenderMesh => mesh;
    public GodotItemBurstFragments() => TopLevel = true;
    public static GodotItemBurstFragments Create(Func<FixedVector3, int> groundY, Func<int, int> sourceRandom)
    {
        ArgumentNullException.ThrowIfNull(groundY); ArgumentNullException.ThrowIfNull(sourceRandom);
        return new() { Name = "OriginalItemBurstQuads", ground = groundY, random = sourceRandom };
    }
    public override void _Ready() => AddChild(new MeshInstance3D { Name = "SourceItemFragmentQuads", Mesh = mesh,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    public void Burst(PsxAssetLibrary bank, int modelIndex, FixedVector3 position, ActorAngles angles, int speed, int randomSpeed)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (DecodedTexture texture in bank.Textures) EnsureTexture(texture);
        ItemBurst.Create(bank.Scene.Models[modelIndex], position, angles, speed, randomSpeed, this, random);
        Rebuild();
    }
    public int GetGroundY(FixedVector3 position) => ground(position);
    public void Emit(ItemBurstFragment fragment)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (fragment.Face.TextureChecksum is uint checksum && !textures.ContainsKey(checksum))
            throw new InvalidDataException("CItemFrag needs its genuine source face texture before construction.");
        fragments.Insert(0, fragment);
        SourceBitCreated?.Invoke(new(fragment, _ => fragment.AdvanceBits(random),
            () => !disposed && fragments.Contains(fragment) && fragment.Alive,
            () => { fragments.Remove(fragment); Rebuild(); }));
    }
    public override void _Process(double delta) => Rebuild();
    public void Rebuild()
    {
        if (disposed || !IsInsideTree()) return;
        mesh.ClearSurfaces();
        foreach (ItemBurstFragment fragment in fragments)
        {
            if (!fragment.Alive) continue;
            Texture? texture = fragment.Face.TextureChecksum is uint checksum ? textures[checksum] : null;
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, texture?.Material ?? untextured);
            mesh.SurfaceSetColor(GodotGlowFlash.SourceColour(fragment.Tint, 0, brightness));
            foreach (int corner in new[] { 0, 1, 2, 1, 3, 2 })
            {
                mesh.SurfaceSetUV(Uv(fragment.Face, corner, texture?.Source));
                mesh.SurfaceAddVertex(PsxCoordinates.FixedWorldPosition(fragment.Corners[corner]));
            }
            mesh.SurfaceEnd();
        }
    }
    private void EnsureTexture(DecodedTexture source)
    {
        if (textures.ContainsKey(source.Checksum)) return;
        using Image image = Image.CreateFromData(source.Width, source.Height, false, Image.Format.Rgba8, source.RgbaPixels);
        ImageTexture native = ImageTexture.CreateFromImage(image);
        StandardMaterial3D material = Material(); material.AlbedoTexture = native;
        textures.Add(source.Checksum, new(source, native, material));
    }
    private static StandardMaterial3D Material() => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled, AlbedoColor = new(1, 1, 1, 128 / 255f),
        TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear, TextureRepeat = false
    };
    private static Vector2 Uv(PsxFace face, int corner, DecodedTexture? texture)
    {
        if (face.TextureCoordinates.Length == 16)
            return new(BinaryPrimitives.ReadUInt16LittleEndian(face.TextureCoordinates.AsSpan(corner * 2, 2)) / 512f,
                BinaryPrimitives.ReadUInt16LittleEndian(face.TextureCoordinates.AsSpan(8 + corner * 2, 2)) / 512f);
        if (face.TextureCoordinates.Length == 8 && texture is not null)
            return new(face.TextureCoordinates[corner * 2] / (float)texture.Width,
                face.TextureCoordinates[corner * 2 + 1] / (float)texture.Height);
        return Vector2.Zero;
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        if (disposed) return;
        disposed = true; fragments.Clear(); mesh.Dispose(); untextured.Dispose();
        foreach (Texture texture in textures.Values) { texture.Material.Dispose(); texture.Image.Dispose(); }
        textures.Clear();
    }
}
