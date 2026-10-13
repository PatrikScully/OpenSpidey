namespace OpenSpidey.Core.Actors.Thugs;

/// <summary>Recovered SetAnimMode tables (PC 0x552178/0x552198) and shared RunAppropriateAnim.</summary>
public static class ThugAnimationBehavior
{
    private static readonly (uint Movement, uint Idle)[] ThugModes =
        [(0x0D0D0100, 3), (0x14141C1B, 0x114), (0x06061C1B, 0x204), (0x14141C1B, 0x314)];
    private static readonly (uint Movement, uint Idle)[] HenchmanModes =
        [(0x06040504, 0), (0x17171817, 0x101), (0x17171817, 0x201), (0x01010B0A, 0x301)];

    public static bool SetMode(ThugState state, int mode, bool playAnimation, IThugGuardHost host)
    {
        int oldMode = (byte)(state.AnimationWord298 >> 8);
        if (oldMode == mode) return false;
        if ((uint)mode >= 4) throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown thug animation mode.");
        int transition = -1;
        if (state.Type == 304)
        {
            transition = (mode, oldMode) switch
            {
                (0, 1) => 0, (0, 2) => 27, (2, 1) => 8, (2, 3) => 21, (3, 2) => 19, _ => -1
            };
        }
        else if (mode == 0 && oldMode is 1 or 2) transition = 3;
        (uint movement, uint idle) = (state.Type == 304 ? ThugModes : HenchmanModes)[mode];
        state.AnimationWord294 = movement;
        state.AnimationWord298 = idle;
        if (transition == -1) return false;
        if (playAnimation) host.RunAnimation((ushort)transition);
        return true;
    }

    public static void RunAppropriate(ThugState state, uint movementFlags, IThugGuardHost host)
    {
        ushort first = (byte)state.AnimationWord294;
        ushort second = (byte)(state.AnimationWord294 >> 8);
        if ((movementFlags & 0x40000) != 0)
        {
            if (first != second && host.AnimationId != second && host.AnimationId != first)
            {
                host.RunAnimation(first);
                return;
            }
            if (first == second || host.AnimationId != first || host.AnimationFinished != 0)
                host.CycleAnimation(second);
        }
        else if ((movementFlags & 0x10000) != 0)
        {
            ushort clip = (byte)(state.AnimationWord294 >> 16);
            if (host.AnimationId != clip || host.AnimationFinished != 0) host.RunAnimation(clip);
        }
        else if ((movementFlags & 0x20000) != 0)
        {
            ushort clip = (byte)(state.AnimationWord294 >> 24);
            if (host.AnimationId != clip || host.AnimationFinished != 0) host.RunAnimation(clip);
        }
        else host.CycleAnimation(state.IdleAnimation);
    }
}
