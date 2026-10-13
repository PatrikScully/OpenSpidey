using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Session-owned source animation and collision resources for the original thug model family.</summary>
public sealed class GodotThugResources(PkrArchive archive, PsxLibraryRepository libraries)
{
    private sealed record Resource(PsxAssetLibrary Library, IReadOnlyList<ActorPartBox> CollisionBoxes);
    private readonly Dictionary<string, Resource> resources = new(StringComparer.OrdinalIgnoreCase);

    public ImportedAnimatedModel CreateModel(string name)
    {
        Resource resource = Load(name);
        return ImportedAnimatedModel.Build(resource.Library.Scene, resource.Library.Textures,
            resource.Library.Animations!, playerPartOrder: false);
    }

    public IReadOnlyList<ActorPartBox> CollisionBoxes(string name) => Load(name).CollisionBoxes;

    private Resource Load(string name)
    {
        string path = PsxLibraryRepository.AssetPath(name);
        if (path is not ("data/thug.psx" or "data/henchman.psx" or "data/henchngt.psx"))
            throw new NotSupportedException($"{path} is not an original thug model family.");
        if (resources.TryGetValue(path, out Resource? resource)) return resource;
        PsxAssetLibrary library = libraries.Load(path);
        if (library.Animations is null) throw new InvalidDataException($"{path} has no original actor animations.");
        IReadOnlyList<ActorPartBox> boxes = PsxActorCollisionBoxes.Read(archive.Read(path));
        if (boxes.Count != library.Animations.Parents.Count)
            throw new InvalidDataException($"{path} collision parts do not match its source skeleton.");
        resource = new(library, boxes);
        resources.Add(path, resource);
        return resource;
    }
}
