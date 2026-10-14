namespace OpenSpidey.Core.Actors.LizMan;

/// <summary>CLizMan::HandleAnimationFollowOn (0x450B50) and the shared follow-on word at 0x5FBC0C.</summary>
public static class LizManAnimationFollowOn
{
    public static ushort PendingAnimation { get; set; } = 0xFFFF;

    public static int Resolve(bool animationFinished, int finishedAnimationId)
    {
        PendingAnimation = 0xFFFF;
        if (!animationFinished) return -1;
        PendingAnimation = unchecked((ushort)finishedAnimationId);
        int next = finishedAnimationId switch
        {
            0 or 2 or 8 => finishedAnimationId,
            1 => 2,
            3 or 6 => 0,
            4 or 5 or 9 or 10 or 11 or 12 or 13 or 16 or 17 or 28 or 32 or 33 or 34 or 39 => 5,
            7 => 8,
            14 => 16,
            18 or 19 => 19,
            20 or 21 => 21,
            22 or 25 => 25,
            23 or 24 => 24,
            29 => 30,
            30 or 31 => 31,
            35 => 35,
            37 or 38 => 38,
            _ => -1
        };
        if (next < 0) PendingAnimation = 0xFFFF;
        return next;
    }
}
