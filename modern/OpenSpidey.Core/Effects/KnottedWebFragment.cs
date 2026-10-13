using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public readonly record struct KnottedWebFragmentRequest(FixedVector3 Start, FixedVector3 End,
    FixedVector3 StartVelocity, FixedVector3 EndVelocity);

/// <summary>Original CKnottedWebFrag endpoint gravity, floor damping, contraction and owned knotted strand.</summary>
public sealed class KnottedWebFragment
{
    public KnottedWebStrand Strand { get; }
    public FixedVector3 Start { get; private set; }
    public FixedVector3 End { get; private set; }
    public FixedVector3 StartVelocity { get; private set; }
    public FixedVector3 EndVelocity { get; private set; }
    public int GroundY { get; }
    public int RestDistance { get; }
    public short Age { get; private set; }
    public bool Alive { get; private set; } = true;

    public KnottedWebFragment(KnottedWebFragmentRequest request, Func<int, int> random,
        Func<FixedVector3, int> groundY)
    {
        Strand = new(request.Start, request.End, random);
        Start = request.Start; End = request.End;
        StartVelocity = request.StartVelocity; EndVelocity = request.EndVelocity;
        GroundY = groundY((Start + End) >> 1);
        Strand.TriggerPulse = 1; Strand.PulseDecay = 2;
        RestDistance = Distance(Start, End) >> 3;
    }

    public void AdvanceBits(uint clock, FixedVector3 cameraWhole, Func<int, int> random,
        Action<FixedVector3, int, Func<int, int>> emitFirePulse)
    {
        if (!Alive) return;
        Start += StartVelocity;
        StartVelocity += new FixedVector3(0, 29584, 0);
        if (Start.Y > GroundY)
        {
            Start = Start with { Y = GroundY };
            StartVelocity = new(StartVelocity.X >> 1, 0, StartVelocity.Z >> 1);
        }
        End += EndVelocity;
        EndVelocity += new FixedVector3(0, 29584, 0);
        if (End.Y > GroundY)
        {
            End = End with { Y = GroundY };
            EndVelocity = new(EndVelocity.X >> 1, 0, EndVelocity.Z >> 1);
        }
        int distance = Distance(Start, End);
        // A coincident source fragment divides by zero. Retire it cleanly instead of crashing the modern engine.
        if (distance == 0) { Alive = false; Strand.MarkDead(); return; }
        FixedVector3 middle = (Start + End) >> 1;
        FixedVector3 correction = (middle + (Start - middle) * RestDistance / distance - Start) >> 4;
        Start += correction; End -= correction;
        Strand.SetEndpoints(Start, End);
        Strand.Side = KnottedWebStrand.FacingCamera(Start, End, cameraWhole);
        Strand.CacheRestPositions();
        Strand.AdvanceBits(clock, random, emitFirePulse);
        Age = unchecked((short)(Age + 1));
        if (Age > 30) { Alive = false; Strand.MarkDead(); }
    }

    internal static int Distance(FixedVector3 from, FixedVector3 to)
    {
        // Utils_Dist shifts from-minus-to before squaring; reversing it changes rounding of fractional coordinates.
        FixedVector3 whole = (from - to) >> 12;
        return FixedVectorMath.OriginalSquareRoot(unchecked(whole.X * whole.X + whole.Y * whole.Y + whole.Z * whole.Z));
    }
    public void Dispose() { Alive = false; Strand.Dispose(); }
}
