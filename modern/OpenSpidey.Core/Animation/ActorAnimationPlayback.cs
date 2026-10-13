namespace OpenSpidey.Core.Animation;

/// <summary>Original RunAnim, CycleAnim and UpdateFrame behavior, independent of rendering and actor ownership.</summary>
public static class ActorAnimationPlayback
{
    public static AnimationPlaybackState Run(AnimationPlaybackState state, ushort clipId, ushort frames, int from = 0, int to = -1)
    {
        from = ResolveFrame(from, frames);
        to = ResolveFrame(to, frames);
        return state with { ClipId = clipId, FrameCount = frames, Mode = AnimationPlaybackMode.OneShot,
            TargetFrame = (short)to, Direction = (sbyte)Math.Sign(to - from), Frame = (short)from, Fraction = 0,
            Finished = (byte)(from == to ? 1 : 0) };
    }

    public static AnimationPlaybackState Cycle(AnimationPlaybackState state, ushort clipId, ushort frames, sbyte direction = 1)
    {
        if (state.ClipId != clipId)
            state = state with { ClipId = clipId, Frame = 0, Fraction = 0, FrameCount = frames, Direction = direction };
        return state with { Mode = AnimationPlaybackMode.Loop, Finished = 0 };
    }

    public static AnimationPlaybackStep Advance(AnimationPlaybackState state, int elapsedTicks)
    {
        if (elapsedTicks == 0) elapsedTicks = 2;
        int increment = unchecked(elapsedTicks * state.Speed) / 2;
        int packedFrame = state.Fraction | (state.Frame << 16);
        if (state.Direction == 1) packedFrame = unchecked(packedFrame + increment);
        if (state.Direction == -1) packedFrame = unchecked(packedFrame - increment);
        short rawFrame = (short)(packedFrame >> 16);
        short frame = rawFrame;
        byte finished = state.Finished;
        if (state.Mode == AnimationPlaybackMode.Loop)
        {
            // The original makes one correction, rather than reducing large jumps modulo the clip length.
            if (rawFrame >= state.FrameCount) frame = unchecked((short)(rawFrame - state.FrameCount));
            else if (rawFrame < 0) frame = unchecked((short)(rawFrame + state.FrameCount));
        }
        else if (state.Mode == AnimationPlaybackMode.OneShot &&
            ((state.Direction == 1 && rawFrame >= state.TargetFrame) || (state.Direction == -1 && rawFrame <= state.TargetFrame)))
        {
            frame = state.TargetFrame;
            finished = 1;
        }
        return new(state with { Frame = frame, Fraction = unchecked((ushort)packedFrame), Finished = finished }, elapsedTicks);
    }

    private static int ResolveFrame(int frame, ushort count)
    {
        if (frame == -1) frame = count - 1;
        return frame < 0 || frame >= count ? 0 : frame;
    }
}
