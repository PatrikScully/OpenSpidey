using System.Numerics;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.World;

public readonly record struct CollisionTriangle(Vector3 A, Vector3 B, Vector3 C,
    Vector3 Normal, int FaceIndex, uint PackedMetadata)
{
    public FixedVector3 FixedNormal { get; init; }
}
