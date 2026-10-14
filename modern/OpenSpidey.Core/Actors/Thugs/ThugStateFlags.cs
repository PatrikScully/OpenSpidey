namespace OpenSpidey.Core.Actors.Thugs;

/// <summary>The 17 (state, flags) pairs at PC VA 0x557CA0, with CBaddy::CheckStateFlags caching.</summary>
public sealed class ThugStateFlags
{
    private static readonly (short State, ushort Flags)[] ThugEntries =
    [
        (2, 96), (23, 64), (24, 64), (1, 96), (26, 11), (27, 11), (22, 27), (18, 1),
        (14, 7), (15, 7), (12, 1), (17, 7), (19, 3), (20, 3), (13, 5), (25, 96), (0, 19)
    ];
    private readonly (short State, ushort Flags)[] Entries;

    public ThugStateFlags() : this(ThugEntries) { }

    /// <summary>Another class's CheckStateFlags table, such as CCop's at 0x549220.</summary>
    public ThugStateFlags((short State, ushort Flags)[] entries) => Entries = entries;

    public int CachedIndex { get; private set; }

    public ushort Check(int state)
    {
        if (CachedIndex < 0)
        {
            if (state == -CachedIndex) return 0;
        }
        else if (state == Entries[CachedIndex].State)
            return Entries[CachedIndex].Flags;

        for (int i = 0; i < Entries.Length; i++)
        {
            if (state != unchecked((ushort)Entries[i].State)) continue;
            CachedIndex = i;
            return Entries[i].Flags;
        }
        CachedIndex = -unchecked((short)state);
        return 0;
    }
}
