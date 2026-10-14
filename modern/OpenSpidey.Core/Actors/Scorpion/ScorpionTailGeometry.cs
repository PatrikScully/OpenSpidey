using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Original489810 ring sweep and signed16 render storage, independent of the native resource owner.</summary>
public sealed class ScorpionTailGeometry
{
    public const int RingSize = 4;
    public const int VertexCount = ScorpionTailPath.NodeCount * RingSize;
    public FixedVector3 Position { get; private set; }
    public uint BoundsX { get; private set; }
    public uint BoundsY { get; private set; }
    public uint BoundsZ { get; private set; }
    public FixedVector3[] Vertices { get; } = new FixedVector3[VertexCount];
    public FixedVector3[] Normals { get; } = new FixedVector3[VertexCount];

    /// <summary>Source regionFF skips all stores and hook callbacks. Initialized geometry is rebuilt before submission.</summary>
    public bool Rebuild(bool initialized, ReadOnlySpan<FixedVector3> nodes, IReadOnlyList<ActorHook> renderHooks,
        Func<ActorHook, FixedVector3> readDynamicHook)
    {
        if (!initialized) return false;
        if (nodes.Length != ScorpionTailPath.NodeCount || renderHooks.Count != 10)
            throw new ArgumentException("The original tail renderer requires23 nodes and ten source ring hooks.");
        ArgumentNullException.ThrowIfNull(readDynamicHook);
        FixedVector3 first = nodes[0], last = nodes[^1];
        FixedVector3 delta = last - first;
        Position = first + new FixedVector3(delta.X / 2, delta.Y / 2, delta.Z / 2);
        FixedVector3 relativeFirst = (first - Position) >> 12, relativeLast = (last - Position) >> 12;
        BoundsX = PackBounds(relativeFirst.X, relativeLast.X);
        BoundsY = PackBounds(relativeFirst.Y, relativeLast.Y);
        BoundsZ = PackBounds(relativeFirst.Z, relativeLast.Z);

        FixedVector3 normal = ReadRing(0, 0);
        for (int node = 1; node < ScorpionTailPath.NodeCount; node++)
        {
            FixedVector3 tangent = FixedVectorMath.Normalize((node == 22 ? nodes[node] - nodes[node - 1] : nodes[node + 1] - nodes[node]) >> 6);
            FixedVector3 binormal = FixedVectorMath.Normalize(CrossQ12(tangent, normal));
            normal = CrossQ12(binormal, tangent);
            if (node == 22) { ReadRing(node, 5); continue; }
            int radius = 16 - ((GameAngle.Heading(42 * (node + 1)).Sine * 8) >> 12);
            for (int corner = 0; corner < RingSize; corner++)
            {
                (short sine, short cosine) = GameAngle.Heading(corner << 10);
                FixedVector3 ringNormal = Narrow(new(
                    unchecked(((normal.X * cosine) >> 12) + ((binormal.X * sine) >> 12)),
                    unchecked(((normal.Y * cosine) >> 12) + ((binormal.Y * sine) >> 12)),
                    unchecked(((normal.Z * cosine) >> 12) + ((binormal.Z * sine) >> 12))));
                int vertex = node * RingSize + corner;
                Normals[vertex] = ringNormal;
                Vertices[vertex] = new(SweptAxis(nodes[node].X, ringNormal.X, radius, Position.X),
                    SweptAxis(nodes[node].Y, ringNormal.Y, radius, Position.Y),
                    SweptAxis(nodes[node].Z, ringNormal.Z, radius, Position.Z));
            }
        }
        return true;

        FixedVector3 ReadRing(int node, int hook)
        {
            FixedVector3 centre = readDynamicHook(renderHooks[hook]);
            FixedVector3 firstNormal = default;
            for (int corner = 0; corner < RingSize; corner++)
            {
                FixedVector3 point = readDynamicHook(renderHooks[hook + corner + 1]);
                FixedVector3 direction = FixedVectorMath.Normalize((point - centre) >> 6);
                int vertex = node * RingSize + corner;
                Normals[vertex] = Narrow(direction);
                Vertices[vertex] = Narrow((point - Position) >> 12);
                if (corner == 0) firstNormal = direction;
            }
            return firstNormal;
        }
    }

    private static FixedVector3 CrossQ12(FixedVector3 first, FixedVector3 second) => new(
        unchecked(first.Y * second.Z - first.Z * second.Y) >> 12,
        unchecked(first.Z * second.X - first.X * second.Z) >> 12,
        unchecked(first.X * second.Y - first.Y * second.X) >> 12);
    private static FixedVector3 Narrow(FixedVector3 value) => new(unchecked((short)value.X), unchecked((short)value.Y), unchecked((short)value.Z));
    private static uint PackBounds(int first, int last) => unchecked((uint)((ushort)Math.Max(first, last) | ((ushort)Math.Min(first, last) << 16)));
    // Original SHR applies to the wrapped node+radius product, while the midpoint uses SAR; both are narrowed only after subtraction.
    private static short SweptAxis(int node, int normal, int radius, int centre) =>
        unchecked((short)((unchecked((uint)(node + normal * radius)) >> 12) - (uint)(centre >> 12)));
}
