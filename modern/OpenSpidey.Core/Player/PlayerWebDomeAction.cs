using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public interface IPlayerWebDomeActionHost
{
    uint Clock { get; }
    ushort MinorRadius { set; }
    void PlayAnimation(ushort animation, int firstFrame, int lastFrame);
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitch);
    void SwitchToStandMode();
    bool BurstDome(ref PlayerKickHandle handle);
}

/// <summary>Actual action20000000: source dome timer/buttons, recoverable owner and release-radius restoration.</summary>
public static class PlayerWebDomeAction
{
    public static void Step(PlayerMeleeState melee, PlayerWebAttackState state, ushort completedAnimation,
        IPlayerWebDomeActionHost host)
    {
        host.MinorRadius = 150;
        if (completedAnimation == 0x11C)
        { host.SwitchToStandMode(); host.MinorRadius = 100; return; }
        if (melee.Combat.AnimationId == 0x11C) return;
        if (unchecked(host.Clock - (uint)state.DomeCreationClock) <= 150 &&
            melee.Input.Pressed[18] == 0 && melee.Input.Pressed[19] == 0 && melee.Input.Pressed[16] == 0) return;
        host.PlayAnimation(0x11C, 0, -1);
        host.PlayPositionSound(0x23, melee.Combat.Position, 0);
        PlayerKickHandle handle = state.DomeHandle;
        bool burst = host.BurstDome(ref handle);
        state.DomeHandle = burst ? default : handle;
    }
}
