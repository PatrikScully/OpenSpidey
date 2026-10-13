using OpenSpidey.Core.Actors.Switches;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Combat;

namespace OpenSpidey.Core.Actors.Manipulation;

/// <summary>Original CManipOb fields; model and ENV shadow ownership remain explicit host resources.</summary>
public sealed class ManipulationState(ushort node, ActorBodyMotion body)
{
    public ActorBodyMotion Body { get; } = body ?? throw new ArgumentNullException(nameof(body));
    public ushort Node { get; } = node;
    public const ushort Type = 401;
    public ushort BodyFlags { get; set; } = 0x16;
    public ushort ItemFlags { get; set; }
    public uint ModelChecksum { get; set; }
    public int Flags { get; set; } // Source +0x10C.
    public short ImpactSound { get; set; } // Source +0x104.
    public short ImpactCount { get; set; } // Source +0x106, omitted in the recovered header.
    public short Parameter108 { get; set; }
    public SwitchEnvironmentItem? ShadowItem { get; set; }
    public IReadOnlyList<uint> ChunkModels { get; set; } = Array.Empty<uint>();
    public FixedVector3 AttachOffset { get; set; }
    public byte PulseSent { get; set; }
    public FixedVector3[]? ThrowPositions { get; set; }
    public int ThrowPositionIndex { get; set; }
    public int ThrowPositionCount { get; set; }
}
public sealed record ManipulationTargetBody(ActorLaserBody Sphere, ushort Type);
public readonly record struct ManipulationWorldHit(object Identity, FixedVector3 Position, FixedVector3 Normal);

public interface IManipulationHost
{
    void InitializeObjectLibrary();
    void AttachToEnvironmentalList(ManipulationState state);
    void SelectObjectModel(uint checksum);
    SwitchEnvironmentItem? FindEnvironmentItem(uint checksum);
    void HideEnvironmentModel(SwitchEnvironmentItem shadowItem);
    void SendAttachedPulse(ushort node);
    IReadOnlyList<ManipulationTargetBody> SourceBaddyOrder { get; }
    bool IgnoreSphereRadius { get; }
    ManipulationWorldHit? LineToItem(FixedVector3 start, FixedVector3 end, int mode);
    void HitBaddy(object identity, ActorHit hit);
    void ShakeCamera(FixedVector3 position, int magnitude);
    /// <summary>Run the real source Chunk behavior; an absent native effects service must report unsupported.</summary>
    void Chunk(ManipulationState state, FixedVector3 surfaceNormal);
}
