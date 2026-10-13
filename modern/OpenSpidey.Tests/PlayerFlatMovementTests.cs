using System.Reflection;
using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerFlatMovementTests
{
    public static void Run(Action<bool, string> assert)
    {
        using (Stream stream = Resource("flat-movement"))
        using (JsonDocument document = JsonDocument.Parse(stream))
        {
            int index = 0;
            foreach (JsonElement fixture in document.RootElement.EnumerateArray())
            {
                int[] v = Values(fixture, "input");
                var frame = new PlayerFlatMovementFrame(new(v[0], v[1], v[2]), new((sbyte)v[3], (sbyte)v[4]),
                    (short)v[5], (short)v[6], (short)v[7], (uint)v[8], v[9], (ushort)v[10], v[11],
                    v[12] != 0, v[13] != 0, v[14] != 0, v[15] != 0, v[16] != 0, v[17] != 0, v[18] != 0);
                PlayerFlatMovementResult result = PlayerFlatMovement.Resolve(frame);
                int ramp = PlayerFlatMovement.UpdateSpeedRamp(v[9], frame.Axes, v[19]);
                int[] actual = [result.Velocity.X, result.Velocity.Y, result.Velocity.Z, result.AnimationSpeed, ramp];
                assert(actual.SequenceEqual(Values(fixture, "result")), $"Free-plane movement {index++} matches recovered finalization and ramp methods.");
            }
        }
        using (Stream stream = Resource("heading-turns"))
        using (JsonDocument document = JsonDocument.Parse(stream))
        {
            int index = 0;
            foreach (JsonElement fixture in document.RootElement.EnumerateArray())
            {
                int[] v = Values(fixture, "input");
                var turn = new PlayerHeadingTurn((short)v[0], (ushort)v[7], v[6], v[8]);
                turn = PlayerHeadingTurning.Target(turn, (short)v[1], (uint)v[2], v[3], v[4] != 0, v[5] != 0);
                turn = PlayerHeadingTurning.Advance(turn, v[9]);
                int[] actual = [turn.Heading, turn.Target, turn.RemainingTicks, turn.Step];
                assert(actual.SequenceEqual(Values(fixture, "result")), $"Heading turn {index++} matches recovered target and frame-finalization methods.");
            }
        }
        var forward = new PlayerFlatMovementFrame(new(12, 65, 34), new(-127, 0), 0, 0, 0, 16, 16, 21, 0x10000);
        assert(PlayerFlatMovement.Resolve(forward).Velocity == new FixedVector3(0, 64, -163840),
            "Full grounded movement uses the recovered scale and Q6 velocity storage.");
        assert(PlayerFlatMovement.UpdateSpeedRamp(0, new(-127, 0), 2) == 2 && PlayerFlatMovement.UpdateSpeedRamp(15, new(-127, 0), 2) == 16,
            "The speed ramp advances in elapsed ticks and caps at sixteen.");
        assert(PlayerFlatMovement.UpdateSpeedRamp(1, default, 2) == 0,
            "Centered input decreases the speed ramp to zero.");
        assert(PlayerFlatMovement.Resolve(forward with { Axes = default, Action = 1 }).Velocity == forward.Velocity,
            "Idle finalization preserves residual velocity for the following physics damping step.");
        var turnToRight = PlayerHeadingTurning.Target(new(0, 0, 0, 0), 1024, 1, 1, false, false);
        assert(turnToRight.Step == 51 && turnToRight.RemainingTicks == 20 && PlayerHeadingTurning.Advance(turnToRight, 2).Heading == 102,
            "A quarter turn respects the native per-tick cap.");
        assert(PlayerHeadingTurning.Advance(turnToRight, 20).Heading == 1024,
            "The final turn update snaps to its exact target.");
    }

    private static Stream Resource(string name) => Assembly.GetExecutingAssembly().GetManifestResourceStream($"OpenSpidey.Tests.Fixtures.{name}.json")!;
    private static int[] Values(JsonElement fixture, string name) => fixture.GetProperty(name).EnumerateArray()
        .Select(value => unchecked((int)value.GetInt64())).ToArray();
}
