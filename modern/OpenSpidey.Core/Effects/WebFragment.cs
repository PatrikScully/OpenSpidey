using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Effects;

/// <summary>Original CWebFrag and its protected companion CGLine, owned and retired together.</summary>
public sealed class WebFragment
{
    public int GroundY { get; }
    public FixedVector3 Start { get; private set; }
    public FixedVector3 End { get; private set; }
    public FixedVector3 CompanionEnd { get; private set; }
    public FixedVector3 StartVelocity { get; private set; }
    public FixedVector3 EndVelocity { get; private set; }
    public FixedVector3 CompanionVelocity { get; private set; }
    public int ColourDecay { get; }
    public uint CodeRgb0 { get; private set; } = 0x52808080;
    public uint CodeRgb1 { get; private set; } = 0x55808080;
    public bool Alive { get; private set; } = true;
    public bool CompanionProtected => true;
    public object CompanionIdentity { get; } = new();
    public bool CompanionAlive { get; private set; } = true;
    public FixedVector3 CompanionStart => Start;

    public WebFragment(ImpactWebFragmentRequest request, Func<int, int> random)
    {
        GroundY = request.GroundY;
        Start = request.FirstHook;
        End = request.Previous;
        CompanionEnd = request.SecondHook;
        FixedVector3 direction = request.FirstHook - request.OwnerPosition;
        FixedVector3 whole = direction >> 12;
        int length = FixedVectorMath.OriginalSquareRoot(unchecked(whole.X * whole.X + whole.Y * whole.Y + whole.Z * whole.Z));
        if (length != 0) StartVelocity = direction * request.Speed / length;
        EndVelocity = StartVelocity;
        CompanionVelocity = StartVelocity;
        if (request.Jitter == 0)
        {
            EndVelocity += new FixedVector3(0, unchecked((random(21) - 10) << 12), 0);
            StartVelocity += new FixedVector3(0, unchecked((random(21) - 10) << 12), 0);
            CompanionVelocity += new FixedVector3(0, unchecked((random(21) - 10) << 12), 0);
        }
        ColourDecay = random(3) != 0 ? random(3) + 6 : random(3) + 1;
    }

    public void DestroyCompanion() => CompanionAlive = false;

    public void AdvanceBits()
    {
        if (!Alive) return;
        End += EndVelocity;
        Start += StartVelocity;
        CompanionEnd += CompanionVelocity;
        if (End.Y > GroundY) { End = End with { Y = GroundY }; EndVelocity >>= 1; }
        if (Start.Y > GroundY) { Start = Start with { Y = GroundY }; StartVelocity >>= 1; }
        if (CompanionEnd.Y > GroundY) { CompanionEnd = CompanionEnd with { Y = GroundY }; CompanionVelocity >>= 1; }
        StartVelocity += new FixedVector3(0, 29584, 0);
        CompanionVelocity += new FixedVector3(0, 29584, 0);
        EndVelocity += new FixedVector3(0, 29584, 0);
        int colour = Math.Max((byte)CodeRgb0 - ColourDecay, 0);
        uint rgb = (uint)(colour | colour << 8 | colour << 16);
        CodeRgb0 = (CodeRgb0 & 0xFF000000u) | rgb;
        CodeRgb1 = rgb;
        if (colour == 0) Alive = false;
    }
}
