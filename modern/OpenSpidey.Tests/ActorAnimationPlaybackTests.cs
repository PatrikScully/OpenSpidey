using System.Reflection;
using System.Text.Json;
using OpenSpidey.Core.Animation;

internal static class ActorAnimationPlaybackTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.actor-animation.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement fixture in document.RootElement.EnumerateArray())
        {
            int[] values = Values(fixture, "state");
            var state = new AnimationPlaybackState((ushort)values[0], (short)values[1], (ushort)values[2],
                (sbyte)values[3], (AnimationPlaybackMode)values[4], (ushort)values[5], (short)values[6], (byte)values[7], values[8]);
            int[] arguments = Values(fixture, "arguments");
            int elapsed = arguments[5];
            switch (fixture.GetProperty("operation").GetInt32())
            {
                case 0: state = ActorAnimationPlayback.Run(state, (ushort)arguments[0], (ushort)arguments[1], arguments[2], arguments[3]); break;
                case 1: state = ActorAnimationPlayback.Cycle(state, (ushort)arguments[0], (ushort)arguments[1], (sbyte)arguments[4]); break;
                case 2:
                    AnimationPlaybackStep step = ActorAnimationPlayback.Advance(state, elapsed);
                    state = step.State;
                    elapsed = step.ElapsedTicks;
                    break;
            }
            int[] actual = [state.ClipId, state.Frame, state.Fraction, state.Direction, (byte)state.Mode,
                state.FrameCount, state.TargetFrame, state.Finished, state.Speed, elapsed];
            assert(actual.SequenceEqual(Values(fixture, "result")), $"Actor playback fixture {index++} matches recovered methods.");
        }

        AnimationPlaybackState initial = AnimationPlaybackState.Initial;
        AnimationPlaybackState reverse = ActorAnimationPlayback.Run(initial, 3, 4, -1, 0);
        AnimationPlaybackState half = ActorAnimationPlayback.Advance(reverse, 1).State;
        assert(reverse.Frame == 3 && reverse.Direction == -1 && !reverse.IsFinished && half.Frame == 2 && half.Fraction == 0x8000,
            "Reverse playback starts at the final frame and borrows across the Q16 fraction.");
        AnimationPlaybackState clamped = ActorAnimationPlayback.Advance(ActorAnimationPlayback.Run(initial, 3, 4, 0, 2), 5).State;
        assert(clamped.Frame == 2 && clamped.Fraction == 0x8000 && clamped.IsFinished,
            "One-shot completion clamps the integer frame while retaining its fractional clock bits.");
        AnimationPlaybackState stationary = ActorAnimationPlayback.Run(initial, 3, 4, -1, -1);
        assert(stationary.Frame == 3 && stationary.Direction == 0 && stationary.IsFinished,
            "Equal endpoints create an already completed stationary pose.");
        AnimationPlaybackStep fallback = ActorAnimationPlayback.Advance(ActorAnimationPlayback.Run(initial, 3, 4), 0);
        assert(fallback.ElapsedTicks == 2 && fallback.State.Frame == 1, "Zero elapsed ticks use the original two-tick fallback.");
        AnimationPlaybackState same = ActorAnimationPlayback.Cycle(reverse with { Fraction = 0x8000 }, 3, 9, 1);
        assert(same.Frame == 3 && same.Fraction == 0x8000 && same.Direction == -1 && same.FrameCount == 4 &&
            same.Mode == AnimationPlaybackMode.Loop && !same.IsFinished,
            "Cycling the current clip preserves frame, direction and frame count.");
        AnimationPlaybackState changed = ActorAnimationPlayback.Cycle(same, 4, 8, 1);
        assert(changed.Frame == 0 && changed.Fraction == 0 && changed.Direction == 1 && changed.FrameCount == 8 && changed.TargetFrame == same.TargetFrame,
            "Changing loop clips resets frame state while retaining the previous one-shot target.");
        AnimationPlaybackState loop = ActorAnimationPlayback.Cycle(initial, 3, 4);
        assert(ActorAnimationPlayback.Advance(loop, 11).State.Frame == 1 && ActorAnimationPlayback.Advance(loop, 19).State.Frame == 5,
            "Looping makes a single wrap correction, including an oversized jump that remains outside the clip.");
        assert(ActorAnimationPlayback.Run(initial, 3, 4, -2, 4).Frame == 0 && ActorAnimationPlayback.Run(initial, 3, 4, -2, 4).TargetFrame == 0,
            "Out-of-range endpoints resolve to zero rather than wrapping into the clip.");
    }

    private static int[] Values(JsonElement fixture, string name) =>
        fixture.GetProperty(name).EnumerateArray().Select(value => value.GetInt32()).ToArray();
}
