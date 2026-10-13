using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Original CChunkSmoke flat bit: animation14 frame0, contraction, rise and RGB fade once per Move.</summary>
public sealed class ManipulationChunkSmoke
{
    public FixedVector3 Position { get; private set; }
    public FixedVector3 Origin { get; }
    public int VerticalVelocity { get; }
    public int TargetScale { get; }
    public int FadeDelay { get; }
    public int ColorDecay { get; }
    public short Angle { get; private set; }
    public short AngularVelocity { get; private set; }
    public short Scale { get; private set; }
    public short Age { get; private set; }
    public uint CodeRgb { get; private set; } = 0x2E202020;
    public bool Alive { get; private set; } = true;
    public const int AnimationIndex = 14;
    public const int Frame = 0;
    public uint PostScale => 0x10001000;
    public byte SemiTransparencyRate => 32;

    public ManipulationChunkSmoke(ManipulationSmokeRequest request, Func<int, int> random)
    {
        Position = request.Position; Origin = request.Origin;
        TargetScale = unchecked(random(1000) + 1000);
        Angle = unchecked((short)random(4096));
        AngularVelocity = (short)(request.CameraSide < 0 ? -150 : 150);
        // Rnd(0) still consumes a source random call.
        ColorDecay = unchecked(random(0) + 4); FadeDelay = random(40);
        VerticalVelocity = unchecked(random(5) << 12);
    }
    public void Advance()
    {
        if (!Alive) return;
        Angle = unchecked((short)(Angle + AngularVelocity));
        AngularVelocity = unchecked((short)(AngularVelocity - (AngularVelocity >> 3)));
        Scale = unchecked((short)(Scale + (unchecked(TargetScale - Scale) >> 1)));
        Age = unchecked((short)(Age + 1));
        if (Age > FadeDelay)
        {
            Position = Position with { Y = unchecked(Position.Y - VerticalVelocity) };
            byte red = Decay((byte)CodeRgb), green = Decay((byte)(CodeRgb >> 8)), blue = Decay((byte)(CodeRgb >> 16));
            CodeRgb = (CodeRgb & 0xFF000000) | red | ((uint)green << 8) | ((uint)blue << 16);
            if ((CodeRgb & 0xFFFFFF) == 0) Alive = false;
        }
        else Position += (Origin - Position) >> 2;
    }
    private byte Decay(byte value) => value >= ColorDecay ? unchecked((byte)(value - ColorDecay)) : (byte)0;
}
