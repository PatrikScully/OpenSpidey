using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public sealed record PlayerZipDiveTarget(object Identity, FixedVector3 Position, ushort Type, short Health);
public interface IPlayerZipDiveHost
{
    uint Clock { get; }
    int Difficulty { get; }
    PlayerZipDiveTarget? RecoverTarget(ref PlayerKickHandle handle);
    bool GrabTarget(PlayerZipDiveTarget target, FixedVector3 destination);
    void ClearGrabFlag(PlayerZipDiveTarget target);
    bool CheckJump();
    void CheckForwards();
    void LockTargetTorsoAngle();
    void SwitchToStandMode();
    void PlayAnimation(ushort animation);
    int GetDamage(int damage);
    void HitTarget(PlayerZipDiveTarget target, ActorHit hit);
    void CreateCombatImpact(FixedVector3 position, int kind);
    void PlayPositionalSound(ushort sound, FixedVector3 position, int pitch);
}

/// <summary>Original ZipDive, ZipDivePunch and ZipDiveKick source dispatch, including safe expired-handle exits.</summary>
public static class PlayerZipDive
{
    public static bool Update(PlayerMeleeState state, ushort completedAnimation, IPlayerZipDiveHost host)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(host);
        switch (state.Combat.ActionState)
        {
            case 0x02000000: Dive(state, completedAnimation, host); return true;
            case 0x04000000: Punch(state, completedAnimation, host); return true;
            case 0x08000000: Kick(state, completedAnimation, host); return true;
            default: return false;
        }
    }
    private static PlayerZipDiveTarget? Recover(PlayerMeleeState state, IPlayerZipDiveHost host)
    {
        PlayerKickHandle handle = state.ZipDiveHandle;
        PlayerZipDiveTarget? target = host.RecoverTarget(ref handle);
        state.ZipDiveHandle = handle; return target;
    }
    private static void Clear(PlayerMeleeState state) => state.ZipDiveHandle = state.ZipDiveHandle with { Target = null };
    private static void Dive(PlayerMeleeState state, ushort completed, IPlayerZipDiveHost host)
    {
        PlayerCombatState combat = state.Combat; combat.FieldEA6 = 0;
        if (completed is 121 or 122) { host.SwitchToStandMode(); return; }
        if (completed == 120)
        {
            PlayerZipDiveTarget? target = Recover(state, host);
            if (target is not null)
            {
                int x = unchecked(target.Position.X - combat.Position.X) >> 12;
                int z = unchecked(target.Position.Z - combat.Position.Z) >> 12;
                int facing = unchecked(combat.ForwardDirection.Z * z + x * combat.ForwardDirection.X);
                if (facing < 0 && FixedAim.Distance(combat.Position, target.Position) <= 190 &&
                    host.GrabTarget(target, combat.Position - combat.ForwardDirection * 32)) return;
            }
            host.PlayAnimation(121); Clear(state); return;
        }
        if (combat.AnimationId != 123) return;
        PlayerZipDiveTarget? held = Recover(state, host);
        // Original123 dereferences a missing recovered target. Use its existing ungrab animation to exit safely.
        if (held is null) { host.PlayAnimation(121); Clear(state); return; }
        if (host.CheckJump()) { host.ClearGrabFlag(held); Clear(state); return; }
        host.CheckForwards();
        if (state.Input.Forward == 0 && state.Input.Sideways == 0) host.LockTargetTorsoAngle();
        int holdTime = held.Type == 314 ? host.Difficulty switch { 0 => 420, 1 or 2 => 120, _ => 60 } : 300;
        if (unchecked(host.Clock - (uint)state.ZipDiveClock) > (uint)holdTime)
        {
            host.ClearGrabFlag(held); host.PlayAnimation(122); Clear(state); return;
        }
        if (state.Input.Triggered[18] != 0 && held.Type != 314)
        {
            combat.ActionState = 0x08000000; host.PlayAnimation(125); state.ZipDiveClock = unchecked((int)host.Clock); return;
        }
        if (state.Input.Triggered[19] == 0) return;
        combat.ActionState = 0x04000000; host.PlayAnimation(124);
    }
    private static void Punch(PlayerMeleeState state, ushort completed, IPlayerZipDiveHost host)
    {
        PlayerCombatState combat = state.Combat; combat.FieldEA6 = 0;
        if (completed == 124) { host.SwitchToStandMode(); return; }
        if (combat.AnimationId != 124 || combat.AnimationFrame < 16) return;
        PlayerZipDiveTarget? target = Recover(state, host); if (target is null) return;
        host.HitTarget(target, new(30, 0, 2, unchecked((ushort)host.GetDamage(70)),
            new(unchecked(-combat.ForwardDirection.X), 0, unchecked(-combat.ForwardDirection.Z)), 600, 16));
        Clear(state); combat.Field534 = 360; combat.Field52C = unchecked((combat.Field528 + 11) << 10);
        host.PlayPositionalSound(16, combat.Position, 0);
    }
    private static void Kick(PlayerMeleeState state, ushort completed, IPlayerZipDiveHost host)
    {
        PlayerCombatState combat = state.Combat;
        if (combat.AnimationId == 125) state.Input.Triggered[16] = state.Input.Mirrors[16] = 0;
        combat.FieldEA6 = 0; state.ZipDiveAirControlSuppression = 0;
        PlayerZipDiveTarget? target = Recover(state, host);
        // Source faults on an expired target here. Preserve its real release/falling branch instead.
        if (target is not null && target.Health > 0 && unchecked(host.Clock - (uint)state.ZipDiveClock) <= 300 && state.Input.Triggered[16] == 0)
        {
            if (completed == 126)
            {
                host.HitTarget(target, new(6, 0, 3, unchecked((ushort)host.GetDamage(20)), default));
                combat.Field52C = unchecked((combat.Field528 + 11) << 10); combat.Field534 = 360;
                host.CreateCombatImpact(combat.Position - combat.ForwardDirection * 32 + combat.UpDirection * 64, 0);
                host.PlayPositionalSound(16, combat.Position, 0);
            }
            else if (combat.AnimationId == 128 && state.Input.Triggered[18] != 0) host.PlayAnimation(126);
        }
        else
        {
            state.ZipDiveAirControlSuppression = 1; combat.ActionState = 4; host.PlayAnimation(127);
            combat.Velocity += new FixedVector3(unchecked(combat.ForwardDirection.X * 24), -0x40000, unchecked(combat.ForwardDirection.Z * 24));
            Clear(state);
        }
        state.Input.Triggered[18] = 0;
    }
}
