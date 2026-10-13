namespace OpenSpidey.Core.Player;

/// <summary>Actual SetupLookaroundCamera transition counters, heading compensation, head/neck angles and raw/smoothed globals.</summary>
public static class PlayerLookaroundAngles
{
    public static bool Advance(PlayerLookaroundState state, short effectiveHeading, bool ceiling, int elapsedTicks)
    {
        if (!state.Active) return false;
        if (state.EntranceTicks > elapsedTicks)
        {
            state.EntranceTicks = unchecked(state.EntranceTicks - elapsedTicks);
            state.HeadingTurnTicksRemaining = 0;
            state.TransitionFade = unchecked(32 * (24 - state.EntranceTicks)) / 24;
        }
        else
        {
            state.EntranceTicks = 0;
            if (state.ExitTicks > elapsedTicks)
            {
                state.ExitTicks = unchecked(state.ExitTicks - elapsedTicks);
                // PC4C3958 rejoins the common fade calculation with ESI still holding exit remaining.
                state.TransitionFade = unchecked(32 * state.ExitTicks) / 24;
            }
            else if (state.ExitTicks != 0)
            {
                state.ExitTicks = 0; state.Active = false; state.TransitionFade = 0;
                state.NeckPitch = state.NeckYaw = state.HeadPitch = state.HeadYaw = 0;
                return false;
            }
        }
        short delta = unchecked((short)(effectiveHeading - state.PreviousHeading));
        if (delta > 512) delta = unchecked((short)(delta - 4096));
        else if (delta < -512) delta = unchecked((short)(delta + 4096));
        state.PreviousHeading = effectiveHeading;
        if (state.EntranceTicks == 0)
        {
            int compensation = ceiling ? delta : -delta;
            state.Yaw = unchecked(state.Yaw + compensation);
            state.SmoothedYaw = unchecked(state.SmoothedYaw + compensation);
        }
        if (state.ExitTicks != 0)
        {
            state.NeckPitch = Decay(state.NeckPitch); state.NeckYaw = Decay(state.NeckYaw);
            state.HeadPitch = Decay(state.HeadPitch); state.HeadYaw = Decay(state.HeadYaw);
        }
        else
        {
            state.NeckPitch = state.HeadPitch = unchecked((short)((unchecked(2 * state.Pitch) / 4) & 4095));
            state.NeckYaw = state.HeadYaw = unchecked((short)((unchecked(2 * state.Yaw) / 4) & 4095));
        }
        state.SmoothedPitch = RebaseWithin192(state.Pitch, state.SmoothedPitch);
        state.SmoothedYaw = RebaseWithin192(state.Yaw, state.SmoothedYaw);
        return true;
    }

    public static short WebShotHeading(PlayerLookaroundState state, short effectiveHeading, bool ceiling) =>
        unchecked((short)(ceiling ? effectiveHeading - state.Yaw : effectiveHeading + state.Yaw));
    private static short Decay(short angle) => unchecked((short)(angle - angle / 4));
    private static int RebaseWithin192(int target, int previous)
    {
        if (target > unchecked(previous + 192)) return unchecked(target - 192);
        if (target < unchecked(previous - 192)) return unchecked(target + 192);
        return previous;
    }
}
