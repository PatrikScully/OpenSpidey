using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Scorpion constructor state, including the private tail buffers established before the first AI update.</summary>
public sealed class ScorpionState(int constructionOrdinal) : ScorpionEncounterBodyState(constructionOrdinal)
{
    public const short ActorType = 310;
    public uint CollisionPartMask { get; internal set; }
    public byte TailGeometryRegion { get; set; }
    public byte TailGlowRegion { get; set; }
    public byte BossCameraEnabled { get; set; }
    public FixedVector3[] TailControlPoints { get; } = new FixedVector3[4];
    public FixedVector3[] TailNodes { get; } = new FixedVector3[23];
    public FixedVector3[] TailHistory { get; } = new FixedVector3[128];
}
