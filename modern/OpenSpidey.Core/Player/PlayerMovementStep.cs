using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerMovementStep(FixedVector3 Position, FixedVector3 Velocity);
