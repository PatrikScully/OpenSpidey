namespace OpenSpidey.Core.Player;

/// <summary>CPlayer constructor and CreateFists: the renderer masks model parts after its temporary pose swaps.</summary>
public static class PlayerHandParts
{
    public const uint InitialHiddenMask=0x840;
    public static uint Select(uint hiddenParts,byte variant)
    {
        hiddenParts=(variant&1)!=0?(hiddenParts&~0x20u)|0x40u:(hiddenParts&~0x40u)|0x20u;
        return (variant&2)!=0?(hiddenParts&~0x400u)|0x800u:(hiddenParts&~0x800u)|0x400u;
    }
}
