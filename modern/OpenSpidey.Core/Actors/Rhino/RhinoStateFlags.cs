namespace OpenSpidey.Core.Actors.Rhino;

/// <summary>The 13 (state, flags) pairs at PC VA 0x5521D0, with CBaddy::CheckStateFlags caching.</summary>
public sealed class RhinoStateFlags
{
    private static readonly (ushort State, ushort Flags)[] Entries =
    [
        (21, 0x03), (14, 0x41), (12, 0x39), (18, 0x41), (7, 0x04), (8, 0x3C),
        (4, 0x04), (15, 0x44), (1, 0x40), (2, 0x40), (16, 0x40), (17, 0x40), (6, 0x08)
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
