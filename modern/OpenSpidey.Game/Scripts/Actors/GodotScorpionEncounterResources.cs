using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Genuine Scorpion/Jameson/Joel models, complete animation banks and original per-part collision boxes.</summary>
public sealed class GodotScorpionEncounterResources(PkrArchive archive, PsxLibraryRepository libraries, ReadOnlyMemory<byte> originalImage)
{
    private sealed record Resource(PsxAssetLibrary Library, IReadOnlyList<ActorPartBox> Boxes);
    private readonly Dictionary<string, Resource> resources = new(StringComparer.OrdinalIgnoreCase);
    public OriginalScorpionEncounterData SourceData { get; } = OriginalScorpionEncounterData.Read(originalImage);

    public ImportedAnimatedModel CreateModel(string name)
    {
        Resource resource = Load(name);
        return ImportedAnimatedModel.Build(resource.Library.Scene, resource.Library.Textures,
            resource.Library.Animations!, playerPartOrder: false);
    }
    public IReadOnlyList<ActorPartBox> CollisionBoxes(string name) => Load(name).Boxes;

    private Resource Load(string name)
    {
        string path = PsxLibraryRepository.AssetPath(name);
        (int parts, int clips) = path switch
        {
            "data/scorpion.psx" => (24, 30),
            "data/jameson.psx" or "data/jjjj.psx" => (16, 26),
            _ => throw new NotSupportedException($"{path} is not an original Scorpion encounter model.")
        };
        if (resources.TryGetValue(path, out Resource? found)) return found;
        PsxAssetLibrary library = libraries.Load(path);
        IReadOnlyList<ActorPartBox> boxes = PsxActorCollisionBoxes.Read(archive.Read(path));
        if (library.Animations is not { } animations || animations.Parents.Count != parts ||
            animations.ClipCount != clips || library.Scene.Models.Count != parts || boxes.Count != parts)
            throw new InvalidDataException($"{path} differs from its original{parts}-part and{clips}-clip actor bank.");
        var resource = new Resource(library, boxes);
        resources.Add(path, resource);
        return resource;
    }
}
