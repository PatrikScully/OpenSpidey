using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Player;
using OpenSpidey.Assets.Animation;

namespace OpenSpidey.Game.Import;

/// <summary>Original common effect textures, owned by the active scene.</summary>
public sealed class PlayerEffectTextures : IDisposable
{
    public ImageTexture ImpactSplat { get; }
    public ImageTexture WebKnot { get; }
    public SpriteAnimationFrame WebKnotFrame { get; }

    private PlayerEffectTextures(DecodedTexture texture, DecodedTexture knot, SpriteAnimationFrame frame)
    {
        using var image = Image.CreateFromData(texture.Width, texture.Height, false, Image.Format.Rgba8, texture.RgbaPixels);
        ImpactSplat = ImageTexture.CreateFromImage(image);
        using var knotImage = Image.CreateFromData(knot.Width, knot.Height, false, Image.Format.Rgba8, knot.RgbaPixels);
        WebKnot = ImageTexture.CreateFromImage(knotImage);
        WebKnotFrame = frame;
    }

    public static PlayerEffectTextures Load(PkrArchive archive)
    {
        byte[] bytes = archive.Read("data/bits.psx");
        IReadOnlyList<DecodedTexture> textures = PsxTextureReader.Read(bytes);
        DecodedTexture texture = textures
            .FirstOrDefault(texture => texture.Checksum == WebImpactSplat.TextureChecksum)
            ?? throw new InvalidDataException("The original web impact texture is missing from bits.psx.");
        SpriteAnimation animation = SpriteAnimationReader.Read(bytes).Single(animation => animation.Name.Equals("webknot", StringComparison.OrdinalIgnoreCase));
        if (animation.Frames.Count != 1) throw new InvalidDataException("The original zip knot requires its single protected frame.");
        SpriteAnimationFrame frame = animation.Frames[0];
        DecodedTexture knot = textures.FirstOrDefault(texture => texture.Checksum == frame.TextureChecksum)
            ?? throw new InvalidDataException("The original web knot texture is missing.");
        return new(texture, knot, frame);
    }

    public void Dispose()
    {
        ImpactSplat.Dispose();
        WebKnot.Dispose();
    }
}
