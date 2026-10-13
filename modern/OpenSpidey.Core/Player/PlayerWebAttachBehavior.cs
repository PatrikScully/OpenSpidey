using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Actors;

namespace OpenSpidey.Core.Player;

/// <summary>Arguments passed to the original CPlayer::FireWeb boundary.</summary>
public readonly record struct PlayerFireWebRequest(bool UseHeldTarget, int Cost, FixedVector3 Target,
    bool HitSomething, ActorAngles Normal);

public interface IPlayerWebAttachHost
{
    bool AnimationFinished { get; }
    bool HasExplicitAim { get; }
    FixedVector3 ExplicitAimPosition { get; }
    ActorAngles TrajectoryAngles { get; }
    bool CheckJump();
    int FireWeb(PlayerFireWebRequest request);
    void CreateWebDrips(bool firstHand, bool secondHand);
    void PlayAnimation(ushort animation, int firstFrame, int lastFrame);
    void SwitchToStandMode();
}

/// <summary>Original action 0x10000: shot release frames, drips and recovery animations.</summary>
public static class PlayerWebAttachBehavior
{
    public static void Step(PlayerMeleeState state, IPlayerWebAttachHost host)
    {
        PlayerCombatState combat = state.Combat;
        ushort animation = combat.AnimationId;
        combat.FieldEA6 = 0;
        if (animation == 0x8B)
        {
            if (host.AnimationFinished) { host.SwitchToStandMode(); return; }
            if (combat.AnimationFrame < 3) return;
            if (state.WebAttackSubstate == 0)
            {
                state.WebAttackSubstate = 1;
                Fire(host);
            }
            else if (host.CheckJump()) return;
            if (combat.AnimationFrame <= 5) host.CreateWebDrips(true, false);
            return;
        }
        if (animation is 0xFF or 0x109)
        {
            if (combat.AnimationFrame < 5) return;
            if (state.WebAttackSubstate == 0)
            {
                state.WebAttackSubstate = 1;
                Fire(host);
            }
            if (combat.AnimationFrame <= 11) host.CreateWebDrips(true, true);
            if (host.AnimationFinished) host.PlayAnimation(animation == 0xFF ? (ushort)0x100 : (ushort)0x10A, 0, -1);
            return;
        }
        if (animation is 0x100 or 0x10A)
        {
            if (host.CheckJump()) return;
            if (host.AnimationFinished) host.SwitchToStandMode();
        }
    }

    private static void Fire(IPlayerWebAttachHost host) => host.FireWeb(new(!host.HasExplicitAim, 0x384,
        host.HasExplicitAim ? host.ExplicitAimPosition : default, false, host.TrajectoryAngles));
}
