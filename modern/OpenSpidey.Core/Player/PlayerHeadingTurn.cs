namespace OpenSpidey.Core.Player;

public readonly record struct PlayerHeadingTurn(short Heading, ushort Target, int RemainingTicks, int Step);

public static class PlayerHeadingTurning
{
    public static PlayerHeadingTurn Target(PlayerHeadingTurn turn, short wanted, uint action, int airborneTurnFactor,
        bool crawling, bool fast)
    {
        int target = wanted & 4095;
        if (target == turn.Heading) return turn with { RemainingTicks = 0 };
        int remaining = (action & 6) != 0 ? unchecked(5 * airborneTurnFactor) : 10;
        if ((action & 0x2000000) != 0) remaining <<= 1;
        if (remaining == 0) throw new InvalidDataException("Player heading turn has a zero duration.");
        int delta;
        if (target > turn.Heading)
        {
            delta = target - turn.Heading;
            if (delta >= 2048) delta = (short)(target - 4096) - turn.Heading;
        }
        else
        {
            int heading = turn.Heading;
            if (heading - target >= 2048) heading = (short)(heading - 4096);
            delta = target - heading;
        }
        int step = delta / remaining;
        int limit = (crawling ? 384 : 512) / remaining;
        if (fast) limit <<= 1;
        if (step > limit)
        {
            if (limit == 0) throw new InvalidDataException("Player heading turn has a zero rate limit.");
            step = limit;
            remaining = Math.Abs(delta / limit);
        }
        if (step < -limit)
        {
            if (limit == 0) throw new InvalidDataException("Player heading turn has a zero rate limit.");
            step = -limit;
            remaining = Math.Abs(delta / limit);
        }
        return new(turn.Heading, (ushort)target, remaining, step);
    }

    public static PlayerHeadingTurn Advance(PlayerHeadingTurn turn, int ticks)
    {
        if (turn.RemainingTicks == 0) return turn;
        if (turn.RemainingTicks > ticks)
        {
            short step = unchecked((short)(turn.Step * ticks));
            return turn with { Heading = (short)((turn.Heading + step) & 4095), RemainingTicks = unchecked(turn.RemainingTicks - ticks) };
        }
        return turn with { Heading = unchecked((short)turn.Target), RemainingTicks = 0 };
    }
}
