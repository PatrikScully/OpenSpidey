using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public readonly record struct ThugRibbonPoint(FixedVector3 Position, byte Red, byte Green, byte Blue, ushort Width);

/// <summary>Original CThugBulletTracer geometry, Rnd call order and five bit-update lifetime.</summary>
public sealed class ThugBulletTracerState
{
    private readonly Func<int, int> random;
    private readonly ThugRibbonPoint[] colored = new ThugRibbonPoint[5], white = new ThugRibbonPoint[5];
    public IReadOnlyList<ThugRibbonPoint> ColoredPoints => colored;
    public IReadOnlyList<ThugRibbonPoint> WhitePoints => white;
    public uint MaximumWidth { get; private set; } = 3;
    public short Age { get; private set; }
    public bool Alive { get; private set; } = true;

    // Host collision/ping and line ricochet effects precede this geometry in the source constructor.
    public ThugBulletTracerState(FixedVector3 start, FixedVector3 end, byte red, byte green, byte blue, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(random);
        this.random = random;
        FixedVector3 difference = (start - end) >> 12;
        int squared = unchecked(difference.X * difference.X + difference.Y * difference.Y + difference.Z * difference.Z);
        int distance = FixedVectorMath.OriginalSquareRoot(squared);
        if (distance > 1000) start = end + (start - end) / distance * 1000;
        for (int i = 0; i < 5; i++)
        {
            colored[i] = new(default, red, green, blue, 0);
            white[i] = new(default, 255, 255, 255, 0);
        }
        SetWidth();
        FixedVector3 direction = end - start;
        for (int i = 0; i < 5; i++)
        {
            FixedVector3 position = start + direction * i / 4;
            colored[i] = colored[i] with { Position = position };
            white[i] = white[i] with { Position = position };
        }
    }

    public void Advance()
    {
        if (!Alive) return;
        SetWidth();
        if (MaximumWidth != 0) MaximumWidth--;
        colored[Age] = colored[Age] with { Red = 0, Green = 0, Blue = 0 };
        white[Age] = white[Age] with { Red = 0, Green = 0, Blue = 0 };
        if (Age < 4) Age++;
        else Alive = false;
    }

    private void SetWidth()
    {
        for (int i = 0; i < 5; i++)
        {
            colored[i] = colored[i] with { Width = unchecked((ushort)((ushort)MaximumWidth * 2 + random((int)MaximumWidth))) };
            white[i] = white[i] with { Width = unchecked((ushort)random((int)MaximumWidth)) };
        }
        colored[0] = colored[0] with { Width = 0 };
        colored[4] = colored[4] with { Width = unchecked((ushort)random((int)MaximumWidth)) };
        white[0] = white[0] with { Width = 0 };
        white[4] = white[4] with { Width = 0 };
    }
}

/// <summary>Actor-owned CGPolyLine in CThug::SetUpLaser; it persists until explicitly deleted.</summary>
public sealed class ThugBarrelLineState
{
    public bool Exists { get; private set; }
    public FixedVector3 Start { get; private set; }
    public FixedVector3 End { get; private set; }
    public uint StartCodeRgb => 0x52500000;
    public uint EndCodeRgb { get; private set; }

    public void SetEndpoints(FixedVector3 start, FixedVector3 end)
    {
        int largest = GameWord.Absolute(unchecked(start.X - end.X));
        int middle = GameWord.Absolute(unchecked(start.Y - end.Y));
        int smallest = GameWord.Absolute(unchecked(start.Z - end.Z));
        if (largest < middle) (largest, middle) = (middle, largest);
        if (middle < smallest) (middle, smallest) = (smallest, middle);
        if (largest < middle) (largest, middle) = (middle, largest);
        int distance = unchecked(largest + (middle >> 1) + (smallest >> 2)) >> 12;
        byte blue = distance >= 1000 ? (byte)0 : unchecked((byte)(unchecked(320 * (1000 - distance)) >> 12));
        Exists = true;
        Start = start;
        // SetStartAndEnd divides the wrapping delta by one then adds it to start.
        End = start + (end - start);
        EndCodeRgb = 0x52000000u | (uint)blue << 16;
    }

    public void Clear() => Exists = false;
}
