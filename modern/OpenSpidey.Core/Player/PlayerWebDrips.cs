using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public interface IPlayerWebDripHost
{
    uint Clock { get; }
    FixedVector3 ForwardDirection { get; }
    FixedVector3 GetPlayerHookPosition(int hook);
    void EmitLine(PlayerImpactLineParticle appearance, LineParticleMotion motion);
}

/// <summary>Actual CPlayer::CreateWebDrips: hook1 before0, component RNG and source gravity-one GLineParticles.</summary>
public static class PlayerWebDrips
{
    public static void Create(bool firstHand, bool secondHand, IPlayerWebDripHost host, Func<int, int> random)
    {
        int count = firstHand && secondHand ? 2 : 4;
        FixedVector3 direction = host.ForwardDirection;
        if (firstHand) EmitHand(1);
        if (secondHand) EmitHand(0);

        void EmitHand(int hook)
        {
            FixedVector3 position = host.GetPlayerHookPosition(hook);
            for (int index = 0; index < count; index++)
            {
                var velocity = new FixedVector3(unchecked(-(random(3) + 6) * direction.X),
                    unchecked(-(random(3) + 6) * direction.Y), unchecked(-(random(3) + 6) * direction.Z));
                var appearance = new PlayerImpactLineParticle(position, velocity, 30, 4096, 0x606060, 0, 0x02000000);
                host.EmitLine(appearance, new(position, velocity, appearance.Lifetime, appearance.VerticalAcceleration, host.Clock));
            }
        }
    }
}
