using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors.Choppers;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

public sealed class GodotChopperResources(PkrArchive archive, PsxLibraryRepository libraries)
{
    private PsxAssetLibrary? loaded;

    public ImportedAnimatedModel CreateModel()
    {
        string path = PsxLibraryRepository.AssetPath(ChopperState.ModelName);
        loaded ??= libraries.Load(path);
        if (loaded.Animations is null) throw new InvalidDataException($"{path} has no original actor animations.");
        _ = PsxActorCollisionBoxes.Read(archive.Read(path));
        return ImportedAnimatedModel.Build(loaded.Scene, loaded.Textures, loaded.Animations!, playerPartOrder: false);
    }
}
