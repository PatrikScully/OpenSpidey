namespace OpenSpidey.Core.Mathematics;

public readonly record struct AimAngles(short Pitch, short Yaw);

/// <summary>Recovered whole-unit aiming and Q12 velocity construction.</summary>
public static class FixedAim
{
    public static int Distance(FixedVector3 from, FixedVector3 to) => FixedVectorMath.OriginalDistance(from, to);

    public static AimAngles Calculate(FixedVector3 from, FixedVector3 to)
    {
        FixedVector3 direction = (to - from) >> 12;
        int x = direction.X, y = direction.Y, z = direction.Z;
        int yaw = z switch
        {
            > 0 => unchecked(2048 - Atan(-((x << 12) / z))),
            < 0 => Atan((x << 12) / z),
            _ => x > 0 ? -1024 : 1024
        };
        int horizontal = FixedVectorMath.OriginalSquareRoot(unchecked(x * x + z * z));
        int pitch;
        if (horizontal == 0) pitch = y > 0 ? 1024 : -1024;
        else if (y > 0) pitch = Atan((y << 12) / horizontal);
        else pitch = -Atan(unchecked(-4096 * y) / horizontal);
        return new((short)(pitch & 4095), (short)(yaw & 4095));
    }

    public static FixedVector3 Velocity(int magnitude, AimAngles aim)
    {
        (short pitchSine, short pitchCosine) = GameAngle.Heading(aim.Pitch);
        (short yawSine, short yawCosine) = GameAngle.Heading(aim.Yaw);
        int horizontal = unchecked(pitchCosine * magnitude) >> 12;
        return new(unchecked(-horizontal * yawSine), unchecked(pitchSine * magnitude), unchecked(-horizontal * yawCosine));
    }

    private static int Atan(int ratio) => (int)(Math.Atan(ratio / 4096.0) * 651.0006103515625);
}
