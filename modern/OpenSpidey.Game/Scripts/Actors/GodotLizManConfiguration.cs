using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors.LizMan;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Real scene dependencies for source317; the session binds each hook to its canonical owners.</summary>
public sealed class GodotLizManConfiguration
{
    public required Func<LizManState, ImportedAnimatedModel> ModelFactory { get; init; }
    public required Func<SourceModelLight> Light { get; init; }
    public required SourceVertexLightingState LightingState { get; init; }
    public required Func<uint> Clock { get; init; }
    public required Func<int> AttackClock { get; init; }
    public required Func<int> TotalTime { get; init; }
    public required Func<int, int> Random { get; init; }
    public required Func<GodotLizManActor, bool> IsSourceCulled { get; init; }
    /// <summary>Session-wide gGlobalLizMan / gLizManAttackFlag / 0x682C48 owners.</summary>
    public LizManAttackOwnership AttackOwnership { get; } = new();
}

/// <summary>Original lizman.psx / lizman2.psx models and M3d_DefaultLight.</summary>
public sealed class GodotLizManResources(PsxLibraryRepository libraries, ReadOnlyMemory<byte> originalImage)
{
    public ImportedAnimatedModel CreateModel(LizManState state)
    {
        PsxAssetLibrary library = libraries.Load(PsxLibraryRepository.AssetPath(state.ModelName));
        if (library.Animations is null || library.Animations.ClipCount < 6)
            throw new InvalidDataException("Original lizman assets require the source locomotion and combat clips.");
        return ImportedAnimatedModel.Build(library.Scene, library.Textures, library.Animations, playerPartOrder: false);
    }

    public SourceModelLight ReadLight() =>
        SourceModelLight.Read(new OriginalExecutableData(originalImage).Slice(LizManState.LightAddress, 52).Span);
}
