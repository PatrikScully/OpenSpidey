using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors.LizMan;
using OpenSpidey.Core.Actors.Turret;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

public sealed class GodotTurretConfiguration
{
    public required Func<ImportedAnimatedModel> ModelFactory { get; init; }
    public required Func<SourceModelLight> Light { get; init; }
    public required SourceVertexLightingState LightingState { get; init; }
    public required Func<uint> Clock { get; init; }
    public required Func<GodotTurretActor, bool> IsSourceCulled { get; init; }
}

public sealed class GodotTurretResources(PsxLibraryRepository libraries, ReadOnlyMemory<byte> originalImage)
{
    public ImportedAnimatedModel CreateModel()
    {
        PsxAssetLibrary library = libraries.Load(PsxLibraryRepository.AssetPath("turret"));
        if (library.Animations is null || library.Animations.ClipCount == 0)
            throw new InvalidDataException("Original turret assets require animation data.");
        return ImportedAnimatedModel.Build(library.Scene, library.Textures, library.Animations, playerPartOrder: false);
    }

    public SourceModelLight ReadLight() =>
        SourceModelLight.Read(new OriginalExecutableData(originalImage).Slice(LizManState.LightAddress, 52).Span);

    public ActorHookPacket ReadHooks(int partCount) =>
        ActorHookPacket.Read(new OriginalExecutableData(originalImage).Slice(TurretState.HooksPacketAddress, 64).Span, partCount);
}
