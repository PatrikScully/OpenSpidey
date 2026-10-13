using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Player;

/// <summary>The actual current CPlayer's source152 request uses its authored level and existing application exit callback.</summary>
public sealed class GodotPlayerScriptDeath : IPlayerScriptDeathHost
{
    private readonly GodotPlayer player;
    private readonly GodotActorRegistry registry;
    private readonly Action? trainingDeath;
    public int LevelId => registry.Environment.LevelId;
    public GodotPlayerScriptDeath(GodotPlayer player, GodotActorRegistry registry, Action? trainingDeath = null)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        if (!ReferenceEquals(registry.Player, player))
            throw new InvalidOperationException("Source152 must resolve the actual current registry player.");
        this.trainingDeath = trainingDeath;
    }
    public void SetLevelStatus(int value) => player.Combat.SetLevelStatus(value);
    public void CallTrainingDeathModule() => (trainingDeath ??
        throw new NotSupportedException("Source training death requires the genuine training module user function1 with null arguments/result."))();
}
