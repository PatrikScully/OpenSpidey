using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Rhino;

/// <summary>M3dZone_LineToItem result fields used by CRhino::GonnaHitWall (0x47E8A0).</summary>
public readonly record struct RhinoChargeWallContact(
    uint ModelChecksum,
    int ActorType,
    uint ItemFlags,
    FixedVector3 HitPosition,
    FixedVector3 Normal,
    object? Collider);
