using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Original world-query coordinates and metadata at a ground hit.</summary>
public readonly record struct PlayerGroundContact(FixedVector3 Position, FixedVector3 Normal,
    int Distance, uint SurfaceFlags, bool SupportsPlayer);

public readonly record struct PlayerGroundFrame(FixedVector3 Position, FixedVector3 InitialPosition,
    FixedVector3 Velocity, ushort CollisionFlags, ushort SurfaceClearance, bool HoldingObject, AxisDamping Damping);

/// <summary>Resolved motion and the notifications the game adapter must apply.</summary>
public readonly record struct PlayerGroundResponse(FixedVector3 Position, FixedVector3 Velocity,
    ushort CollisionFlags, byte GroundContactGraceUpdates, FixedVector3 SurfaceNormal,
    FixedVector3 ShadowPosition, bool NotifySupport, bool UseAlternateBrightness);
