using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors.Npcs;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Real scene dependencies for source303/326/719; the session binds each hook to its canonical owner.</summary>
public sealed class GodotIdleNpcConfiguration
{
    public required Func<IdleNpcState, ImportedAnimatedModel> ModelFactory { get; init; }
    public required Func<IdleNpcState, SourceModelLight> Light { get; init; }
    public required SourceVertexLightingState LightingState { get; init; }
    public required Func<uint> Clock { get; init; }
    public required Func<bool> FastForwardRequested { get; init; }
    public required Func<GodotIdleNpcActor, bool> IsSourceCulled { get; init; }
}

/// <summary>Original mj/lizard/mariner models and per-class SLight records, retained by their actual scene library identity.</summary>
public sealed class GodotIdleNpcResources(PsxLibraryRepository libraries, ReadOnlyMemory<byte> originalImage)
{
    public ImportedAnimatedModel CreateModel(IdleNpcState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        PsxAssetLibrary library = libraries.Load(PsxLibraryRepository.AssetPath(state.ModelName));
        int clips = state.Kind switch { IdleNpcKind.MaryJane => 2, IdleNpcKind.Lizard => 6, _ => 1 };
        if (library.Animations is null || library.Animations.ClipCount < clips)
            throw new InvalidDataException($"Original {state.ModelName} assets require the {clips} clips its AI cycles.");
        return ImportedAnimatedModel.Build(library.Scene, library.Textures, library.Animations, playerPartOrder: false);
    }
    public SourceModelLight ReadLight(IdleNpcState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return SourceModelLight.Read(new OriginalExecutableData(originalImage).Slice(state.LightAddress, 52).Span);
    }
}
