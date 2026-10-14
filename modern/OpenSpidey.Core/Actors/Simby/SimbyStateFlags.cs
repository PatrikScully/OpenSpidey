namespace OpenSpidey.Core.Actors.Simby;

/// <summary>The 25 (state, flags) pairs at PC VA 0x555564 (gSimbyFlags), with CBaddy::CheckStateFlags caching.</summary>
public sealed class SimbyStateFlags
{
    private static readonly (ushort State, ushort Flags)[] Entries =
    [
        (19, 0x431B), (22, 0x431B), (23, 0x431B), (10, 0x0101), (11, 0x4114),
        (6, 0x8840), (24, 0x8840), (16, 0x8880), (7, 0x001B), (5, 0x3CE0),
        (4, 0x0CE0), (2, 0x1840), (9, 0x2C80), (21, 0x38A0), (17, 0x08A0),
        (18, 0x08E0), (8, 0x0990), (20, 0x0990), (12, 0x0820), (13, 0x0830),
        (14, 0x0020), (15, 0x0020), (1, 0x001B), (25, 0x0800), (0, 0x001B)
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
