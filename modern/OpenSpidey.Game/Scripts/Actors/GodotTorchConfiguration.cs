using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors.Torch;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Real scene dependencies for source328; the session binds each hook to its canonical owners.</summary>
public sealed class GodotTorchConfiguration
{
    public required Func<ImportedAnimatedModel> ModelFactory { get; init; }
    public required Func<int, OriginalTorchData> SourceData { get; init; }
    public required DecodedTexture ShadowTexture { get; init; }
    public required SourceVertexLightingState LightingState { get; init; }
    public required Func<uint> Clock { get; init; }
    public required Func<bool> FastForwardRequested { get; init; }
    /// <summary>CTorch::AI's PreProcessWibblyTextures(model region) call, owned by the scene texture-wibble renderer.</summary>
    public required Action<GodotTorchActor> AdvanceTextureWibble { get; init; }
    public required Func<GodotTorchActor, bool> IsSourceCulled { get; init; }
    public required Action<SourceParticleRegistration> SourceShadowCreated { get; init; }
    public required Action<object> SourceShadowRemoved { get; init; }
}

/// <summary>Original torch.psx model and private CTorch pose/light data.</summary>
public sealed class GodotTorchResources(PsxLibraryRepository libraries, ReadOnlyMemory<byte> originalImage)
{
    public ImportedAnimatedModel CreateModel()
    {
        PsxAssetLibrary library = libraries.Load(PsxLibraryRepository.AssetPath(TorchState.ModelName));
        if (library.Animations is null || library.Animations.ClipCount < 6)
            throw new InvalidDataException("Original torch assets require the source walk, flame, entrance and landing clips.");
        return ImportedAnimatedModel.Build(library.Scene, library.Textures, library.Animations, playerPartOrder: false);
    }
    public OriginalTorchData ReadSourceData(int partCount) => OriginalTorchData.Read(originalImage, partCount);
}
