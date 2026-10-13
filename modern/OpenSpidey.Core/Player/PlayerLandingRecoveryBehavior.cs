namespace OpenSpidey.Core.Player;

/// <summary>Source action8/40 shared jump fields; input and airborne release remain owned by the actual player.</summary>
public sealed class PlayerLandingRecoveryState(PlayerMeleeState melee, PlayerLookaroundState lookaround)
{
    public PlayerMeleeState Melee { get; } = melee;
    public PlayerLookaroundState Lookaround { get; } = lookaround;
    public PlayerJumpImpulse Jump { get; set; }
    public int SpeedRamp { get; set; } // Source EBC.
}

public interface IPlayerLandingRecoveryHost
{
    int? SupportVerticalVelocity { get; }
    bool CheckGroundGone();
    bool CheckForwards(bool allowBackwards);
    bool CheckJump();
    bool CheckKick();
    bool CheckJumpingSwingWeb();
    bool CheckJumpingR1ZipWeb();
    bool CheckJumpingR2ZipWeb();
    int Random(int range);
    void PlaySingleAnimation(ushort animation);
    void SwitchToStandMode();
}

/// <summary>Actual source action8 recovery after landing, including protectedB2/B4 clips and held-jump continuation.</summary>
public static class PlayerLandingRecoveryBehavior
{
    public static void Step(PlayerLandingRecoveryState state, ushort completedAnimation, IPlayerLandingRecoveryHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        combat.FieldEA6 = 0;
        if (host.CheckGroundGone()) return;
        if (combat.AnimationId is not (178 or 180))
        {
            if (host.CheckForwards(true) || host.CheckJump() || host.CheckKick() || host.CheckJumpingSwingWeb() ||
                host.CheckJumpingR1ZipWeb() || host.CheckJumpingR2ZipWeb()) return;
        }
        combat.FieldAE4 = combat.FieldE8C != 0 ? (byte)1 : (byte)0;
        if (completedAnimation is ushort.MaxValue or 178) return;
        if (combat.FieldE8C == 0) { host.SwitchToStandMode(); return; }
        if (state.Melee.Input.Pressed[16] == 0) { combat.ActionState = 16; return; }
        state.Jump = state.Jump with { Velocity = PlayerSupportRules.JumpVelocity(host.SupportVerticalVelocity) };
        host.PlaySingleAnimation(223);
        state.Lookaround.JumpReleased = 0;
        combat.ActionState = 64;
        int phase = state.Jump.Phase;
        state.Jump = state.Jump with { Hold = unchecked((phase >> 1) + phase) };
        if ((combat.CollisionFlags & 1) != 0) state.SpeedRamp = 0;
    }

    /// <summary>Actual source action40: source210/223 windup completion recomputes impulse from current support, then enters jumping.</summary>
    public static void StepWindup(PlayerLandingRecoveryState state, bool animationFinished, IPlayerLandingRecoveryHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        combat.FieldEA6 = 0;
        if (!animationFinished) return;
        if (combat.AnimationId == 210) host.PlaySingleAnimation(211);
        else if (combat.AnimationId == 223) host.PlaySingleAnimation(host.Random(2) != 0 ? (ushort)231 : (ushort)224);
        else return;
        int velocity = host.SupportVerticalVelocity is { } support ? unchecked(support - 245760) : -245760;
        state.Jump = new(velocity, 0x70000, 0xA8000);
        combat.ActionState = 2;
    }
}
