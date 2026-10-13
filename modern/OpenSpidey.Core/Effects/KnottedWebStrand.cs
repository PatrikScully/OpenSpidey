using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public sealed class WebStrandKnot
{
    public FixedVector3 RestPosition { get; internal set; }
    public FixedVector3 Position { get; internal set; }
    public byte WobbleAmplitude { get; set; } = 4;
    public byte WobbleSpeed { get; internal set; }
    public short Phase { get; internal set; }
    public byte Fraction { get; internal set; }
    public sbyte SideOffset { get; internal set; }
    public uint CodeRgb { get; internal set; } = 0x2E808080;
    public short Scale => 176;
    public uint PostScale => 0x10001000;
    public bool Protected => true;
}

public sealed class KnottedWebInnerPolyline(KnottedWebStrand owner)
{
    public KnottedWebStrand Owner { get; } = owner;
    public bool Protected => true;
}

/// <summary>Actual CKnottedWeb base/inner polylines and protected knot sprites.</summary>
public sealed class KnottedWebStrand
{
    private readonly FixedVector3[] ends, innerEnds;
    private readonly uint[] colours, innerColours;
    private readonly WebStrandKnot[] knots;
    public IReadOnlyList<FixedVector3> Ends { get; }
    public IReadOnlyList<FixedVector3> InnerEnds { get; }
    public IReadOnlyList<uint> Colours { get; }
    public IReadOnlyList<uint> InnerColours { get; }
    public IReadOnlyList<WebStrandKnot> Knots { get; }
    public KnottedWebInnerPolyline InnerPolyline { get; }
    public event Action? Disposed;
    public FixedVector3 Start { get; private set; }
    public FixedVector3 InnerStart { get; private set; }
    public FixedVector3 Side { get; set; }
    public uint StartColour { get; private set; } = 0x50A2A2A2;
    public uint InnerStartColour { get; private set; } = 0x50A2A2A2;
    public byte FlickerKnots { get; set; }
    public byte WobbleKnots { get; set; }
    public byte TriggerPulse { get; set; }
    public byte Pulse { get; private set; }
    public byte PulseDecay { get; set; } = 1;
    public int FireVariant { get; set; }
    public int SegmentCount => ends.Length;
    public bool Alive { get; private set; } = true;
    public bool IsDisposed { get; private set; }

    public KnottedWebStrand(FixedVector3 start, FixedVector3 end, Func<int, int> random)
    {
        FixedVector3 delta = (end - start) >> 12;
        int length = FixedVectorMath.OriginalSquareRoot(unchecked(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z));
        int count = Math.Clamp(length / 80, 1, 40);
        ends = new FixedVector3[count]; innerEnds = new FixedVector3[count * 2];
        colours = Enumerable.Repeat(0x50A2A2A2u, count).ToArray();
        innerColours = Enumerable.Repeat(0x50A2A2A2u, count * 2).ToArray();
        knots = new WebStrandKnot[count];
        for (int i = 0; i < count; i++) knots[i] = new()
        {
            WobbleSpeed = unchecked((byte)(random(100) + 125)), Phase = unchecked((short)random(4096)),
            Fraction = unchecked((byte)(random(192) + 64)), SideOffset = unchecked((sbyte)(random(19) - 9))
        };
        Ends = Array.AsReadOnly(ends); InnerEnds = Array.AsReadOnly(innerEnds);
        Colours = Array.AsReadOnly(colours); InnerColours = Array.AsReadOnly(innerColours); Knots = Array.AsReadOnly(knots);
        InnerPolyline = new(this);
    }

    public void SetEndpoints(FixedVector3 start, FixedVector3 end)
    {
        CheckAlive();
        FixedVector3 step = (end - start) / SegmentCount, current = start;
        Start = start;
        for (int i = 0; i < SegmentCount; i++) { current += step; ends[i] = current; }
    }
    public void CacheRestPositions()
    {
        CheckAlive(); for (int i = 0; i < SegmentCount; i++) knots[i].RestPosition = ends[i];
    }
    public void SetPolylinePoint(int index, FixedVector3 point) { CheckAlive(); ends[index] = point; }
    public void SetPolylineStart(FixedVector3 point) { CheckAlive(); Start = point; }
    public void ClearPulse() { CheckAlive(); TriggerPulse = 0; Pulse = 0; }

    /// <summary>CSwingBack fades each knot, then copies the faded outer-head RGB to both paired polylines.</summary>
    public void FadeReleasedSwing()
    {
        CheckAlive();
        foreach (WebStrandKnot knot in knots) knot.CodeRgb = FadeRgb(knot.CodeRgb);
        StartColour = FadeRgb(StartColour);
        uint rgb = StartColour & 0xFFFFFF;
        for (int i = 0; i < colours.Length; i++) colours[i] = (colours[i] & 0xFF000000) | rgb;
        InnerStartColour = (InnerStartColour & 0xFF000000) | rgb;
        for (int i = 0; i < innerColours.Length; i++) innerColours[i] = (innerColours[i] & 0xFF000000) | rgb;
    }
    public void SetPolylinesSemiTransparent()
    {
        CheckAlive(); StartColour |= 0x02000000; InnerStartColour |= 0x02000000;
        for (int i = 0; i < colours.Length; i++) colours[i] |= 0x02000000;
        for (int i = 0; i < innerColours.Length; i++) innerColours[i] |= 0x02000000;
    }
    private static uint FadeRgb(uint colour)
    {
        uint result = colour & 0xFF000000;
        for (int shift = 0; shift < 24; shift += 8)
        {
            uint channel = (colour >> shift) & 255;
            result |= (channel > 10 ? channel - 10 : channel) << shift;
        }
        return result;
    }

    public void AdvanceBits(uint clock, Func<int, int> random, Action<FixedVector3, int, Func<int, int>> emitFirePulse)
    {
        CheckAlive();
        if (FireVariant != 0)
        {
            FixedVector3 delta = ends[^1] - Start;
            for (int i = 0; i < 3; i++) emitFirePulse(Start + ((delta * random(256)) >> 8), 256, random);
            foreach (WebStrandKnot knot in knots)
            {
                int tint = random(192);
                knot.CodeRgb = 0x2E000000u | (uint)((255 * tint >> 8) + 63 | ((123 * tint >> 8) + 30) << 8);
            }
            StartColour = FireColour(StartColour, random(128));
            for (int i = 0; i < SegmentCount; i++) colours[i] = FireColour(colours[i], random(128));
            InnerStartColour = FireColour(InnerStartColour, random(128));
            for (int i = 0; i < innerEnds.Length; i++) innerColours[i] = FireColour(innerColours[i], random(128));
        }
        if (WobbleKnots != 0)
            for (int i = 0; i < SegmentCount; i++)
            {
                WebStrandKnot knot = knots[i];
                short phase = unchecked((short)(knot.Phase + unchecked((int)clock) * knot.WobbleSpeed));
                int offset = knot.WobbleAmplitude * GameAngle.Heading(phase).Sine >> 12;
                ends[i] = knot.RestPosition + Side * offset;
            }
        if (TriggerPulse != 0) Pulse = 20;
        if (Pulse != 0)
        {
            for (int i = 0; i < SegmentCount; i++)
            {
                int offset = unchecked((Pulse * GameAngle.Heading(unchecked((int)clock << 9)).Sine >> 12) *
                    GameAngle.Heading(2048 * (i + 1) / SegmentCount).Sine) >> 12;
                ends[i] = knots[i].RestPosition + Side * offset;
            }
            TriggerPulse = 0;
            Pulse = PulseDecay > Pulse ? (byte)0 : unchecked((byte)(Pulse - PulseDecay));
        }
        InnerStart = Start; FixedVector3 previous = Start;
        for (int i = 0; i < SegmentCount; i++)
        {
            WebStrandKnot knot = knots[i];
            if (FlickerKnots != 0) { knot.Fraction = unchecked((byte)random(256)); knot.SideOffset = unchecked((sbyte)(random(19) - 9)); }
            FixedVector3 position = previous + (((ends[i] - previous) * knot.Fraction) >> 8);
            knot.Position = position;
            FixedVector3 middle = i != 0 ? previous : (Start + position) >> 1;
            innerEnds[2 * i] = middle + Side * knot.SideOffset;
            innerEnds[2 * i + 1] = position;
            previous = ends[i];
        }
    }
    private static uint FireColour(uint original, int tint) => (original & 0xFF000000u) |
        (uint)((255 * tint >> 8) + 127 | ((123 * tint >> 8) + 61) << 8);
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true; Alive = false; Disposed?.Invoke();
    }
    public void MarkDead() => Alive = false;
    private void CheckAlive() { if (!Alive) throw new ObjectDisposedException(nameof(KnottedWebStrand)); }

    public static FixedVector3 FacingCamera(FixedVector3 start, FixedVector3 end, FixedVector3 cameraWhole)
    {
        FixedVector3 delta = (end - start) >> 12;
        if (delta.X > 500 || delta.Y > 500 || delta.Z > 500) delta >>= 4;
        FixedVector3 view = cameraWhole - (start >> 12);
        var cross = new FixedVector3(unchecked(delta.Y * view.Z - delta.Z * view.Y), unchecked(delta.Z * view.X - delta.X * view.Z),
            unchecked(delta.X * view.Y - delta.Y * view.X));
        FixedVector3 shifted = cross >> 8;
        int magnitude = FixedVectorMath.OriginalSquareRoot(unchecked(shifted.X * shifted.X + shifted.Y * shifted.Y + shifted.Z * shifted.Z));
        return magnitude < 5 ? default : (cross / magnitude) << 4;
    }
}
