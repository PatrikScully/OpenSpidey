namespace OpenSpidey.Core.Player;

/// <summary>Player actor identity and visibility flags, independent of damage and locomotion actions.</summary>
public sealed class PlayerActorState
{
    // CBody initializes 0x16; CPlayer clears body flag2 and selects minor radius100.
    public ushort BodyFlags { get; set; } = 0x14;
    public ushort MinorRadius { get; set; } = 100;
    public ushort ItemFlags { get; set; } = 2;
    public sbyte InvisibilityFlag { get; set; }
    public bool IsDead => (BodyFlags & 0x40) != 0;

    public void Die()
    {
        if (IsDead) return;
        BodyFlags |= 0x40;
        ItemFlags |= 1;
    }
}
