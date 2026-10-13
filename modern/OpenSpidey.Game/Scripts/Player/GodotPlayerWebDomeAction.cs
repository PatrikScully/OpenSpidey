using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.Player;

/// <summary>Actual dome release action, using the same AB8/374 state and genuine Misc-list dome identity.</summary>
public sealed class GodotPlayerWebDomeAction(GodotPlayer player, GodotPlayerWebAttack attack, GodotWebDomes domes) : IPlayerWebDomeActionHost
{
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Dome action requires its actual melee/input owner.");
    public uint Clock => attack.Clock;
    public ushort MinorRadius { set => player.ActorState.MinorRadius = value; }
    public void Update(ushort completedAnimation) => PlayerWebDomeAction.Step(Melee.State, attack.State, completedAnimation, this);
    public void PlayAnimation(ushort animation, int firstFrame, int lastFrame) => Melee.PlaySingleAnimation(animation, firstFrame, lastFrame);
    public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => Melee.PlayPositionSound(sound, position, pitch);
    public void SwitchToStandMode() => Melee.SwitchToStandMode();
    public bool BurstDome(ref PlayerKickHandle handle) => domes.Burst(ref handle);
}
