using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public readonly record struct ExplosionModelParameters(FixedVector3 Position, string ModelName, int ModelIndex,
    int Delay, int InitialScale, int ScaleStep, int VerticalScale, int GrowthTicks, int FadeRate,
    int VerticalSpeed, int VerticalSpeedRange);

/// <summary>Scoped recovered C3DExplosion model motion and scale/tint lifecycle.</summary>
public sealed class ExplosionModelState
{
    public string ModelName { get; }
    public ushort ModelIndex { get; }
    public FixedVector3 Position { get; set; }
    public FixedVector3 Scale { get; set; }
    public int VerticalVelocity { get; }
    public uint Tint { get; set; } = 0x808080;
    public ushort ItemFlags { get; private set; } = 0x601;
    public byte Stage { get; set; }
    public int Delay { get; set; }
    public int GrowthAge { get; set; }
    public int ScaleStep { get; }
    public int VerticalScale { get; }
    public int GrowthTicks { get; }
    public int FadeRate { get; }
    public bool IsDead { get; private set; }

    public ExplosionModelState(ExplosionModelParameters parameters, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(random);
        ModelName = parameters.ModelName;
        ModelIndex = unchecked((ushort)parameters.ModelIndex);
        Position = parameters.Position;
        Delay = parameters.Delay;
        Scale = new(unchecked((short)parameters.InitialScale),
            unchecked((short)(unchecked(parameters.VerticalScale * parameters.InitialScale) >> 8)), unchecked((short)parameters.InitialScale));
        VerticalScale = parameters.VerticalScale;
        ScaleStep = parameters.ScaleStep;
        GrowthTicks = parameters.GrowthTicks;
        FadeRate = parameters.FadeRate;
        if (parameters.Delay == 0) { ItemFlags &= unchecked((ushort)~1); Stage = 1; }
        // Original Rnd(0) advances RNG and returns zero; zero range is valid.
        VerticalVelocity = unchecked(-4096 * unchecked(parameters.VerticalSpeed + random(parameters.VerticalSpeedRange)));
    }

    public void Move()
    {
        switch (Stage)
        {
            case 0:
                if (Delay != 0) Delay = unchecked(Delay - 1);
                else { ItemFlags &= unchecked((ushort)~1); Stage = 1; }
                break;
            case 1:
                Advance();
                GrowthAge = unchecked(GrowthAge + 1);
                if (GrowthAge >= GrowthTicks) Stage = 2;
                break;
            case 2:
                Position = Position with { Y = unchecked(Position.Y + VerticalVelocity) };
                int value = unchecked((int)(Tint & 255) - FadeRate);
                if (value <= 0) { value = 0; IsDead = true; }
                Tint = unchecked((uint)(value | value << 16 | value << 8));
                Grow();
                break;
            default: throw new InvalidOperationException($"Unknown source C3DExplosion stage {Stage}.");
        }
    }

    private void Advance() { Position = Position with { Y = unchecked(Position.Y + VerticalVelocity) }; Grow(); }
    private void Grow()
    {
        short horizontal = unchecked((short)(Scale.X + ScaleStep));
        Scale = new(horizontal, unchecked((short)(unchecked(horizontal * VerticalScale) >> 8)), horizontal);
    }
}
