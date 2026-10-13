using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

internal static class PlatformMotionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(PlatformMotionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.platform-motion.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] initial = test.GetProperty("initial").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var motion = new PlatformMotion
            {
                Position = Vector(0), Velocity = Vector(3), Acceleration = Vector(6),
                Angles = Angles(9), AngularVelocity = Angles(12), AngularAcceleration = Angles(15)
            };
            for (int axis = 0; axis < 3; axis++)
                motion.ChangeScale(axis, test.GetProperty("percent").GetInt16(), test.GetProperty("duration").GetInt16());
            int frame = 0;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                if (frame++ != 0)
                {
                    motion.AdvanceScale(test.GetProperty("ticks").GetInt32());
                    motion.Integrate(test.GetProperty("ticks").GetInt32(), test.GetProperty("mode").GetInt16(), test.GetProperty("level").GetInt32());
                }
                int[] actual = [motion.Position.X, motion.Position.Y, motion.Position.Z,
                    motion.Velocity.X, motion.Velocity.Y, motion.Velocity.Z,
                    motion.Angles.X, motion.Angles.Y, motion.Angles.Z,
                    motion.AngularVelocity.X, motion.AngularVelocity.Y, motion.AngularVelocity.Z,
                    .. motion.Scale.Select(value => (int)value), .. motion.ScaleTicks.Select(value => (int)value),
                    .. motion.ScaleRates.Select(value => (int)value)];
                assert(actual.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt32())),
                    "Platform translation, angular wrapping, physics modes and scale clocks match recovered source updates.");
            }
            FixedVector3 Vector(int offset) => new(initial[offset], initial[offset + 1], initial[offset + 2]);
            ActorAngles Angles(int offset) => new((short)initial[offset], (short)initial[offset + 1], (short)initial[offset + 2]);
        }
        var target = new FixedVector3(10 << 12, 0, 0);
        var platform = new PlatformMotion();
        platform.MoveTo(target);
        platform.UpdateDestination(3);
        assert(platform.MovingToDestination && platform.Velocity.X > 0, "Platform destination builds source aim velocity toward its target.");
        platform.Position = new(12 << 12, 0, 0);
        platform.UpdateDestination(3);
        assert(platform.Position == target && platform.Velocity == default && !platform.MovingToDestination,
            "A platform that crosses its destination snaps and stops rather than oscillating.");
        platform.Position = new((9 << 12) + 100, 0, 0);
        platform.MoveTo(new(11 << 12, 0, 0));
        platform.UpdateDestination(2);
        assert(!platform.MovingToDestination && platform.Position.X == 11 << 12,
            "Destination arrival compares whole-unit length using the source arithmetic shift.");
    }
}
