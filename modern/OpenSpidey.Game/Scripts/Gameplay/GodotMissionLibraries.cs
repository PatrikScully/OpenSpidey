using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Import;
using OpenSpidey.Assets.Textures;

namespace OpenSpidey.Game.Gameplay;

/// <summary>Owns a level's decoded libraries and environment nodes during initialization.</summary>
public partial class GodotMissionLibraries : Node3D, ISceneLibraryHost
{
    public PsxLibraryRepository Repository { get; init; } = null!;
    public ImportedLevelWorld? World { get; private set; }
    public PsxAssetLibrary? ObjectLibrary { get; private set; }
    public event Action<ImportedLevelWorld>? EnvironmentLoaded;

    public void LoadLibrary(string name) => Repository.Load(name);

    public void LoadProtectedCommonLibraries(PsxAssetRegions regions)
    {
        // Init_AtStart's full-graphics bank order, before any mission autoexec or actor construction.
        string[] names = ["webdome2", "webdome3", "spidey", "bits", "spArmor", "shell_dc", "costarm", "items", "ring", "sparmour"];
        foreach (string name in names) regions.SetProtected(Repository.Load(name));
    }

    public void ClearUnprotectedLibraries(PsxAssetRegions regions) =>
        regions.ClearUnprotected(library => UnloadLibrary(library.AssetPath));

    public void LoadEnvironment(string name)
    {
        string path = PsxLibraryRepository.AssetPath(name);
        if (World?.Sources.Any(source => source.AssetPath.Equals(path, StringComparison.OrdinalIgnoreCase)) == true)
            return;
        ImportedLevelWorld next = ImportedLevelWorld.Load(Repository, path);
        ClearWorld();
        World = next;
        AddChild(next);
        EnvironmentLoaded?.Invoke(next);
    }

    public void SelectObjectLibrary(string name) => ObjectLibrary = Repository.RequireLoaded(name);

    public IReadOnlyList<DecodedTexture> ObjectTextures()
    {
        PsxAssetLibrary objects = ObjectLibrary ?? throw new InvalidOperationException("No object library has been selected.");
        string texturePath = objects.AssetPath.EndsWith("_O.psx", StringComparison.OrdinalIgnoreCase)
            ? objects.AssetPath[..^6] + "_L.psx" : objects.AssetPath;
        return Repository.Loaded.TryGetValue(texturePath, out PsxAssetLibrary? textures)
            ? textures.Textures.Concat(objects.Textures).ToArray() : objects.Textures;
    }

    public void UnloadLibrary(string name)
    {
        string path = PsxLibraryRepository.AssetPath(name);
        if (World is not null)
        {
            World.UnloadSource(path);
            if (World.Sources.Count == 0) ClearWorld();
        }
        if (ObjectLibrary?.AssetPath.Equals(path, StringComparison.OrdinalIgnoreCase) == true) ObjectLibrary = null;
        Repository.Unload(path);
    }

    private void ClearWorld()
    {
        if (World is null) return;
        RemoveChild(World);
        World.Free();
        World = null;
    }
}
