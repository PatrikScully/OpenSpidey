namespace OpenSpidey.Core.Player;

/// <summary>Actual airborne action fields, retaining the shared jump impulse and player combat owner.</summary>
public sealed class PlayerAirborneMovementState(PlayerMeleeState melee)
{
    public PlayerMeleeState Melee { get; } = melee;
    public PlayerJumpImpulse Jump { get; set; }
    public int FallReferenceHeight { get => Melee.Combat.FallReferenceHeight; set => Melee.Combat.FallReferenceHeight = value; }
    public int FallReferenceTick { get => Melee.Combat.FallReferenceTick; set => Melee.Combat.FallReferenceTick = value; }
    public int FallDamageStartsAt { get => Melee.Combat.FallDamageStartsAt; set => Melee.Combat.FallDamageStartsAt = value; }
    public int FallDamageFullHealthAt { get => Melee.Combat.FallDamageFullHealthAt; set => Melee.Combat.FallDamageFullHealthAt = value; }
}

public interface IPlayerAirborneMovementHost
{
    uint Clock { get; }
    int Difficulty { get; }
    int LevelId { get; }
    bool AnimationFinished { get; }
    bool CheckLanded();
    bool CheckStickToWall();
    bool CheckStickToCeiling();
    bool CheckJumpingSmashKick();
    bool CheckJumpingSwingWeb();
    bool CheckJumpingR1ZipWeb();
    bool CheckJumpingR2ZipWeb();
    bool ShouldPlayerDropFlail();
    void PlayAnimation(ushort animation);
    void SwitchToDeathMode();
    void SetLevelStatus(int status);
}

public interface IPlayerAirborneMovementFrameHost
{
    void UpdateJumping();
    void UpdateFalling(int verticalVelocityBeforePhysics);
}

/// <summary>Complete source action2 jumping and4 falling, in original guard and animation order.</summary>
public static class PlayerAirborneMovement
{
    public static void UpdateJumping(PlayerAirborneMovementState state, IPlayerAirborneMovementHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        combat.FieldEA6 = 0;
        if (host.CheckStickToCeiling() || host.CheckJumpingSmashKick() || host.CheckJumpingSwingWeb() ||
            host.CheckJumpingR1ZipWeb() || host.CheckJumpingR2ZipWeb()) return;
        if (host.AnimationFinished)
        {
            if (combat.AnimationId == 231) host.PlayAnimation(232);
            else if (combat.AnimationId == 215) host.PlayAnimation(216);
        }
        if (state.Melee.Input.Pressed[16] == 0 && (host.Difficulty != 0 || host.LevelId != 0x705))
            state.Jump = state.Jump with { Hold = 0 };
        if ((combat.LastHitAction & 2) != 0 && unchecked(host.Clock - combat.LastHitClock) < 6)
        {
            if (!host.ShouldPlayerDropFlail()) return;
            host.PlayAnimation(175);
            combat.Velocity = combat.Velocity with { X = 0, Z = 0 };
            combat.ActionState = 0x800000;
            combat.FlailDropChain = 0;
            return;
        }
        if (state.Jump.Phase <= 0 && state.Jump.Hold <= 0) combat.ActionState = 4;
    }

    public static void UpdateFalling(PlayerAirborneMovementState state, int verticalVelocityBeforePhysics, IPlayerAirborneMovementHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        combat.FieldEA6 = 0;
        if (combat.Health <= 0)
        {
            if (host.CheckLanded()) host.SwitchToDeathMode();
            return;
        }
        if (host.CheckLanded() || host.CheckStickToWall() || host.CheckStickToCeiling() || host.CheckJumpingSmashKick() ||
            host.CheckJumpingSwingWeb() || host.CheckJumpingR1ZipWeb() || host.CheckJumpingR2ZipWeb()) return;
        if (combat.AirborneTicks > 480) host.SetLevelStatus(8);
        if (host.AnimationFinished && combat.AnimationId == 215) host.PlayAnimation(216);
        if (host.AnimationFinished && combat.AnimationId == 127) host.PlayAnimation(216);
        if ((combat.LastHitAction & 4) != 0 && unchecked(host.Clock - combat.LastHitClock) < 6 && host.ShouldPlayerDropFlail())
        {
            host.PlayAnimation(175);
            combat.ActionState = 0x800000;
            combat.FlailDropChain = 0;
        }
        PlayerFallAnimationPlan plan = PlayerFallAnimations.Resolve(combat.AnimationId, verticalVelocityBeforePhysics,
            combat.Velocity.Y, combat.PendingWebReleaseAnimation != 0);
        if (plan.ResetReference)
        {
            state.FallReferenceHeight = combat.Position.Y;
            state.FallReferenceTick = unchecked((int)host.Clock);
            if (plan.Next is { } clip) host.PlayAnimation(clip);
            if (plan.ClearWebRelease) combat.PendingWebReleaseAnimation = 0;
        }
        if (combat.FieldAE5 != 0 && unchecked(host.Clock - (uint)state.FallReferenceTick) > 30)
            combat.FieldAE5 = combat.FieldAE6 = 0;
    }
}
