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
    public required Func<GodotVenomActor, bool> IsSourceCulled { get; init; }
    public Action<VenomState>? CreateHealthBar { get; init; }
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
