using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;

namespace OpenSpidey.Assets.Models;

/// <summary>A decoded source library retains the mesh names used by actor scripts.</summary>
public sealed record PsxAssetLibrary(string AssetPath, PsxScene Scene,
    IReadOnlyList<DecodedTexture> Textures, IAnimationPoseSource? Animations)
{
    public static PsxAssetLibrary Read(string path, ReadOnlySpan<byte> bytes)
    {
        IAnimationPoseSource? animations = PsxCompressedAnimationReader.Read(bytes);
        animations ??= PsxMatrixAnimationReader.Read(bytes);
        return new(path, PsxSceneReader.Read(bytes), PsxTextureReader.Read(bytes), animations);
    }

    public int FindModel(uint checksum)
    {
        // Spool_GetModel chooses the first model when checksum entries repeat.
        for (int index = 0; index < Scene.Models.Count; index++)
            if (Scene.Models[index].Checksum == checksum) return index;
        throw new KeyNotFoundException($"Model {checksum:X8} is missing from {AssetPath}.");
    }
}
