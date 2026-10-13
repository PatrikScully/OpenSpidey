using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Combat;

public enum GrenadeTargetList { Baddies, Player }
public enum GrenadeDamageList { PrimaryTargets, EnvironmentalBodies, Player }
public readonly record struct GrenadeSweep(bool HasItem, int Distance, FixedVector3 Position, FixedVector3 Normal);
public readonly record struct GrenadeDamageRequest(GrenadeDamageList List, FixedVector3 Position,
    ushort Damage, ushort Radius, int SlideDistance, int SlideFrames, int AttackKind);

public interface IGrenadeProjectileHost
{
    int Timer { get; }
    int TotalTime { get; }
    int LevelId { get; }
    byte ObjectFileRegion { get; }
    byte DefaultGrenadeRegion { get; }
    void AttachProjectile(GrenadeProjectile projectile);
    void LoadModel(uint checksum, byte region);
    GrenadeSweep Sweep(FixedVector3 start, FixedVector3 end, bool lineOfSightCheck);
    int GroundHeight(FixedVector3 position, int above, int below);
    void SetShadow(bool enabled, FixedVector3 position, ushort scale, short distance);
    void CreateGlow(int sectors, int bands);
    void SetGlowSkipTriangles(bool skip);
    void SetGlowFringeWidth(int index, int width);
    void SetGlowCentreRgb(byte red, byte green, byte blue);
    void SetGlowRgb(byte red, byte green, byte blue);
    void SetGlowFringeRgb(int index, byte red, byte green, byte blue);
    void SetGlowAngle(int angle);
    void SetGlowRadius(int radius);
    void SetGlowPosition(FixedVector3 position);
    void SetGlowProtected(bool protect);
    void PlaySound(uint sound, FixedVector3 position, int pitchOffset);
    void GiveScaledDamageToObjects(GrenadeDamageRequest request);
    void GiveScaledDamageToEnviro(FixedVector3 position, ushort damage, ushort radius);
    void CreateGrenadeExplosion(FixedVector3 position);
    void CreateGlowFlash(FixedVector3 position, IReadOnlyList<int> arguments);
    void MarkDead();
    void DestroyGlow();
    void DetachProjectile();
}

/// <summary>Recovered CGrenade trajectory, bounce, fuse, explosion and glow lifecycle.</summary>
public sealed class GrenadeProjectile : IDisposable
{
    private readonly IGrenadeProjectileHost host;
    private readonly Func<int, int> random;
    private bool disposed;
    private static readonly IReadOnlyList<int> ExplosionFlashArguments = Array.AsReadOnly(
        new[] { 5, 255, 255, 255, 1, 128, 100, 0, 1, 60, 0, 1, 100, 120, 80, 100, 10, 4 });

    public FixedVector3 Position { get; set; }
    public FixedVector3 Velocity { get; set; }
    public ActorAngles Angles { get; set; }
    public FixedVector3 ShadowPosition { get; set; }
    public short ShadowDistance { get; set; }
    public ushort ShadowScale { get; set; } = 32;
    public bool ShadowEnabled { get; private set; }
    public int StartTime { get; set; }
    public byte Settled { get; set; }
    public byte Red { get; set; } = 100;
    public byte Green { get; set; } = 50;
    public byte Blue { get; set; }
    public byte ExplodeOnContact { get; }
    public byte Flag { get; }
    public ushort Damage { get; }
    public ushort Radius { get; }
    public GrenadeTargetList Targets { get; }
    public int Type => Targets == GrenadeTargetList.Player ? 63 : 64;
    public ushort BodyFlags { get; set; }
    public ushort ItemFlags { get; } = 0x200;
    public FixedVector3 ModelScale { get; } = new(2048, 2048, 2048);
    public short ShadowThreshold { get; } = 300;
    public int AttackKind { get; } = 29;
    public uint ModelChecksum { get; }
    public byte Region { get; }
    public bool IsDead => (BodyFlags & 0x40) != 0;

    public GrenadeProjectile(FixedVector3 start, FixedVector3 target, int speed, int damage, int radius,
        GrenadeTargetList targets, uint checksum, byte contact, byte flag,
        IGrenadeProjectileHost host, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(random);
        if (targets is not (GrenadeTargetList.Baddies or GrenadeTargetList.Player))
            throw new ArgumentOutOfRangeException(nameof(targets));
        if (unchecked((ushort)radius) == 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Source blast falloff requires a nonzero stored radius.");
        int dx = unchecked(target.X - start.X) >> 12;
        int dz = unchecked(target.Z - start.Z) >> 12;
        int distance = FixedVectorMath.OriginalSquareRoot(unchecked(dx * dx + dz * dz));
        if (distance != 0 && unchecked(2 * speed) == 0)
            throw new ArgumentOutOfRangeException(nameof(speed), "Source ballistic division requires a nonzero doubled speed.");
        this.host = host;
        this.random = random;
        Damage = unchecked((ushort)damage);
        Radius = unchecked((ushort)radius);
        Targets = targets;
        ExplodeOnContact = contact;
        Flag = flag;
        BodyFlags = (ushort)(0x16 & ~2);
        if (targets == GrenadeTargetList.Player) BodyFlags = (ushort)(BodyFlags & ~0x10);
        ModelChecksum = checksum != 0 ? checksum : 0x51E46AAF;
        Region = checksum != 0 ? host.ObjectFileRegion : host.DefaultGrenadeRegion;
        host.AttachProjectile(this);
        host.LoadModel(ModelChecksum, Region);
        StartTime = host.Timer;
        if (host.LevelId == 0x1101) StartTime = unchecked(StartTime + random(480) + 120);
        host.CreateGlow(4, 2);
        host.SetGlowSkipTriangles(true);
        host.SetGlowFringeWidth(0, 16);
        host.SetGlowFringeWidth(1, 16);
        host.SetGlowCentreRgb(0, 0, 0);
        host.SetGlowRgb(0, 0, 0);
        host.SetGlowFringeRgb(0, Red, Green, Blue);
        host.SetGlowFringeRgb(1, 0, 0, 0);
        host.SetGlowAngle(random(1024));
        host.SetGlowRadius(0);
        // CommonInit precedes assigning start: source glow initially sits at zero.
        host.SetGlowPosition(Position);
        host.SetGlowProtected(true);
        Position = start;
        if (distance != 0)
        {
            int horizontalSpeed = unchecked(9 * speed) / 10;
            Velocity = new(unchecked(horizontalSpeed * ((dx << 12) / distance)),
                unchecked((unchecked(speed * (unchecked(target.Y - start.Y) >> 12)) / distance -
                    unchecked(8 * distance) / unchecked(2 * speed)) << 12),
                unchecked(horizontalSpeed * ((dz << 12) / distance)));
        }
        else Velocity = new(0, -81920, 0);
    }

    public void Update()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        bool detonate = false;
        Position = Position with { Y = unchecked(Position.Y + 81920) };
        if (Settled == 0)
        {
            int speed = Length(Velocity);
            GrenadeSweep collision = host.Sweep(Position, Position + Velocity * 2, true);
            Position += Velocity;
            Velocity = Velocity with { Y = unchecked(Velocity.Y + 0x8000) };
            if (collision.HasItem && collision.Distance <= speed)
            {
                Position = collision.Position;
                host.PlaySound(0x2A, Position, 0);
                detonate = ExplodeOnContact != 0;
                FixedVector3 v = Velocity;
                FixedVector3 n = collision.Normal;
                int dot = unchecked(n.X * (v.X >> 6) + n.Y * (v.Y >> 6) + n.Z * (v.Z >> 6)) >> 12;
                Velocity = new(unchecked(v.X - (unchecked(dot * n.X) >> 6)),
                    unchecked(v.Y - (unchecked(dot * n.Y) >> 6)), unchecked(v.Z - (unchecked(dot * n.Z) >> 6)));
                Velocity >>= 1;
                Velocity = new(unchecked(Velocity.X - (unchecked(dot * n.X) >> 7)),
                    unchecked(Velocity.Y - (unchecked(dot * n.Y) >> 7)), unchecked(Velocity.Z - (unchecked(dot * n.Z) >> 7)));
                Velocity += new FixedVector3(unchecked((random(11) - 5) << 12), 0, unchecked((random(11) - 5) << 12));
                if (Velocity.Y > -61440 && n.Y < -3500)
                {
                    Settled = 1;
                    if (host.LevelId == 0x1101)
                    {
                        int height = host.GroundHeight(Position, 50, 50);
                        Velocity = default;
                        if (height != -1) Position = Position with { Y = unchecked(height + 81920) };
                    }
                }
            }
            short pitch = unchecked((short)(Angles.X + unchecked(410 * Length(Velocity)) / 128));
            int vx = Velocity.X >> 12, vz = Velocity.Z >> 12;
            int yaw = vz > 0 ? unchecked(2048 - Atan(unchecked(-((vx << 12) / vz)))) :
                vz < 0 ? Atan((vx << 12) / vz) : vx > 0 ? -1024 : 1024;
            Angles = new(pitch, unchecked((short)yaw), Angles.Z);
        }
        GrenadeSweep shadow = host.Sweep(Position, Position + new FixedVector3(0, 0xBB8000, 0), false);
        if (shadow.HasItem)
        {
            ShadowPosition = shadow.Position;
            ShadowDistance = unchecked((short)(unchecked(shadow.Position.Y - Position.Y) >> 12));
            ShadowScale = unchecked((ushort)(50 - 21 * ShadowDistance / 128));
            ShadowEnabled = ShadowScale != 0;
        }
        else ShadowEnabled = false;
        if (ShadowEnabled) BodyFlags |= 8;
        else BodyFlags = (ushort)(BodyFlags & ~8);
        host.SetShadow(ShadowEnabled, ShadowPosition, ShadowScale, ShadowDistance);
        int age = unchecked(host.Timer - StartTime);
        if (age > 90)
        {
            bool bright = (host.TotalTime & 2) != 0;
            host.SetGlowFringeRgb(0, bright ? Double(Red) : Red, bright ? Double(Green) : Green, bright ? Double(Blue) : Blue);
        }
        if (age > 120 || detonate)
        {
            DamageObjects(GrenadeDamageList.PrimaryTargets);
            DamageObjects(GrenadeDamageList.EnvironmentalBodies);
            if (Targets != GrenadeTargetList.Player) DamageObjects(GrenadeDamageList.Player);
            host.GiveScaledDamageToEnviro(Position, Damage, Radius);
            for (int i = 0; i < 2; i++)
                host.CreateGrenadeExplosion(Position + new FixedVector3(random(50) << 12, random(50) << 12, random(50) << 12));
            host.PlaySound(0x8029, Position, 0);
            host.CreateGlowFlash(Position, ExplosionFlashArguments);
            BodyFlags |= 0x40;
            host.MarkDead();
        }
        Position = Position with { Y = unchecked(Position.Y - 81920) };
        host.SetGlowPosition(Position);

        void DamageObjects(GrenadeDamageList list) => host.GiveScaledDamageToObjects(new(list, Position, Damage, Radius, 100, 10, 29));
    }

    public void Dispose()
    {
        if (disposed) return;
        host.DestroyGlow();
        host.DetachProjectile();
        disposed = true;
    }

    private static byte Double(byte value) => value <= 127 ? (byte)(value * 2) : (byte)255;
    private static int Atan(int ratio) => (int)(Math.Atan(ratio / 4096.0) * 651.0006103515625);
    private static int Length(FixedVector3 velocity) => FixedAim.Distance(velocity, default);
}
