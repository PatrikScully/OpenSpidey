using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors.DocOck;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

public sealed class GodotDocOckConfiguration
{
    public required Func<ImportedAnimatedModel> ModelFactory { get; init; }
    public required Func<SourceModelLight> Light { get; init; }
    public required SourceVertexLightingState LightingState { get; init; }
    public required Func<uint> Clock { get; init; }
    public required Func<bool> WhatIf { get; init; }
    public required Action<DocOckState> CreateHealthBar { get; init; }
    public required Func<GodotDocOckActor, bool> IsSourceCulled { get; init; }
}

public sealed class GodotDocOckResources(PsxLibraryRepository libraries, ReadOnlyMemory<byte> originalImage)
{
    public ImportedAnimatedModel CreateModel()
    {
        PsxAssetLibrary library = libraries.Load(PsxLibraryRepository.AssetPath(DocOckState.ModelName));
        if (library.Animations is null || library.Animations.ClipCount == 0)
            throw new InvalidDataException("Original docock assets require animation data.");
        return ImportedAnimatedModel.Build(library.Scene, library.Textures, library.Animations, playerPartOrder: false);
    }

    public SourceModelLight ReadLight() =>
        SourceModelLight.Read(new OriginalExecutableData(originalImage).Slice(DocOckState.LightAddress, 52).Span);
}
