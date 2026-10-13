namespace OpenSpidey.Core.World;

/// <summary>Recovered M3dZone_LineToItem masks and TestItemFaces category gate.</summary>
public static class OriginalCollisionFilter
{
    public static bool Accepts(uint packed, CollisionQuery query)
    {
        uint excluded = (query & CollisionQuery.LineOfSight) != 0 ? 0x400000u : 0;
        if ((query & CollisionQuery.Enemy) == 0) excluded ^= 0x200000u;
        uint requiredComplement = (query & CollisionQuery.Camera) != 0 ? 0xFFEFFFFFu : uint.MaxValue;
        if ((query & CollisionQuery.Trigger) != 0) requiredComplement ^= 0x20000u;
        return (packed & excluded) == 0 && (requiredComplement | packed) == uint.MaxValue
            && ((packed ^ 0x10000u) & 0x30000u) != 0;
    }

    public static bool IsTriggerZone(uint packed) => (packed & 0x20000) != 0;
    public static bool IsInstanceEnabled(ushort flags) => (flags & 0x21) == 0;
}
