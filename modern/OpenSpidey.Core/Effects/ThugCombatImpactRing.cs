using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Original CCombatImpactRing growth and delayed equality death on the source clock.</summary>
public sealed class ThugCombatImpactRing
{
    public FixedVector3 Position { get; }
    public short Angle { get; }
    public short Scale { get; private set; }
    public int MaximumScale { get; }
    public int GrowthPerTick { get; }
    public uint LastClock { get; private set; }
    public uint CodeRgb { get; }
    public uint PostScale => 0x0A001000;
    public ushort FrigDeltaZ => 64;
    public int AnimationIndex => 8;
    public int Frame => 0;
    public bool Alive { get; private set; } = true;

    public ThugCombatImpactRing(FixedVector3 position, byte red, byte green, byte blue,
        int initialScale, int maximumScale, int growthPerTick, uint clock, Func<int, int> random)
    {
        Position = position;
        Angle = unchecked((short)random(4096));
        Scale = unchecked((short)initialScale);
        MaximumScale = maximumScale;
        GrowthPerTick = growthPerTick;
        LastClock = clock;
        CodeRgb = 0x2E000000u | (uint)(red | green << 8 | blue << 16);
    }

    public void Advance(uint clock)
    {
        if (!Alive) return;
        if (Scale == MaximumScale) { Alive = false; return; }
        int elapsed = unchecked((int)(clock - LastClock));
        LastClock = clock;
        Scale = unchecked((short)(Scale + GrowthPerTick * elapsed));
        if (Scale > MaximumScale) Scale = unchecked((short)MaximumScale);
    }

    // CThug ignores its flags argument; actor type alone selects the two constructor color banks.
    public static ThugCombatImpactRing[] Create(int actorType, FixedVector3 position, uint clock, Func<int, int> random)
    {
        return actorType == 304
            ? [new(position, 18, 18, 108, 384, 1792, 144, clock, random), new(position, 72, 72, 144, 192, 896, 72, clock, random)]
            : [new(position, 108, 108, 18, 384, 1792, 144, clock, random), new(position, 144, 144, 72, 192, 896, 72, clock, random)];
    }
}
