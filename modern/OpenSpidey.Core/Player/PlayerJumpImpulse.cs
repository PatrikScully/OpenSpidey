namespace OpenSpidey.Core.Player;

public readonly record struct PlayerJumpImpulse(int Velocity, int Phase, int Hold);
public readonly record struct PlayerJumpImpulseStep(PlayerJumpImpulse Impulse, int VerticalVelocity);

/// <summary>Recovered jump impulse override, fractional tail and per-update timer decay.</summary>
public static class PlayerJumpImpulseRules
{
    public static PlayerJumpImpulseStep Advance(PlayerJumpImpulse impulse, int verticalVelocity, ushort collisions, uint action, int ticks)
    {
        if ((collisions & 0x100) != 0) impulse = impulse with { Hold = 0 };
        int phase = impulse.Phase;
        if (phase > 0 || impulse.Hold > 0)
            verticalVelocity = phase < 65536 && impulse.Hold == 0
                ? (unchecked((impulse.Velocity >> 12) * phase) >> 4) & unchecked((int)0xFFFFF000)
                : impulse.Velocity;
        if ((phase & unchecked((int)0xFFFF0000)) > 0 && action == 0x200) verticalVelocity = impulse.Velocity;
        int decrement = unchecked(ticks << 16) / 2;
        return new(impulse with { Phase = Math.Max(unchecked(impulse.Phase - (impulse.Phase > 0 ? decrement : 0)), 0),
            Hold = Math.Max(unchecked(impulse.Hold - (impulse.Hold > 0 ? decrement : 0)), 0) }, verticalVelocity);
    }
}
