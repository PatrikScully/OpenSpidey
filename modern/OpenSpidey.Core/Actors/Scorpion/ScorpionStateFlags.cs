namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>The ten original state/flag pairs at PC VA 0x5523A8 and CBaddy's signed-short cache.</summary>
public sealed class ScorpionStateFlags
{
    private static readonly (ushort State, ushort Flags)[] Entries =
    [
        (21, 3), (9, 1), (12, 1), (3, 36), (15, 8),
        (19, 4), (2, 48), (18, 48), (16, 48), (17, 48)
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
