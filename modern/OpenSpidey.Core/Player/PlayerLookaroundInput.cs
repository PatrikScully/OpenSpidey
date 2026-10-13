namespace OpenSpidey.Core.Player;

/// <summary>Raw source SControl fields after ReadAnalogueInput, before player action selection.</summary>
public readonly record struct PlayerLookaroundButtons(byte LookHeld, byte SwingHeld, byte SwingTriggered,
    byte JumpHeld, byte JumpTriggered);

public readonly record struct PlayerLookaroundInputFrame(PlayerLookaroundButtons Buttons, int AnalogRamp,
    byte GroundContactGraceUpdates, ushort CameraMode, bool PracticeMode, bool InvertPitch, bool SwingWebPresent);

public interface IPlayerLookaroundInputHost
{
    short EffectiveHeading { get; }
    void EnterLookaroundMode();
    void ExitLookaroundMode();
    void SetTargetTorsoAngle(short wanted, bool fast);
}

/// <summary>Actual post-analogue lookaround input block (PC4B1CFE..4B1FCD), sharing the web latch and original button slots.</summary>
public static class PlayerLookaroundInput
{
    public static PlayerLookaroundButtons Step(PlayerLookaroundState state, PlayerWebAttackState web,
        PlayerKickState kick, PlayerLookaroundInputFrame frame, IPlayerLookaroundInputHost host)
    {
        PlayerMeleeState melee = kick.Melee;
        int pitchInput = melee.Input.Sideways; // SourceE2D: physical forward/backward.
        int yawInput = melee.Input.Forward; // SourceE2E: physical sideways.
        PlayerLookaroundButtons buttons = frame.Buttons;
        if (buttons.JumpHeld == 0) state.JumpReleased = 1;
        if ((web.BlockedVariants & 1) != 0 && pitchInput <= 0) web.BlockedVariants &= ~1u;
        if ((web.BlockedVariants & 2) != 0 && pitchInput >= 0) web.BlockedVariants &= ~2u;
        if ((web.BlockedVariants & 4) != 0 && yawInput <= 0) web.BlockedVariants &= ~4u;
        if ((web.BlockedVariants & 8) != 0 && yawInput >= 0) web.BlockedVariants &= ~8u;
        if (state.SwingHeldLatch != 0 && buttons.SwingHeld == 0) state.SwingHeldLatch = 0;
        uint action = melee.Combat.ActionState;
        if ((action & 0x3EBE3FCE) != 0 || ((action & 0x1C000) != 0 ? !state.Active :
            kick.HeldObject is not null || frame.GroundContactGraceUpdates != 4))
            buttons = buttons with { LookHeld = 0 };

        state.HoldTicks = buttons.LookHeld != 0 && state.Allowed != 0
            ? unchecked(state.HoldTicks + melee.Combat.ElapsedTicks) : 0;
        if (unchecked((uint)state.HoldTicks) > 15)
        {
            if (!state.Active)
            {
                if (frame.CameraMode is not (0 or 4 or 5 or 6)) host.EnterLookaroundMode();
            }
            else if (buttons.LookHeld == 0) host.ExitLookaroundMode();
        }
        else if (state.Active && buttons.LookHeld == 0) host.ExitLookaroundMode();

        if (!state.Active || (state.EntranceTicks | state.ExitTicks) != 0)
        {
            state.Yaw = state.SmoothedPitch = state.SmoothedYaw = 0;
            return buttons;
        }
        if ((frame.PracticeMode ? buttons.JumpTriggered : buttons.SwingTriggered) != 0 && !frame.SwingWebPresent)
        {
            buttons = buttons with { JumpTriggered = 0, SwingTriggered = 0 };
            state.ActivateTarget = 1;
            melee.Input.Triggered[16] = melee.Input.Mirrors[16] = melee.Input.Triggered[7] = melee.Input.Mirrors[7] = 0;
        }
        if (pitchInput != 0 && web.ButtonLatched == 0)
        {
            int delta = unchecked((pitchInput >> 3) * frame.AnalogRamp * melee.Combat.ElapsedTicks) >> 8;
            int previous = state.Pitch;
            int next = frame.InvertPitch ? unchecked(previous - delta) : unchecked(previous + delta);
            if (SourceAbs(next) <= 1024) state.Pitch = next;
        }
        if (yawInput != 0 && web.ButtonLatched == 0)
        {
            int delta = unchecked((yawInput >> 3) * frame.AnalogRamp * melee.Combat.ElapsedTicks) >> 8;
            int next = state.Yaw = unchecked(state.Yaw + delta);
            if (SourceAbs(next) > 1024)
            {
                int excess = next > 1024 ? unchecked(next - 1024) : unchecked(next + 1024);
                short heading = host.EffectiveHeading;
                host.SetTargetTorsoAngle(unchecked((short)((melee.Combat.Field8E9 != 0 ? heading - excess : heading + excess) & 4095)), false);
            }
        }
        return buttons;
    }
    private static int SourceAbs(int value) => value < 0 ? unchecked(-value) : value;
}
