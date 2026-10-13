using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerSwingProbeContact(FixedVector3 Position, FixedVector3 Normal, uint FaceFlags, int Distance);

/// <summary>The source fan cursors belong to the player; targets and swing geometry retain their shared owners.</summary>
public sealed class PlayerSwingSelectionState(PlayerMeleeState melee, PlayerSwingWebAvailabilityState availability,
    PlayerLookaroundTargetState targets)
{
    public PlayerMeleeState Melee { get; } = melee;
    public PlayerSwingWebAvailabilityState Availability { get; } = availability;
    public PlayerLookaroundTargetState Targets { get; } = targets;
    public int WideFanCursor { get; set; } // 5CC.
    public int NarrowFanCursor { get; set; } // 5C8.
}

public interface IPlayerSwingSelectionHost
{
    bool PracticeMode { get; }
    byte AirborneReleaseLatch { get; }
    byte SwingHeldLatch { get; }
    PlayerSwingProbeContact? TraceSwingWorld(FixedVector3 start, FixedVector3 end);
    bool CheckSwingAvailability(PlayerSwingProbeContact contact);
    void PlayAnimation(ushort clip);
    void SetSwingCameraResetLatch(byte value);
}

/// <summary>Actual CheckJumpingSwingWeb4C18A0, including source ordered fans and the final rejected straightening probe's position.</summary>
public static class PlayerSwingSelection
{
    private static readonly short[] WideAngles = [-171, 171, -341, 341, -512, 512];
    private static readonly int[] NarrowAngles = [57, -57, 114, -114, 171, -171];

    public static bool Check(PlayerSwingSelectionState state, IPlayerSwingSelectionHost host)
    {
        PlayerMeleeState melee = state.Melee;
        PlayerCombatState combat = melee.Combat;
        if (!ButtonPermits(melee, host.PracticeMode, host.AirborneReleaseLatch) || combat.LookaroundActive ||
            combat.HeldObjectPresent || host.SwingHeldLatch != 0) return false;
        FixedVector3 start = combat.Position with { Y = unchecked(combat.Position.Y - 0x40000) };
        FixedVector3 lastPosition = default;
        PlayerSwingProbeContact? first = Trace(combat.Position - combat.ForwardDirection * 4096);
        bool found = first is { } direct && !Restricted(direct) && host.CheckSwingAvailability(direct);
        if ((combat.ActionState & 0x40006) != 0 && !found)
        {
            short angle = WideAngles[state.WideFanCursor];
            state.WideFanCursor = unchecked(state.WideFanCursor + 1);
            if (state.WideFanCursor > 5) state.WideFanCursor = 0;
            PlayerSwingProbeContact? wide = Trace(FanEnd(combat.Position, combat.UpDirection, combat.ForwardDirection, angle));
            if (wide is { } wideHit && !Restricted(wideHit) && host.CheckSwingAvailability(wideHit))
            {
                while (angle <= -57)
                {
                    angle = unchecked((short)(angle + 57));
                    PlayerSwingProbeContact? straighter = Trace(FanEnd(combat.Position, combat.UpDirection, combat.ForwardDirection, angle));
                    if (straighter is not { } hit || Restricted(hit) || !host.CheckSwingAvailability(hit)) break;
                }
                found = true;
            }
            else
            {
                int narrowAngle = NarrowAngles[state.NarrowFanCursor];
                state.NarrowFanCursor = unchecked(state.NarrowFanCursor + 1);
                if (state.NarrowFanCursor > 5) state.NarrowFanCursor = 0;
                PlayerSwingProbeContact? narrow = Trace(FanEnd(combat.Position, melee.RightDirection, combat.ForwardDirection, narrowAngle));
                if (narrow is not { } hit || Restricted(hit)) return false;
                found = host.CheckSwingAvailability(hit);
            }
        }
        if (!found) return false;
        // print_if_false(E64==null) is nonfatal in the original. The actual latest
        // SLineInfo position is written even when straightening ended in rejection.
        state.Targets.TargetPosition = lastPosition;
        state.Availability.TargetPosition = lastPosition;
        state.Targets.SwingFacingTarget = lastPosition;
        melee.WebAttackStarted = 0;
        combat.IsCrawling = false;
        combat.HeadingTurnTicksRemaining = 0;
        if ((combat.ActionState & 9) != 0)
        {
            combat.ActionState = 0x100;
            host.PlayAnimation(273);
        }
        else
        {
            combat.Field54C = 1;
            combat.ActionState = 0x200;
            combat.ActionProgress = 0;
            host.PlayAnimation(282);
            if (combat.Velocity.Y > 0) combat.Velocity = combat.Velocity with { Y = 0 };
        }
        host.SetSwingCameraResetLatch(1);
        melee.Input.Triggered[4] = 1;
        return true;

        PlayerSwingProbeContact? Trace(FixedVector3 end)
        {
            PlayerSwingProbeContact? contact = host.TraceSwingWorld(start, end);
            if (contact is { } hit) lastPosition = hit.Position;
            return contact;
        }
    }

    public static bool ButtonPermits(PlayerMeleeState melee, bool practiceMode, byte airborneReleaseLatch) =>
        melee.Input.Pressed[7] != 0 && (!practiceMode || melee.Combat.SynthesizedInputActive) ||
        practiceMode && (melee.Combat.ActionState & 6) != 0 && airborneReleaseLatch != 0 && melee.Input.Pressed[16] != 0;

    private static bool Restricted(PlayerSwingProbeContact hit) => (hit.FaceFlags & 0x40000) != 0 && hit.Normal.Y >= -2600;

    private static FixedVector3 FanEnd(FixedVector3 position, FixedVector3 side, FixedVector3 forward, int angle)
    {
        (short sine, short cosine) = GameAngle.Heading(angle);
        int Axis(int a, int b) => unchecked((unchecked(a * sine) >> 12) - (unchecked(b * cosine) >> 12));
        return position + new FixedVector3(Axis(side.X, forward.X) << 12,
            unchecked(Axis(side.Y, forward.Y) - 64) << 12, Axis(side.Z, forward.Z) << 12);
    }
}
