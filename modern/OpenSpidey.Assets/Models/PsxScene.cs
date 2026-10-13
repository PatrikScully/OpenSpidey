using System.Numerics;

namespace OpenSpidey.Assets.Models;

public sealed record PsxScene(uint Version, IReadOnlyList<PsxInstance> Instances,
    IReadOnlyList<PsxModel> Models, IReadOnlyList<uint> ColorTable);

public sealed record PsxInstance(ushort Flags, Vector3 Position, Vector3 Angles,
    ushort ModelIndex, uint Color);

public sealed record PsxModel(ushort Flags, ushort NextLod, IReadOnlyList<PsxVertex> Vertices,
    IReadOnlyList<PsxFace> Faces)
{
    public uint? Checksum { get; init; }
    public IReadOnlyList<PsxNormal> Normals { get; init; } = [];
}

public readonly record struct PsxNormal(Vector3 Direction, ushort Flags);

public readonly record struct PsxVertex(Vector3 Position, ushort Flags);

public sealed record PsxFace(uint Flags, byte[] VertexIndices, uint Color,
    uint? TextureChecksum, byte[] TextureCoordinates, uint? CollisionWord = null)
{
    public bool IsTriangle => (Flags & 0x10) != 0;
}
