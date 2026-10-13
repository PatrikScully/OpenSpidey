using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

internal static class ActorBodyMotionTests
{
    public static void Run(Action<bool, string> assert)
    {
        VerifyFixture("actor-body", assert);
        VerifyFixture("actor-clock-motion", assert);
    }

    private static void VerifyFixture(string name, Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorBodyMotionTests).Assembly.GetManifestResourceStream($"OpenSpidey.Tests.Fixtures.{name}.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] initial = test.GetProperty("initial").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            byte[] shifts = test.GetProperty("shifts").EnumerateArray().Select(value => value.GetByte()).ToArray();
            var motion = new ActorBodyMotion
            {
                Position = Vector(0), Velocity = Vector(3), Acceleration = Vector(6), LocalVelocity = Vector(9),
                Angles = Angles(12), AngularVelocity = Angles(15), AngularAcceleration = Angles(18),
                LinearDamping = new(shifts[0], shifts[1], shifts[2]), AngularDamping = new(shifts[3], shifts[4], shifts[5]),
                UseLocalVelocity = test.GetProperty("local").GetBoolean(), MovementFlags = test.GetProperty("flag").GetUInt32(),
                CollisionFlags = 7, ScriptWaitTicks = test.GetProperty("wait").GetInt32(), LimitedMotionTicks = test.GetProperty("remaining").GetInt32()
            };
            motion.Recorded.StartPosition = motion.Position; motion.Recorded.EndPosition = new(10000, -20000, 30000);
            motion.Recorded.VelocityPerTick = new(101, -202, 303);
            motion.Recorded.StartAngles = motion.Angles; motion.Recorded.EndAngles = new(100, 200, 300);
            motion.Recorded.AngularVelocityPerTick = new(13, -27, 41);
            int mode = test.GetProperty("recorded").GetInt32();
            if (mode == 1) motion.Recorded.TotalTicks = 9;
            if (mode == 2) motion.Recorded.ElapsedTicks = 9;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                motion.Integrate(test.GetProperty("ticks").GetInt32(), test.GetProperty("limit").GetBoolean());
                long[] actual = [motion.Position.X, motion.Position.Y, motion.Position.Z,
                    motion.Velocity.X, motion.Velocity.Y, motion.Velocity.Z,
                    motion.LocalVelocity.X, motion.LocalVelocity.Y, motion.LocalVelocity.Z,
                    motion.Angles.X, motion.Angles.Y, motion.Angles.Z,
                    motion.AngularVelocity.X, motion.AngularVelocity.Y, motion.AngularVelocity.Z,
                    motion.MovementFlags, motion.CollisionFlags, motion.LimitedMotionTicks,
                    motion.Recorded.ElapsedTicks, motion.Recorded.TotalTicks, motion.ScriptWaitTicks];
                long[] target = expected.EnumerateArray().Select(value => value.GetInt64()).ToArray();
                assert(actual.SequenceEqual(target),
                    $"{name} case{caseIndex}: shared local/world physics, damping, angular order, limited and recorded motion match source; actual[{string.Join(',', actual)}], expected[{string.Join(',', target)}].");
            }
            FixedVector3 Vector(int offset) => new(initial[offset], initial[offset + 1], initial[offset + 2]);
            ActorAngles Angles(int offset) => new((short)initial[offset], (short)initial[offset + 1], (short)initial[offset + 2]);
            caseIndex++;
        }
    }
}
