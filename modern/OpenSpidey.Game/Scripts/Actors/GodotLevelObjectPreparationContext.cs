using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Actual scene services that exist before the original initial actor census and CPlayer.</summary>
public sealed class GodotLevelObjectPreparationContext
{
    public required PkrArchive Archive { get; init; }
    public required PsxLibraryRepository Repository { get; init; }
    public required Func<ImportedLevelWorld> CurrentWorld { get; init; }
    public required GodotActorRegistry Registry { get; init; }
    public required GodotCombatSceneOwnership SceneOwnership { get; init; }
    public required GodotSoundChannels Sounds { get; init; }
    public required Func<SoundListener> Listener { get; init; }
    public required MissionCountdown Countdown { get; init; }
    public required ReadOnlyMemory<byte> TriggerData { get; init; }
    public required Func<uint> Clock { get; init; }
    public required GodotEnvironmentVisibility Visibility { get; init; }
}
