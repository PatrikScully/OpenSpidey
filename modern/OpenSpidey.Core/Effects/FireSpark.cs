using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Actual CFireySpark pixel, constructor RNG and once-per-bit gravity/ground/RGB decay.</summary>
public sealed class FireSpark
{
    public FixedVector3 Position { get; private set; }
    public FixedVector3 Velocity { get; private set; }
    public int GroundY { get; }
    public byte Code { get; }
    public uint Tag { get; }
    public uint WidthHeight { get; }
    public int ColourDecay { get; }
    public byte Red { get; private set; } = 255;
    public byte Green { get; private set; } = 128;
    public byte Blue { get; private set; }
    public uint Colour => (uint)(Red | Green << 8 | Blue << 16);
    public int DisplayWidth => (int)(WidthHeight & 15); // PC DisplayPixelList uses low nibble for both width and height.
    public bool Alive { get; private set; } = true;

    public FireSpark(FireSparkRequest request, Func<int, int> random)
    {
        Position = request.Position;
        Velocity = request.Velocity;
        GroundY = request.GroundY;
        if (random(2) != 0) { Code = 106; Tag = 0x02000000; WidthHeight = 1; }
        else
        {
            Code = 98; Tag = 0x03000000;
            int size = random(2) + 2;
            WidthHeight = unchecked((uint)((size << 16) | (size + 1)));
        }
        ColourDecay = random(10) + 10;
    }

    public void AdvanceBits()
    {
        if (!Alive) return;
        Position += Velocity;
        if (Position.Y > GroundY)
        {
            Position = Position with { Y = GroundY };
            Velocity = Velocity with { X = Velocity.X >> 1, Z = Velocity.Z >> 1 };
        }
        Velocity = Velocity with { Y = unchecked(Velocity.Y + 29584) };
        Red = (byte)Math.Max(Red - ColourDecay, 0);
        Blue = (byte)Math.Max(Blue - ColourDecay, 0);
        Green = (byte)Math.Max(Green - ColourDecay, 0);
        if ((Red | Green | Blue) == 0) Alive = false;
    }
}
