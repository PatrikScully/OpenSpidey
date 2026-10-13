namespace OpenSpidey.Core.World;

/// <summary>TestItemFaces records accepted zone models separately from the nearest blocking face.</summary>
public static class OriginalTriggerZoneContacts
{
    public static bool Records(uint packedFace, bool recordTriggerZoneHits) => recordTriggerZoneHits &&
        OriginalCollisionFilter.Accepts(packedFace, CollisionQuery.Player) && OriginalCollisionFilter.IsTriggerZone(packedFace);
}
