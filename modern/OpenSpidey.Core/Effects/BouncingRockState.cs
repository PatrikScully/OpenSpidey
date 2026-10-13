using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Original PC CBouncingRock (43B550/43B6B0), updated once per flat-bit phase.</summary>
public sealed class BouncingRockState
{
    public const uint FireSelector = 0x28001F00;
    public const uint DebrisSelector = 0x3288E271;
    public FixedVector3 Position { get; private set; }
    public FixedVector3 Velocity { get; private set; }
    public int GroundY { get; }
    public int BounceCount { get; private set; }
    public ushort Lifetime { get; private set; }
    public short Scale { get; }
    public short Angle { get; private set; }
    public short AngularVelocity { get; }
    public int TextureAnimation => 14;
    public int TextureFrame { get; }
    public byte SemiTransparencyRate { get; }
    public uint CodeBgr => 0x2C808080;
    public uint PostScale => 0x0C001000;
    public bool Alive { get; private set; } = true;

    public BouncingRockState(FixedVector3 position, int groundY, uint selector, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(random);
        // Other source selectors leave an uninitialized texture; reject before consuming RNG.
        TextureFrame = selector switch { FireSelector => 5, DebrisSelector => 2, _ => throw new ArgumentOutOfRangeException(nameof(selector)) };
        SemiTransparencyRate = selector == DebrisSelector ? (byte)0 : (byte)32;
        Position = position; GroundY = groundY;
        Scale = unchecked((short)(random(200) + 350));
        short phase = unchecked((short)random(4096));
        int speed = unchecked(random(10) + 10);
        (short sine, short cosine) = GameAngle.Heading(phase);
        Velocity = new(unchecked(speed * sine), unchecked(-81920 - (random(20) << 12)), unchecked(speed * cosine));
        AngularVelocity = random(2) != 0 ? (short)-500 : (short)500;
    }

    public void Advance()
    {
        if (!Alive) throw new InvalidOperationException("A deleted source flat bit cannot be updated.");
        Position += Velocity;
        if (Position.Y > GroundY)
        {
            Position = Position with { Y = GroundY };
            // Original shl32 7 then sar32 8 preserves signed wrap before halving.
            Velocity = new(unchecked(Velocity.X << 7) >> 8, unchecked(-(unchecked(Velocity.Y << 7) >> 8)), unchecked(Velocity.Z << 7) >> 8);
            BounceCount = unchecked(BounceCount + 1);
        }
        Angle = unchecked((short)(Angle + AngularVelocity));
        Velocity = Velocity with { Y = unchecked(Velocity.Y + 29584) };
        if (BounceCount >= 5) Alive = false;
        Lifetime = unchecked((ushort)(Lifetime + 1));
        if (Lifetime > 60) Alive = false;
    }
}

public interface IBouncingRockHost
{
    /// <summary>M3dZone_LineToItem with source flags 1; null means no original item hit.</summary>
    FixedVector3? TraceGround(FixedVector3 from, FixedVector3 to, int flags);
    void EmitRock(BouncingRockState rock);
}

public static class BouncingRockEffects
{
    public static int GetGroundY(FixedVector3 position, IBouncingRockHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        FixedVector3 end = position with { Y = unchecked(position.Y + 0x1388000) };
        return host.TraceGround(position, end, 1)?.Y ?? end.Y;
    }

    /// <summary>Effects_MakeRocks: one ground query followed by ten constructors, in source order.</summary>
    public static void MakeRocks(FixedVector3 position, uint selector, IBouncingRockHost host, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(host); ArgumentNullException.ThrowIfNull(random);
        if (selector is not BouncingRockState.FireSelector and not BouncingRockState.DebrisSelector) throw new ArgumentOutOfRangeException(nameof(selector));
        int ground = GetGroundY(position, host);
        for (int i = 0; i < 10; i++) host.EmitRock(new(position, ground, selector, random));
    }
}
