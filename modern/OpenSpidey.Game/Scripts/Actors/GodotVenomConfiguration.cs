using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors.Venom;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

public sealed class GodotVenomConfiguration
{
    public required Func<VenomState, ImportedAnimatedModel> ModelFactory { get; init; }
    public required Func<SourceModelLight> Light { get; init; }
    public required SourceVertexLightingState LightingState { get; init; }
    public required Func<int> DifficultyLevel { get; init; }
    /// <summary>gTimerRelated (0x6B4CA8).</summary>
    public required Func<int> TimerRelated { get; init; }
    public required Func<bool> FastForwardRequested { get; init; }
    public required Func<GodotVenomActor, bool> IsSourceCulled { get; init; }
    public Action<VenomState>? CreateHealthBar { get; init; }
    /// <summary>Actor-owned trap (false) or tug (true) web contact behind CBaddy 0x104/0x10C; null when none exists.</summary>
    public Func<GodotVenomActor, bool, OpenSpidey.Core.Actors.Thugs.IThugTrapWeb?> RecoverWeb { get; init; } = (_, _) => null;
    public Func<IReadOnlyList<VenomControlSwitch>> EnumerateControlSwitches { get; init; } =
        () => throw new NotSupportedException("Venom switch resolution requires the source control list.");
}

public sealed class GodotVenomResources(PsxLibraryRepository libraries, ReadOnlyMemory<byte> originalImage)
{
    public ImportedAnimatedModel CreateModel(VenomState state)
    {
        string model = state.ModelName;
        PsxAssetLibrary library = libraries.Load(PsxLibraryRepository.AssetPath(model));
        if (library.Animations is null || library.Animations.ClipCount < 1)
            throw new InvalidDataException($"Original {model} assets require the source animation bank.");
        return ImportedAnimatedModel.Build(library.Scene, library.Textures, library.Animations, playerPartOrder: false);
    }

    public SourceModelLight ReadLight() =>
        SourceModelLight.Read(new OriginalExecutableData(originalImage).Slice(VenomState.LightAddress, 52).Span);
}
