using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Actual player-owned CSwinger body and its protected CKnottedWeb, advanced directly before player physics.</summary>
public sealed class WebSwingAnchor : IDisposable
{
    public FixedVector3 Anchor { get; }
    public int Length { get; }
    public SourceMatrix BaseMatrix { get; }
    public SourceMatrix SwingMatrix { get; private set; }
    public FixedVector3 Forward { get; private set; }
    public FixedVector3 Side { get; private set; }
    public FixedVector3 Down { get; private set; }
    public FixedVector3 RenderEndOffset { get; private set; }
    public KnottedWebStrand Strand { get; }
    public uint LastUpdateTick { get; private set; }
    public ActorAngles Angles { get; set; }
    public int Phase { get; private set; } = 2048;
    public int PhaseSpeed { get; }
    public int LengthEighth { get; }
    public int SampleCount => 9;
    public int FireVariant { get; set; }
    public bool IsComplete => Phase >= 4096;
    public bool IsDisposed { get; private set; }
    public FixedVector3 CurrentPosition => Anchor - Down * Length;

    public WebSwingAnchor(FixedVector3 anchor, int length, SourceMatrix basis, FixedVector3 direction, uint sourceClock,
        FixedVector3 cameraWhole, Func<int, int> random)
    {
        Anchor = anchor; Length = length; BaseMatrix = basis; LastUpdateTick = sourceClock;
        SetAxes(basis);
        Strand = new(Anchor, CurrentPosition, random); UpdateLine(CurrentPosition, cameraWhole);
        Strand.WobbleKnots = 1; Strand.FireVariant = FireVariant;
        int dot = unchecked((unchecked(Down.X * direction.X) >> 12) + (unchecked(Down.Y * direction.Y) >> 12) +
            (unchecked(Down.Z * direction.Z) >> 12));
        float radians = (float)Math.Acos((double)(float)GameWord.Absolute(dot) / 4096.0);
        Angles = new(unchecked((short)GameWord.TruncateToInt32((double)radians * 2048.0 / (float)3.1415927)), 0, 0);
        PhaseSpeed = (120 - FixedVectorMath.OriginalSquareRoot(length)) / 2;
        LengthEighth = length / 8;
    }
    public void Advance(uint sourceClock, FixedVector3 cameraWhole)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (FireVariant != 0) Strand.FireVariant = 1;
        Phase = unchecked(Phase + unchecked((int)(sourceClock - LastUpdateTick)) * PhaseSpeed);
        LastUpdateTick = sourceClock;
        short pitch = unchecked((short)(unchecked(Angles.X * (GameAngle.Heading(Phase).Cosine + 4096)) >> 12));
        SourceMatrix rotation = SourceMatrix.FromRotation(ActorJointPoseBuilder.JointRotation(new(pitch, 0, Angles.Z, 0, 0, 0)));
        SwingMatrix = BaseMatrix.MultiplyRotation(rotation); SetAxes(SwingMatrix); UpdateLine(CurrentPosition, cameraWhole);
    }
    private void SetAxes(SourceMatrix matrix)
    {
        Forward = new(matrix.M02, matrix.M12, matrix.M22);
        Side = new(matrix.M00, matrix.M10, matrix.M20);
        Down = new(-matrix.M01, -matrix.M11, -matrix.M21);
    }
    private void UpdateLine(FixedVector3 end, FixedVector3 cameraWhole)
    {
        Strand.SetEndpoints(Anchor, end); Strand.Side = KnottedWebStrand.FacingCamera(Anchor, end, cameraWhole);
        Strand.CacheRestPositions();
    }
    public void SetRenderEnd(FixedVector3 end, FixedVector3 cameraWhole)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        RenderEndOffset = end - Strand.Ends[^1]; UpdateLine(end, cameraWhole);
    }
    public short AnimationFrame(int frameScale) => unchecked((short)(unchecked(frameScale * (Phase - 2048)) >> 11));
    /// <summary>Actual PC4F7550 adds the stored render offset; the recovered C++ subtraction was incorrect.</summary>
    public FixedVector3 ReleasedEndpoint => ReleasedSwingWeb.CreationEndpoint(Strand, RenderEndOffset);
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true; Strand.Dispose();
    }
}
