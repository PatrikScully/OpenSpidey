using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Audio;

public readonly record struct SoundListener(FixedVector3 WholePosition);
public readonly record struct StereoAttenuation(int Left, int Right);

/// <summary>Original PC listener distance and Q12 stereo attenuation using world axes.</summary>
public static class SpatialSound
{
    public static StereoAttenuation Calculate(FixedVector3 position, int near, int far, SoundListener listener)
    {
        int distance = unchecked((int)ActorLifecycle.Distance(position, listener.WholePosition << 12));
        if (distance <= near) return new(4095, 4095);
        if (distance >= far) return default;
        int attenuation = unchecked((far - distance) * 4095) / far;
        FixedVector3 direction = (position >> 12) - listener.WholePosition;
        // PC gte_rtir writes the rotated result to a different register than the caller subsequently reads.
        if ((direction.X | direction.Z) == 0) return new(attenuation, attenuation);
        int angle = (1024 - GameAngle.Atan2(direction.Z, direction.X)) & 4095;
        int difference = unchecked(GameAngle.Heading(angle).Sine * (attenuation / 2)) >> 12;
        return angle < 2048 ? new(attenuation - difference, attenuation) : new(attenuation, attenuation + difference);
    }
}
