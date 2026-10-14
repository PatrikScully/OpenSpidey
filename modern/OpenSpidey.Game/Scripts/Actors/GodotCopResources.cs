using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

public sealed class GodotCopResources(PkrArchive archive, PsxLibraryRepository libraries)
{
    private readonly Dictionary<string, PsxAssetLibrary> loaded = new(StringComparer.OrdinalIgnoreCase);

    public ImportedAnimatedModel CreateModel(string name)
    {
        if (name is not ("police" or "swat")) throw new NotSupportedException($"Unknown cop model {name}.");
        string path = PsxLibraryRepository.AssetPath(name);
        if (!loaded.TryGetValue(path, out PsxAssetLibrary? library))
        {
            library = libraries.Load(path);
            if (library.Animations is null) throw new InvalidDataException($"{path} has no original actor animations.");
            _ = PsxActorCollisionBoxes.Read(archive.Read(path));
            loaded[path] = library;
        }
        return ImportedAnimatedModel.Build(library.Scene, library.Textures, library.Animations!, playerPartOrder: false);
    }
}
