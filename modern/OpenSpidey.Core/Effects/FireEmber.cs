using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Actual CEmber and its shared PC CShellEmber::Move: source spiral, delayed RGB decay and final flicker.</summary>
public sealed class FireEmber
{
    public FixedVector3 Position { get; private set; }
    public FixedVector3 Origin { get; }
    public FixedVector3 Velocity { get; }
    public short Scale { get; }
    public int Amplitude { get; }
    public int OrbitPhase { get; private set; }
    public int AmplitudePhase { get; private set; }
    public int FadeDelay { get; private set; }
    public int Red { get; private set; } = 255;
    public int Green { get; private set; } = 128;
    public int Blue { get; private set; }
    public uint CodeRgb { get; private set; } = 0x2E0080FF;
    public bool Alive { get; private set; } = true;
    public short Angle => 0;
    public uint PostScale => 0x10001000;

    public FireEmber(FixedVector3 position, int scale, Func<int, int> random)
    {
        Position = position;
        Origin = new(position.X, 0, position.Z);
        Amplitude = random(10) + 10;
        OrbitPhase = random(4096);
        AmplitudePhase = random(4096);
        Scale = unchecked((short)(random(200) + 350));
        FadeDelay = unchecked(scale * (random(5) + 5)) >> 8;
        Velocity = new(0, unchecked(scale * (random(5) + 6) << 12) >> 8, 0);
    }

    public void AdvanceBits(Func<int, int> random)
    {
        if (!Alive) return;
        int amplitude = unchecked(Amplitude * GameAngle.Heading(AmplitudePhase).Sine) >> 12;
        AmplitudePhase = unchecked(AmplitudePhase + 100);
        (short sine, short cosine) = GameAngle.Heading(OrbitPhase);
        Position = new(unchecked(Origin.X + amplitude * sine), unchecked(Position.Y - Velocity.Y),
            unchecked(Origin.Z + amplitude * cosine));
        OrbitPhase = unchecked(OrbitPhase + 100);
        if (FadeDelay != 0) FadeDelay = unchecked(FadeDelay - 1);
        else
        {
            Red = Math.Max(Red - 15, 0); Green = Math.Max(Green - 15, 0); Blue = Math.Max(Blue - 15, 0);
            if ((Red | Green | Blue) == 0) Alive = false;
        }
        // Original still consumes this RNG and writes tint on the update that calls Die.
        int flicker = random(256);
        int red = unchecked((ushort)(flicker * Red)) >> 8;
        int green = unchecked((ushort)(flicker * Green)) >> 8;
        int blue = unchecked((ushort)(flicker * Blue)) >> 8;
        CodeRgb = (CodeRgb & 0xFF000000u) | (uint)(red | green << 8 | blue << 16);
    }
}
