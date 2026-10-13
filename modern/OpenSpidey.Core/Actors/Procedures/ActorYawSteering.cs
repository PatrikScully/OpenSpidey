namespace OpenSpidey.Core.Actors.Procedures;

/// <summary>Recovered CBaddy::YawTowards, including its signed-short crossing test.</summary>
public static class ActorYawSteering
{
    public static int YawTowards(ActorBodyMotion body, int targetYaw, int rate)
    {
        ArgumentNullException.ThrowIfNull(body);
        int previousYaw = body.Angles.Y;
        int residual = unchecked(targetYaw - previousYaw);
        if (residual < -2048) residual = unchecked(residual + 4096);
        if (residual > 2048) residual = unchecked(residual - 4096);
        if (residual == 0)
        {
            StopYaw(body);
            return 0;
        }

        int step = unchecked(rate * residual) >> 8;
        short yaw = unchecked((short)(previousYaw + step));
        body.Angles = body.Angles with { Y = yaw };
        residual = unchecked(residual - step);
        if (step != 0 && (unchecked(yaw - targetYaw) > 0) != (unchecked(previousYaw - targetYaw) > 0))
            return residual;

        body.Angles = body.Angles with { Y = unchecked((short)targetYaw) };
        StopYaw(body);
        return 0;
    }

    private static void StopYaw(ActorBodyMotion body)
    {
        body.AngularAcceleration = body.AngularAcceleration with { Y = 0 };
        body.AngularVelocity = body.AngularVelocity with { Y = 0 };
    }
}
