using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Smash-attack entry fields shared with its subsequent source action1000000.</summary>
public sealed class PlayerSmashAttackState(PlayerMeleeState melee)
{
    public PlayerMeleeState Melee { get; } = melee;
    public byte Field54D { get => Melee.Combat.PendingWebReleaseAnimation; set => Melee.Combat.PendingWebReleaseAnimation = value; }
    public FixedVector3 TargetPosition { get; set; } // Source8CC.
    public int EntryClock { get; set; } // Source8C4.
    public int PreviousEntryClock { get; set; } // Source8C8.
    public byte Field8D8 { get; set; }
    public byte FieldAE4 { get => Melee.Combat.FieldAE4; set => Melee.Combat.FieldAE4 = value; }
    public byte FieldE8C { get => Melee.Combat.FieldE8C; set => Melee.Combat.FieldE8C = value; }
}

public interface IPlayerSmashAttackSelectionHost
{
    uint Clock { get; }
    bool SwingPresent { get; }
    void SetCameraFocusHook(int hook);
    void ReleaseSwing(bool createSwingBack);
    void SetTargetTorsoAngle(FixedVector3 point);
    void SelectAndRunAnimation(ushort animation);
    void CreateSmashAttackTrails();
}

public interface IPlayerCeilingSmashSelectionHost
{
    uint Clock { get; }
    void OrientToNormal(bool useTarget, FixedVector3 target);
    void SetTargetTorsoAngle(FixedVector3 point);
    void SelectAndRunAnimation(ushort animation);
}

/// <summary>Actual CheckJumpingSmashKick4C1EB0, preserving untouched input edges and the source current-swingE64 lifetime.</summary>
public static class PlayerAirborneAttackSelection
{
    /// <summary>Actual4C0B80 ceiling/wall smash entry, retaining attack edges and deferring foot-trail creation to its action driver.</summary>
    public static bool CheckCeilingSmash(PlayerSmashAttackState state, IPlayerCeilingSmashSelectionHost host)
    {
        PlayerMeleeState melee = state.Melee;
        PlayerCombatState combat = melee.Combat;
        if (combat.LookaroundActive || (combat.Field8E9 == 0 && combat.Field8E8 == 0) || melee.AimTarget is null ||
            (melee.Input.Triggered[18] == 0 && melee.Input.Triggered[19] == 0)) return false;
        combat.FieldAE4 = 0;
        combat.CrawlSurfaceNormal = new(0, -4096, 0);
        host.OrientToNormal(false, default);
        combat.IsCrawling = false;
        combat.Field8DC = 0;
        state.TargetPosition = melee.AimTarget.Position;
        host.SetTargetTorsoAngle(state.TargetPosition);
        combat.ActionState = 0x1000000;
        state.PreviousEntryClock = state.EntryClock;
        state.EntryClock = unchecked((int)host.Clock);
        state.Field8D8 = 0;
        host.SelectAndRunAnimation(melee.Input.Triggered[18] != 0 ? (ushort)133 : (ushort)129);
        return true;
    }

    public static bool CheckSmash(PlayerSmashAttackState state, IPlayerSmashAttackSelectionHost host)
    {
        PlayerMeleeState melee = state.Melee;
        PlayerCombatState combat = melee.Combat;
        IPlayerMeleeTarget? target = melee.AimTarget;
        if (target is null || (melee.Input.Triggered[19] == 0 && melee.Input.Triggered[18] == 0) || combat.LookaroundActive ||
            target.Type is 412 or 329) return false;
        FixedVector3 direction = FixedVectorMath.Normalize((target.Position - combat.Position) >> 12);
        if (direction.Y <= 1024) return false;
        combat.Field54C = 0;
        host.SetCameraFocusHook(-1);
        state.Field54D = 0;
        if (host.SwingPresent) host.ReleaseSwing(true);
        combat.Field8DC = 0;
        state.TargetPosition = target.Position;
        host.SetTargetTorsoAngle(state.TargetPosition);
        combat.ActionState = 0x1000000;
        state.PreviousEntryClock = state.EntryClock;
        state.EntryClock = unchecked((int)host.Clock);
        state.Field8D8 = 0;
        host.SelectAndRunAnimation(melee.Input.Triggered[18] != 0 ? (ushort)133 : (ushort)129);
        host.CreateSmashAttackTrails();
        return true;
    }
}
