namespace OpenSpidey.Core.Actors.LizMan;

/// <summary>The 19 (state, flags) pairs at PC VA 0x54B688, with CBaddy::CheckStateFlags caching.</summary>
public sealed class LizManStateFlags
{
    private static readonly (ushort State, ushort Flags)[] Entries =
    [
        (20, 0xAB), (21, 0x2B), (18, 0x3B), (14, 0x121), (10, 0x27), (11, 0x27),
        (8, 0x121), (13, 0x27), (15, 0x123), (16, 0x23), (9, 0x125), (24, 0x40),
        (19, 0x40), (3, 0x80), (4, 0x100), (2, 0x80), (1, 0x100), (7, 0x100), (0, 0x52)
    ];

    public short CachedIndex { get; private set; }

    public ushort Check(int state)
    {
        if (CachedIndex < 0)
        {
            if (state == -CachedIndex) return 0;
        }
        else if (state == Entries[CachedIndex].State)
            return Entries[CachedIndex].Flags;

        for (int index = 0; index < Entries.Length; index++)
        {
            if (state != Entries[index].State) continue;
            CachedIndex = unchecked((short)index);
            return Entries[index].Flags;
        }
        CachedIndex = unchecked((short)-state);
        return 0;
    }
}
