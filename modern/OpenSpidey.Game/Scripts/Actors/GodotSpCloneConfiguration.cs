using System.Buffers.Binary;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors.SpClone;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

public sealed class GodotSpCloneConfiguration
{
    public required Func<ImportedAnimatedModel> ModelFactory { get; init; }
    public required Func<SourceModelLight> Light { get; init; }
    public required Func<int, ActorPoseLinkPacket> PoseLinks { get; init; }
    public required DecodedTexture ShadowTexture { get; init; }
    public required SourceVertexLightingState LightingState { get; init; }
    public required Func<uint> Clock { get; init; }
    public required Func<bool> FastForwardRequested { get; init; }
    public required Func<GodotSpCloneActor, bool> IsSourceCulled { get; init; }
    public required Action<SourceParticleRegistration> SourceShadowCreated { get; init; }
    public required Action<object> SourceShadowRemoved { get; init; }
}

public sealed class GodotSpCloneResources(PsxLibraryRepository libraries, ReadOnlyMemory<byte> originalImage)
{
    public ImportedAnimatedModel CreateModel()
    {
        PsxAssetLibrary library = libraries.Load(PsxLibraryRepository.AssetPath(SpCloneState.ModelName));
        if (library.Animations is null || library.Animations.ClipCount == 0)
            throw new InvalidDataException("Original spclone assets require spidey animation data.");
        return ImportedAnimatedModel.Build(library.Scene, library.Textures, library.Animations, playerPartOrder: false);
    }

    public SourceModelLight ReadLight() =>
        SourceModelLight.Read(new OriginalExecutableData(originalImage).Slice(SpCloneState.LightAddress, 52).Span);

    /// <summary>G_UNK_POSE 0x555888: twelve source pose links.</summary>
    public ActorPoseLinkPacket ReadPoseLinks(int partCount)
    {
        OriginalExecutableData data = new(originalImage);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(SpCloneState.PosePacketAddress, 4).Span[2..]);
        if (count != 12) throw new InvalidDataException("Original CSpClone pose packet requires the source twelve links.");
        return ActorPoseLinkPacket.Read(data.Slice(SpCloneState.PosePacketAddress, checked(4 + count * 12)).Span, partCount);
    }
}
