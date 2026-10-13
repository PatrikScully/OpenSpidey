using OpenSpidey.Assets.Models;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Original Black Cat/bc2 models and private pose data, retained by their actual scene library identity.</summary>
public sealed class GodotBlackCatResources(PsxLibraryRepository libraries, ReadOnlyMemory<byte> originalImage)
{
    public ImportedAnimatedModel CreateModel(string name)
    {
        string path = PsxLibraryRepository.AssetPath(name);
        if (path is not ("data/blackcat.psx" or "data/bc2.psx"))
            throw new NotSupportedException("The Black Cat factory requires original blackcat/bc2 assets.");
        PsxAssetLibrary library = libraries.Load(path);
        if (library.Animations is null || library.Animations.Parents.Count < 18 || library.Animations.ClipCount <= 12)
            throw new InvalidDataException("Original Black Cat assets require their full skeleton and cinematic clips.");
        return ImportedAnimatedModel.Build(library.Scene, library.Textures, library.Animations, playerPartOrder: false);
    }
    public OriginalBlackCatData ReadPoseData(int partCount) => OriginalBlackCatData.Read(originalImage, partCount);
}
