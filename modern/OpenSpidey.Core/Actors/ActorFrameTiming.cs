namespace OpenSpidey.Core.Actors;

public readonly record struct ActorAnimationSnapshot(short Frame, ushort Animation, sbyte Direction);

/// <summary>Shared CBody::EveryFrame timing and pre-update animation cache, independent of scene scheduling.</summary>
public sealed class ActorFrameTiming
{
    public uint LastUpdateTick { get; set; }
    public int ElapsedTicks { get; private set; }
    public int AccumulatedTicks { get; set; }
    public short PreviousFrame { get; set; }
    public ActorAnimationSnapshot BeforeUpdateAnimation { get; set; }
    public bool TimingError { get; private set; }

    /// <summary>CSuper::UpdateFrame changes a zero elapsed value to2 after the timing accumulator has already advanced.</summary>
    public void RecordAnimationElapsed(int elapsedTicks) => ElapsedTicks = elapsedTicks;

    /// <returns>The source body flags after consuming the first-update bit.</returns>
    public ushort Prepare(uint clock, ushort bodyFlags, ushort itemFlags, ActorAnimationSnapshot animation)
    {
        bodyFlags = PrepareClock(clock, bodyFlags);
        if ((itemFlags & 2) != 0)
        {
            PreviousFrame = animation.Frame;
            BeforeUpdateAnimation = animation;
        }
        return bodyFlags;
    }

    /// <summary>Timing portion for bodies that do not own a CSuper animation cache.</summary>
    public ushort PrepareClock(uint clock, ushort bodyFlags)
    {
        TimingError = false;
        if ((bodyFlags & 4) != 0)
        {
            ElapsedTicks = 2;
            bodyFlags &= 0xFFFB;
            AccumulatedTicks = 0;
        }
        else
        {
            ElapsedTicks = unchecked((int)(clock - LastUpdateTick));
            TimingError = ElapsedTicks < 0;
            if (ElapsedTicks > 6) ElapsedTicks = 6;
        }
        LastUpdateTick = clock;
        AccumulatedTicks = unchecked(AccumulatedTicks + ElapsedTicks);
        return bodyFlags;
    }
}
