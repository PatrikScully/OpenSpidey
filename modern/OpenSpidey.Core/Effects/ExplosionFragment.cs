using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public readonly record struct ExplosionFragmentParameters(FixedVector3 Position, byte Red, byte Green, byte Blue,
    int Animation, ushort Scale, bool SemiTransparent, int Spread, int Gravity, int MaximumLifetime);

/// <summary>Original CFrag constructor and once-per-flat-bit motion, including signed byte frame and signed short age.</summary>
public sealed class ExplosionFragment
{
    public FixedVector3 Position { get; private set; }
    public FixedVector3 Velocity { get; private set; }
    public FixedVector3 Acceleration { get; }
    public int Animation { get; }
    public byte FrameCount { get; }
    public sbyte Frame { get; private set; }
    // CFrag::Move modifies mFrame without changing mpPSXFrame; renderer retains SetAnim's first frame.
    public int DisplayFrame => 0;
    public short Age { get; private set; }
    public ushort Lifetime { get; }
    public short Scale { get; }
    public uint CodeRgb { get; }
    public uint PostScale => 0x10001000;
    public byte SemiTransparencyRate => 32;
    public ushort FrigDeltaZ => 0;
    public bool Alive { get; private set; } = true;

    public ExplosionFragment(ExplosionFragmentParameters parameters, int frameCount, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (parameters.Animation is < 0 or >= 29 || frameCount is < 1 or > 255)
            throw new ArgumentOutOfRangeException(nameof(parameters), "Source fragment needs a valid sprite animation and byte frame count.");
        if (parameters.Spread is < 0 or > (int.MaxValue - 1) / 2 || parameters.MaximumLifetime < 0)
            throw new ArgumentOutOfRangeException(nameof(parameters), "Source fragment random ranges must fit their signed constructor operands.");
        Position = parameters.Position;
        Animation = parameters.Animation;
        FrameCount = (byte)frameCount;
        Scale = unchecked((short)parameters.Scale);
        CodeRgb = (parameters.SemiTransparent ? 0x2E000000u : 0x2C000000u) |
            parameters.Red | (uint)parameters.Green << 8 | (uint)parameters.Blue << 16;
        int range = parameters.Spread * 2 + 1;
        Velocity = new(unchecked((random(range) - parameters.Spread) << 12),
            unchecked(-4096 * random(parameters.Spread)), unchecked((random(range) - parameters.Spread) << 12));
        Acceleration = new(0, parameters.Gravity, 0);
        Lifetime = unchecked((ushort)random(parameters.MaximumLifetime));
    }

    public void AdvanceBits()
    {
        if (!Alive) throw new InvalidOperationException("A deleted source fragment cannot advance.");
        Position += Velocity;
        FixedVector3 velocity = Velocity + Acceleration;
        Velocity = new(unchecked(velocity.X - (velocity.X >> 3)), unchecked(velocity.Y - (velocity.Y >> 3)),
            unchecked(velocity.Z - (velocity.Z >> 3)));
        Frame = unchecked((sbyte)(Frame + 1));
        if (Frame >= FrameCount) Frame = unchecked((sbyte)(FrameCount - 1));
        Age = unchecked((short)(Age + 1));
        if (Age >= Lifetime) Alive = false;
    }
}

public interface ICrateExplosionHost
{
    bool LowMemory { get; }
    void CreateFragment(ExplosionFragmentParameters parameters);
    void CreateGlowFlash(GlowFlashParameters parameters);
}

/// <summary>Actual Exp_SmallExplosion/Exp_BigExplosion CFrag and glow leaves; unrelated 3D explosions are not substituted.</summary>
public static class CrateExplosion
{
    public static void Create(FixedVector3 position, bool small, ICrateExplosionHost host, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(random);
        if (host.LowMemory) return;
        int count = random(20);
        if (!host.LowMemory)
            for (int index = 0; index < count; index++)
                host.CreateFragment(new(position, 128, 128, 128, 6, 750, true, 80, 12288, 30));
        if (host.LowMemory) return;
        int radius = small ? 70 : 200;
        host.CreateGlowFlash(new(position, 5, 240, 200, 0, 2, 240, 200, 0, 20, 100, 0, 0,
            radius, 0, radius, 0, 0, 0));
    }
}
