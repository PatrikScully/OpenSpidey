using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Required scene owners shared by every original pickup; the actor itself owns its model, shadow and protected flat.</summary>
public sealed class GodotPowerUpConfiguration
{
    public required PsxAssetLibrary Items { get; init; }
    public required DecodedTexture ShadowTexture { get; init; }
    public required Func<DecodedTexture> TrainingPickupTexture { get; init; }
    public required GodotPlayerWorldQuery World { get; init; }
    public required GodotGlowFlash Glows { get; init; }
    public required CampaignProgress Progress { get; init; }
    public required PlayerPowerUpCollectionState Collection { get; init; }
    public required Func<uint> SourceClock { get; init; }
    public required Func<int> LogicFrame { get; init; }
    public required Func<bool> SpeechBusy { get; init; }
    public required Action<ushort> SendSignal { get; init; }
    public required Action<ushort, int, int> PlaySound { get; init; }
    public required Action<int, int, int> PlaySpeech { get; init; }
    public required Func<bool, bool> SetArmor { get; init; }
    public required Action<SourceParticleList, SourceParticleRegistration> SourceBitCreated { get; init; }
    public required Action<object> SourceBitRemoved { get; init; }
}
