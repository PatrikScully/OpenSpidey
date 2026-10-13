using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerHeldPose(short Reach, short Yaw);
public interface IPlayerInteractionHost
{
    PlayerKickObject? RecoverManipulation(ref PlayerKickHandle handle);
    object? RecoverSwitch(ref PlayerKickHandle handle);
    void Hold(PlayerKickObject item);
    void Pickup(object identity);
    void ThrowVelocity(object identity, FixedVector3 velocity);
    void ThrowAt(object identity, FixedVector3 target, int count);
    void Release(object identity);
    void FlickSwitch(object identity);
    void SwitchToStandMode();
    void CheckJump();
    void PutCameraBehind(int mode);
    void PlaySound(ushort sound, int volume, int pitch);
    void PlayPositionalSound(ushort sound, FixedVector3 position, int pitch);
    PlayerHeldPose ReadHeldPose(object identity);
    void SetHeldPose(object identity, FixedVector3 position, short yaw);
}

/// <summary>Original pickup/throw/switch action slices and the finalizer's actual two-hand held transform.</summary>
public static class PlayerInteractionActions
{
    public static bool Update(PlayerKickState state, ushort completedAnimation, IPlayerInteractionHost host)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(host);
        switch (state.Melee.Combat.ActionState)
        {
            case 0x100000: Pickup(state, host); return true;
            case 0x200000: Throw(state, host); return true;
            case 0x80000000: Switch(state, completedAnimation, host); return true;
            default: return false;
        }
    }
    private static void Pickup(PlayerKickState state, IPlayerInteractionHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        combat.FieldEA6 = 0;
        if (state.Melee.AnimationFinished) { host.SwitchToStandMode(); return; }
        if (state.HeldObject is not null) return;
        if (combat.AnimationId == 0) { host.SwitchToStandMode(); return; }
        if (combat.AnimationId is not 190 and not 196 || combat.AnimationFrame < 10) return;
        PlayerKickHandle handle = state.ManipulationTarget;
        PlayerKickObject? item = host.RecoverManipulation(ref handle);
        state.ManipulationTarget = handle;
        if (item is null || unchecked((uint)PlayerTargetSelection.Distance(combat.Position, item.Position)) >= 768) return;
        state.HeldObject = item; combat.HeldObjectPresent = true;
        host.Hold(item); host.Pickup(item.Identity); host.PlaySound(7, 0x2000, 0);
    }
    private static void Throw(PlayerKickState state, IPlayerInteractionHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        combat.FieldEA6 = 0;
        if (state.Melee.AnimationFinished) { host.SwitchToStandMode(); return; }
        if (state.HeldObject is not { } held) { host.CheckJump(); return; }
        bool hard = combat.AnimationId == 201 && combat.AnimationFrame >= 18;
        bool throwing = hard || (combat.AnimationId == 195 && combat.AnimationFrame >= 13);
        if (combat.AnimationFrame >= 6) host.PutCameraBehind(0);
        if (!throwing) return;
        FixedVector3 velocity;
        if (state.TorsoTarget is { } target)
        {
            int distance = PlayerTargetSelection.Distance(combat.Position with { Y = target.Position.Y }, target.Position);
            if (distance >= 1024) host.ThrowAt(held.Identity, target.Position, distance / 32);
            else
            {
                int speed = unchecked(distance << 12) / (hard ? 24 : 32);
                velocity = combat.ForwardDirection * (hard ? -24 : -32);
                if (speed < 0x14000) speed = 0x14000;
                velocity = velocity with { Y = unchecked(velocity.Y + unchecked(target.Position.Y - combat.Position.Y + 0x80000) / speed - speed / 2) };
                host.ThrowVelocity(held.Identity, velocity);
            }
        }
        else
        {
            velocity = combat.ForwardDirection * (hard ? -24 : -32) + combat.UpDirection * (hard ? 14 : 16);
            host.ThrowVelocity(held.Identity, velocity);
        }
        host.PlayPositionalSound((ushort)(hard ? 38 : 37), combat.Position, 0);
        host.Release(held.Identity);
        state.HeldObject = null; combat.HeldObjectPresent = false;
    }
    private static void Switch(PlayerKickState state, ushort completedAnimation, IPlayerInteractionHost host)
    {
        if (state.SwitchActionProgress != 0)
        {
            if (completedAnimation != ushort.MaxValue) host.SwitchToStandMode();
            return;
        }
        if (state.Melee.Combat.AnimationFrame < 12) return;
        PlayerKickHandle handle = state.SwitchTarget;
        object? target = host.RecoverSwitch(ref handle);
        state.SwitchTarget = handle;
        if (target is not null) host.FlickSwitch(target);
        state.SwitchActionProgress = unchecked(state.SwitchActionProgress + 1);
    }
    /// <summary>Called after original hook1/0 evaluation and any preceding attachment modifications, once in the frame finalizer.</summary>
    public static void UpdateHeldPose(PlayerKickState state, FixedVector3 leftHand, FixedVector3 rightHand, IPlayerInteractionHost host)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(host);
        if (state.HeldObject is not { } held) return;
        FixedVector3 midpoint = leftHand + (rightHand - leftHand) / 2;
        FixedVector3 direction = FixedVectorMath.Normalize((midpoint - state.Melee.Combat.Position) >> 6);
        PlayerHeldPose pose = host.ReadHeldPose(held.Identity);
        FixedVector3 position = midpoint + direction * pose.Reach + state.Melee.Combat.ForwardDirection * 32;
        host.SetHeldPose(held.Identity, position, unchecked((short)(pose.Yaw + (short)state.Melee.HeadingDelta)));
    }
}
