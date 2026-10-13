using OpenSpidey.Assets.Models;

namespace OpenSpidey.Core.World;

/// <summary>One loaded source model/DCF face pair; archive model records remain immutable.</summary>
public sealed class EnvironmentChunkModelState : IEnvironmentChunkModel
{
    private readonly PsxModel source;
    private readonly EnvironmentChunkFaceState[] faces;
    private ushort flags;
    private uint rendererFlags;
    public EnvironmentChunkModelState(PreparedEnvironmentModel prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        source = prepared.Model; flags = source.Flags; rendererFlags = prepared.RendererFlags;
        if (source.Faces.Count != prepared.RendererFaceFlags.Count) throw new InvalidDataException("Prepared source/DC face counts differ.");
        faces = source.Faces.Select((face, index) => new EnvironmentChunkFaceState(face,
            prepared.RendererFaceFlags[index], Changed)).ToArray();
    }
    public int Revision { get; private set; }
    private void Changed() => Revision++;
    public ushort Flags { get => flags; set { if (flags != value) { flags = value; Changed(); } } }
    public uint RendererFlags { get => rendererFlags; set { if (rendererFlags != value) { rendererFlags = value; Changed(); } } }
    public IReadOnlyList<IEnvironmentChunkFace> Faces => faces;
    public PsxModel RenderSnapshot() => source with { Flags = flags, Faces = Array.AsReadOnly(faces.Select(face => face.RenderSnapshot()).ToArray()) };
    public PsxModel CollisionSnapshot() => source with { Flags = flags, Faces = Array.AsReadOnly(faces.Select(face => face.CollisionSnapshot()).ToArray()) };
}

public sealed class EnvironmentChunkFaceState : IEnvironmentChunkFace
{
    private readonly PsxFace source;
    private readonly Action changed;
    private uint flags, collisionWord;
    private ushort rendererFlags;
    internal EnvironmentChunkFaceState(PsxFace source, ushort rendererFlags, Action changed)
    {
        this.source = source; this.changed = changed;
        flags = source.Flags; collisionWord = source.CollisionWord ?? throw new InvalidDataException("Source chunk face has no actual collision word.");
        this.rendererFlags = rendererFlags;
    }
    public uint Flags { get => flags; set { if (flags != value) { flags = value; changed(); } } }
    public uint CollisionWord { get => collisionWord; set { if (collisionWord != value) { collisionWord = value; changed(); } } }
    public ushort RendererFlags { get => rendererFlags; set { if (rendererFlags != value) { rendererFlags = value; changed(); } } }
    internal PsxFace RenderSnapshot() => source with { Flags = flags, CollisionWord = collisionWord };
    // Native collision metadata retains indices; the source runtime word has already multiplied each index by8.
    internal PsxFace CollisionSnapshot() => source with { Flags = flags,
        CollisionWord = (collisionWord & 0xFFFF0000) | ((collisionWord & 0xFFFF) >> 3) };
}
