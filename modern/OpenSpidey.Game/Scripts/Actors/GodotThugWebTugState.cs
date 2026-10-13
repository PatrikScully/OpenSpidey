using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Game.Actors;

public partial class GodotThugActor
{
    internal FixedVector3[]? SnapshotThrowPoints() => throwPoints?.ToArray();
    internal void RestoreThrowPoints(FixedVector3[]? points) => throwPoints = points?.ToArray();
}
