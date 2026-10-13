using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>A protected CRibbonBit: exact two endpoints, half-frame source animation and CFT4Bit fading.</summary>
public sealed class SmokeTrailSegment(byte frameCount)
{
    public FixedVector3 Start { get; internal set; }
    public FixedVector3 End { get; internal set; }
    public byte Flags { get; internal set; }
    public uint CodeBgr { get; internal set; }
    public ushort Scale { get; set; } = 400;
    public short AnimationSpeed { get; set; } = 128;
    public byte FrameFraction { get; private set; }
    public sbyte Frame { get; private set; }
    public byte FrameCount { get; } = frameCount;
    public byte FadeRate { get; internal set; } = 8;

    public void Move()
    {
        short combined = unchecked((short)(((Frame << 8) | FrameFraction) + AnimationSpeed));
        FrameFraction = unchecked((byte)combined);
        Frame = unchecked((sbyte)(combined >> 8));
        if (Frame >= FrameCount) Frame = 0;
    }
    public bool Fade()
    {
        if ((CodeBgr & 0xFFFFFF) == 0) return true;
        byte red = FadeChannel((byte)CodeBgr), green = FadeChannel((byte)(CodeBgr >> 8)), blue = FadeChannel((byte)(CodeBgr >> 16));
        CodeBgr = (CodeBgr & 0xFF000000) | (uint)(red | (green << 8) | (blue << 16));
        return false;
    }
    private byte FadeChannel(byte channel) => channel < FadeRate ? (byte)0 : unchecked((byte)(channel - FadeRate));
}

/// <summary>Actual CSmokeTrail/CRibbon's ring-buffer endpoints, protected child order and source fade-to-death.</summary>
public sealed class SmokeTrail
{
    private readonly FixedVector3[] points;
    public SmokeTrailSegment[] Segments { get; }
    public int Cursor { get; private set; }
    public int FadeAway { get; set; }
    public bool IsDead { get; private set; }
    public bool IsDisposed { get; private set; }
    public IReadOnlyList<FixedVector3> Points => points;

    public SmokeTrail(FixedVector3 position, int segments, int red, int green, int blue, byte frameCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segments);
        if (frameCount == 0) throw new ArgumentOutOfRangeException(nameof(frameCount));
        points = Enumerable.Repeat(position, checked(segments + 1)).ToArray();
        Segments = new SmokeTrailSegment[segments];
        int redStep = red / segments, greenStep = green / segments, blueStep = blue / segments;
        for (int index = segments - 1; index >= 0; index--)
        {
            int distance = segments - 1 - index;
            Segments[index] = new(frameCount) { Flags = index == 0 ? (byte)0x10 : (byte)0,
                CodeBgr = 0x2E000000 | (uint)(unchecked((byte)(red - distance * redStep)) |
                    (unchecked((byte)(green - distance * greenStep)) << 8) | (unchecked((byte)(blue - distance * blueStep)) << 16)) };
        }
    }
    public void SetPosition(FixedVector3 position)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (++Cursor == points.Length) Cursor = 0;
        points[Cursor] = position;
        int point = Cursor;
        for (int index = Segments.Length - 1; index >= 0; index--)
        {
            Segments[index].End = points[point];
            if (--point < 0) point += points.Length;
            Segments[index].Start = points[point];
        }
    }
    public void Move()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (FadeAway == 0) return;
        bool complete = true;
        foreach (SmokeTrailSegment segment in Segments) if (!segment.Fade()) complete = false;
        if (complete) IsDead = true;
    }
    public void Dispose() => IsDisposed = true;
}
