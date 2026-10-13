using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public readonly record struct PlayerCombatImpactContext(byte FireWebbing, int KickDamageCharges, ushort Animation);
public readonly record struct PlayerImpactLineParticle(FixedVector3 Position, FixedVector3 Velocity, ushort Lifetime,
    int VerticalAcceleration, uint StartRgb, uint EndRgb, uint CodeFlags)
{
    public uint StartCodeBgr => 0x50000000 | StartRgb | CodeFlags;
    public uint EndCodeBgr => EndRgb;
}

public interface IPlayerCombatImpactHost
{
    uint Clock { get; }
    int GetGroundY(FixedVector3 position);
    void EmitBouncingRock(FixedVector3 position, int groundY, uint textureSelector, Func<int, int> random);
    void EmitRing(ThugCombatImpactRing ring);
    void EmitLine(PlayerImpactLineParticle particle, LineParticleMotion motion);
}

/// <summary>Actual CPlayer::CreateCombatImpactEffect red ring banks, ordered rock requests and five-draw particle RNG.</summary>
public static class PlayerCombatImpact
{
    public static void Create(PlayerCombatImpactContext context, FixedVector3 position, int kind,
        IPlayerCombatImpactHost host, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(host); ArgumentNullException.ThrowIfNull(random);
        if (context.FireWebbing != 0 && context.KickDamageCharges != 0 && context.Animation is 100 or 102 or 104 or 106)
        {
            int groundY = host.GetGroundY(position);
            for (int rock = 0; rock < 10; rock++) host.EmitBouncingRock(position, groundY, 0x28001F00, random);
        }
        int particles;
        if (kind == 0)
        {
            particles = 6;
            Ring(108, 18, 18, 384, 1536, 144); Ring(144, 72, 72, 192, 768, 72);
        }
        else if (kind == 1)
        {
            particles = 12;
            Ring(108, 18, 18, 384, 1792, 144); Ring(144, 72, 72, 192, 896, 72);
        }
        else if (kind == 2)
        {
            particles = 12;
            Ring(108, 25, 25, 512, 1792, 128); Ring(162, 101, 101, 256, 896, 64);
        }
        else return;
        for (int index = 0; index < particles; index++)
        {
            int speed = unchecked(random(3) + 8);
            FixedVector3 velocity = new(unchecked(speed * (random(4096) - 2048)), unchecked(speed * (random(4096) - 2048)),
                unchecked(speed * (random(4096) - 2048)));
            ushort lifetime = unchecked((ushort)(random(7) + 12));
            var particle = new PlayerImpactLineParticle(position, velocity, lifetime, 0, 0xA0, 0x20, 0x02000000);
            host.EmitLine(particle, new(position, velocity, lifetime, 0, host.Clock));
        }
        void Ring(byte red, byte green, byte blue, int initial, int maximum, int growth) =>
            host.EmitRing(new(position, red, green, blue, initial, maximum, growth, host.Clock, random));
    }
}
