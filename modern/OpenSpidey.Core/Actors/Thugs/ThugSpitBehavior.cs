using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

/// <summary>Source CGLineParticle constructor and color/code updates from MonitorSpitPlease.</summary>
public readonly record struct ThugSpitParticle(FixedVector3 Position, FixedVector3 Velocity, ushort Lifetime,
    int VerticalAcceleration, uint StartRgb, uint EndRgb, uint AdditionalCodeFlags);

public interface IThugSpitHost
{
    int AnimationFrame { get; }
    ushort ItemFlags { get; }
    FixedVector3 Position { get; }
    FixedVector3 PlayerPosition { get; }
    ActorAngles Angles { get; }
    FixedVector3 GetDynamicHookPosition(ActorHook hook);
    void PlayPositionSound(ushort sound, FixedVector3 position, int volume);
    void EmitSpitParticle(ThugSpitParticle particle);
}

/// <summary>Recovered CThug::MonitorSpitPlease; caller leaves the spit substate when this returns true.</summary>
public static class ThugSpitBehavior
{
    public static bool Monitor(IThugSpitHost host, Func<int, int> random)
    {
        if (host.AnimationFrame < 30) return false;
        if ((host.ItemFlags & 0x8000) != 0 && ApproximateDistance(host.Position, host.PlayerPosition) < 4000)
        {
            host.PlayPositionSound(0x8011, host.Position, 0);
            ActorAngles angles = host.Angles;
            short yaw = unchecked((short)(angles.Y + 1024));
            FixedVector3 direction = FixedAim.Velocity(-256, new(angles.X, yaw)) >> 8;
            FixedVector3 mouth = host.GetDynamicHookPosition(new(0, 20, -100, 13));
            for (int index = 0; index < 6; index++)
            {
                // Two independent Rnd(3) calls per particle, X before Z; pitch does not add vertical velocity.
                int x = unchecked(direction.X * (random(3) + 4));
                int z = unchecked(direction.Z * (random(3) + 4));
                host.EmitSpitParticle(new(mouth, new(x, 0, z), 20, 4096, 0x306030, 0, 0x02000000));
            }
        }
        // Hidden and distant actors still complete their spit state at frame 30.
        return true;
    }

    private static uint ApproximateDistance(FixedVector3 first, FixedVector3 second)
    {
        FixedVector3 difference = first - second;
        int largest = Abs(difference.X), middle = Abs(difference.Y), smallest = Abs(difference.Z);
        if (largest < middle) (largest, middle) = (middle, largest);
        if (middle < smallest) (middle, smallest) = (smallest, middle);
        if (largest < middle) (largest, middle) = (middle, largest);
        return unchecked((uint)(unchecked(largest + (middle >> 1) + (smallest >> 2)) >> 12));
    }

    private static int Abs(int value) => value < 0 ? unchecked(-value) : value;
}
