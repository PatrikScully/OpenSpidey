using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Actual CSwingBack GPolyLine: released web falls toward its anchor's vertical, retracts and fades.</summary>
public sealed class ReleasedSwingWeb : IDisposable
{
    public static FixedVector3 CreationEndpoint(KnottedWebStrand held, FixedVector3 renderOffset) => held.Ends[^1] + renderOffset;
    public static ReleasedSwingWeb? Create(KnottedWebStrand held, FixedVector3 renderOffset, int fireVariant,
        bool lowMemory, FixedVector3 cameraWhole, Func<int, int> random) => lowMemory ? null :
        new(held.Start, CreationEndpoint(held, renderOffset), fireVariant, cameraWhole, random);
    public KnottedWebStrand Strand { get; }
    public int Retracting { get; private set; }
    public short Age { get; private set; }
    public bool Alive => Strand.Alive;
    public ReleasedSwingWeb(FixedVector3 start, FixedVector3 end, int fireVariant,
        FixedVector3 cameraWhole, Func<int, int> random)
    {
        Strand = new(start, end, random);
        Strand.SetEndpoints(start, end); Strand.Side = KnottedWebStrand.FacingCamera(start, end, cameraWhole);
        Strand.CacheRestPositions(); Strand.WobbleKnots = 1; Strand.SetPolylinesSemiTransparent(); Strand.FireVariant = fireVariant;
    }
    public void Move(uint clock, FixedVector3 playerPosition, FixedVector3 cameraWhole, Func<int, int> random,
        Action<FixedVector3, int, Func<int, int>> emitFirePulse)
    {
        if (!Alive) return;
        FixedVector3 end = Strand.Knots[^1].RestPosition;
        uint distance = unchecked((uint)FixedVectorMath.OriginalDistance(Strand.Start, end));
        if (Retracting != 0) distance = unchecked(distance - 700);
        FixedVector3 target = Strand.Start with { Y = unchecked(Strand.Start.Y + (int)(distance << 12)) };
        FixedVector3 delta = target - end;
        end += delta >> 4;
        FixedVector3 whole = delta >> 12;
        if (FixedVectorMath.OriginalSquareRoot(unchecked(whole.X * whole.X + whole.Y * whole.Y + whole.Z * whole.Z)) < 500) Retracting = 1;
        Strand.SetEndpoints(Strand.Start, end); Strand.Side = KnottedWebStrand.FacingCamera(Strand.Start, end, cameraWhole);
        Strand.CacheRestPositions(); Strand.AdvanceBits(clock, random, emitFirePulse); Strand.FadeReleasedSwing();
        if (unchecked((uint)ActorLifecycle.Distance(end, playerPosition)) > 2000) Strand.MarkDead();
        Age = unchecked((short)(Age + 1)); if (Age > 60) Strand.MarkDead();
    }
    public void Dispose() => Strand.Dispose();
}
