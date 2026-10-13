using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Actual CDomeRing's shared-model expansion, signed short vertices and RGB3 source lifetime.</summary>
public sealed class WebDomeRing
{
    private readonly FixedVector3[] vertices;
    private readonly FixedVector3[] directions;
    public FixedVector3 Position { get; }
    public int FireVariant { get; }
    public string ModelName => FireVariant != 0 ? "firering" : "ring";
    public ushort ItemFlags => FireVariant != 0 ? (ushort)0x200 : (ushort)0x600;
    public uint Tint { get; private set; } = 0x808080;
    public ushort BodyFlags { get; set; } = 0x16;
    public IReadOnlyList<FixedVector3> Vertices => vertices;
    public IReadOnlyList<FixedVector3> Directions => directions;
    public WebDomeRing(FixedVector3 position, int fireVariant, IReadOnlyList<FixedVector3> sourceVertices)
    {
        Position = position with { Y = unchecked(position.Y - 0x5A000) };
        FireVariant = fireVariant;
        vertices = sourceVertices.ToArray(); directions = new FixedVector3[vertices.Length];
        for (int index = 0; index < vertices.Length; index++)
        {
            FixedVector3 vertex = vertices[index];
            int length = FixedVectorMath.OriginalSquareRoot(unchecked(vertex.X * vertex.X + vertex.Z * vertex.Z));
            if (length == 0) throw new InvalidDataException("Original CDomeRing source vertex has a zero horizontal radius.");
            directions[index] = new(unchecked((short)(vertex.X * 100 / length)), 0, unchecked((short)(vertex.Z * 100 / length)));
        }
    }
    public void AdvanceActor()
    {
        for (int index = 0; index < vertices.Length; index++)
        {
            FixedVector3 vertex = vertices[index], direction = directions[index];
            vertices[index] = new(unchecked((short)(vertex.X + direction.X)), vertex.Y, unchecked((short)(vertex.Z + direction.Z)));
        }
        int channel = (byte)Tint - 3;
        if (channel <= 0) { channel = 0; BodyFlags |= 0x40; }
        Tint = unchecked((uint)(channel | channel << 8 | channel << 16));
    }
}
