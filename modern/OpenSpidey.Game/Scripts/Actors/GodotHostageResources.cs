using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Original hostage model families:15part matrix poses, without player remapping or a synthetic hook packet.</summary>
public sealed class GodotHostageResources(PkrArchive archive,PsxLibraryRepository libraries)
{
    private sealed record Resource(PsxAssetLibrary Library,IReadOnlyList<ActorPartBox> Boxes);
    private readonly Dictionary<string,Resource> resources = new(StringComparer.OrdinalIgnoreCase);
    public ImportedAnimatedModel CreateModel(string name)
    {
        Resource value=Load(name);
        return ImportedAnimatedModel.Build(value.Library.Scene,value.Library.Textures,value.Library.Animations!,playerPartOrder:false);
    }
    public IReadOnlyList<ActorPartBox> CollisionBoxes(string name) => Load(name).Boxes;
    private Resource Load(string name)
    {
        string path=PsxLibraryRepository.AssetPath(name);
        int clips=path switch { "data/hostage.psx"=>7,"data/hostagef.psx"=>14,_=>throw new NotSupportedException($"{path} is not an original hostage family.") };
        if(resources.TryGetValue(path,out Resource? value))return value;
        PsxAssetLibrary library=libraries.Load(path);
        IReadOnlyList<ActorPartBox> boxes=PsxActorCollisionBoxes.Read(archive.Read(path));
        if(library.Animations is not { } animations || animations.Parents.Count!=15 || animations.ClipCount!=clips || library.Scene.Models.Count!=15 || boxes.Count!=15)
            throw new InvalidDataException($"{path} differs from the original15part/model and{clips}clip hostage asset.");
        value=new(library,boxes);resources.Add(path,value);return value;
    }
}
