using Godot;
using OpenSpidey.Assets.Textures;

namespace OpenSpidey.Game.Import;

/// <summary>Shares imported image resources and materials across all scene models.</summary>
public sealed class PsxMaterialLibrary : IDisposable
{
    private readonly Dictionary<uint, DecodedTexture> textures;
    private readonly Dictionary<uint, StandardMaterial3D> materials = [];
    private readonly StandardMaterial3D untextured;
    public bool IsDisposed { get; private set; }

    public PsxMaterialLibrary(IReadOnlyList<DecodedTexture> textures)
    {
        this.textures = [];
        foreach (DecodedTexture texture in textures) this.textures.TryAdd(texture.Checksum, texture);
        untextured = CreateMaterial();
    }

    public DecodedTexture? FindTexture(uint? checksum) =>
        checksum is { } key && textures.TryGetValue(key, out DecodedTexture? texture) ? texture : null;

    public StandardMaterial3D GetMaterial(uint? checksum)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (checksum is not { } key || FindTexture(key) is not { } texture) return untextured;
        if (materials.TryGetValue(key, out StandardMaterial3D? existing)) return existing;
        using var image = Image.CreateFromData(texture.Width, texture.Height, false, Image.Format.Rgba8, texture.RgbaPixels);
        StandardMaterial3D material = CreateMaterial();
        material.AlbedoTexture = ImageTexture.CreateFromImage(image);
        if (Enumerable.Range(0, texture.Width * texture.Height).Any(pixel => texture.RgbaPixels[pixel * 4 + 3] < 255))
            material.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
        materials.Add(key, material);
        return material;
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        foreach (StandardMaterial3D material in materials.Values)
        {
            Texture2D? texture = material.AlbedoTexture;
            material.AlbedoTexture = null;
            material.Dispose(); texture?.Dispose();
        }
        materials.Clear(); untextured.Dispose();
    }

    internal IEnumerable<StandardMaterial3D> Materials
    {
        get { ObjectDisposedException.ThrowIf(IsDisposed, this); return materials.Values.Append(untextured); }
    }

    internal Resource[] CaptureOwnedResources()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return materials.Values.Append(untextured).Cast<Resource>()
            .Concat(materials.Values.Where(material => material.AlbedoTexture is not null).Select(material => (Resource)material.AlbedoTexture!))
            .DistinctBy(resource => resource.GetInstanceId()).ToArray();
    }

    private static StandardMaterial3D CreateMaterial() => new()
    {
        VertexColorUseAsAlbedo = true,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
    };
}
