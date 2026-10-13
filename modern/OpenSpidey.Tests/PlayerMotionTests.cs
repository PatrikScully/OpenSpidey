using System.Reflection;
using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerMotionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.player-motion.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement sample in document.RootElement.EnumerateArray())
        {
            FixedVector3 velocity = Vector(sample, "velocity");
            FixedVector3 acceleration = Vector(sample, "acceleration");
            FixedVector3 normal = Vector(sample, "normal");
            byte[] shifts = sample.GetProperty("damping").EnumerateArray().Select(value => value.GetByte()).ToArray();
            var damping = new AxisDamping(shifts[0], shifts[1], shifts[2]);
            int ticks = sample.GetProperty("elapsedTicks").GetInt32();
            int reach = sample.GetProperty("reach").GetInt32();
            int length = sample.GetProperty("length").GetInt32();
            int horizontalLength = sample.GetProperty("horizontalLength").GetInt32();
            var actual = new List<int>();
            Append(PlayerMotion.UpdateVelocity(velocity, acceleration, damping));
            Append(PlayerMotion.Displacement(velocity, ticks));
            Append(PlayerMotion.CollisionFreeDisplacement(velocity, ticks));
            Append(PlayerMotion.VelocityFromDisplacement(velocity, ticks));
            Append(PlayerMotion.SlideHorizontally(velocity, normal));
            actual.Add(PlayerMotion.FallDisplacement(velocity.Y, ticks));
            actual.Add(PlayerMotion.DampedValue(velocity.X, damping.X));
            actual.Add(PlayerSweepGeometry.ScaledSquaredLength(velocity));
            actual.Add(PlayerSweepGeometry.ScaledSquaredLength(velocity, horizontalOnly: true));
            actual.Add(PlayerSweepGeometry.ReachForTicks(reach, ticks));
            Append(PlayerSweepGeometry.MainRay(velocity, reach, ticks, length));
            Append(PlayerSweepGeometry.ShoulderSide(velocity, reach, length));
            Append(PlayerSweepGeometry.ShoulderForward(velocity, reach, ticks, length, horizontalLength));
            int[] expected = sample.GetProperty("result").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            assert(actual.SequenceEqual(expected), $"Movement/sweep sample {index++} matches recovered C++ word arithmetic.");
            PlayerMovementStep step = PlayerMotion.IntegrateCollisionFree(new(10, 20, 30), velocity, acceleration, damping, ticks);
            assert(step.Position == Vector(sample, "collisionFreePosition") &&
                step.Velocity == new FixedVector3(expected[0], expected[1], expected[2]),
                "Complete collision-free integration preserves reference velocity and position.");
            assert(FixedVectorMath.Normalize(velocity) == Vector(sample, "normalizedVelocity") &&
                FixedVectorMath.OriginalSquareRoot(PlayerSweepGeometry.ScaledSquaredLength(velocity)) == sample.GetProperty("originalLength").GetInt32(),
                "Vector normalization and overflowed sweep lengths match recovered C++.");

            void Append(FixedVector3 value) => actual.AddRange([value.X, value.Y, value.Z]);
        }

        var oddVelocity = new FixedVector3(4097, -4097, 8192);
        assert(PlayerMotion.Displacement(oddVelocity, 3) == new FixedVector3(6145, -6146, 12288),
            "Negative odd velocities use arithmetic shifts when computing elapsed-tick displacement.");
        assert(PlayerMotion.CollisionFreeDisplacement(oddVelocity, 3) == new FixedVector3(8194, -8194, 16384),
            "The collision-free zip window uses its distinct full-velocity tick multiplier.");
        assert(PlayerMotion.VelocityFromDisplacement(new(7, -7, 8), 3) == new FixedVector3(4, -4, 5),
            "Conversion back to velocity truncates signed division toward zero.");
        assert(PlayerMotion.SlideHorizontally(new(-4096, 8000, 8192), new(4096, 0, 0)) == new FixedVector3(0, 8000, 2048),
            "Wall slides retain vertical movement and damp tangential motion by one quarter.");
        assert(PlayerMotion.SlideHorizontally(new(4096, 8000, 8192), new(4096, 0, 0)) == new FixedVector3(4096, 8000, 8192),
            "Movement away from the wall remains unchanged.");
        assert(PlayerMotion.UpdateVelocity(new(-2048, 2048, 2049), default, new(31, 31, 31)) == new FixedVector3(0, 0, 2049),
            "Damping precedes the inclusive small-velocity dead zone.");
        assert(PlayerMotion.UpdateVelocity(new(int.MaxValue, 0, 0), new(1, 0, 0), new(31, 31, 31)).X == -2147483647,
            "Velocity addition wraps before damping.");
        assert(PlayerMotion.DampedValue(-5, 1) == -2 && PlayerMotion.DampedValue(5, 32) == 0,
            "Damping preserves negative rounding and x86 shift-count masking.");
        assert(PlayerSweepGeometry.MainRay(new(4096, 0, 0), 70, 2, 8) == new FixedVector3(322560, 0, 0),
            "The forward collision ray includes the recovered extra one-eighth reach.");
        assert(PlayerSweepGeometry.ShoulderSide(new(4096, 0, 0), 70, 8) == new FixedVector3(0, 0, -143360),
            "Shoulder offsets preserve source handedness.");
        assert(PlayerSweepGeometry.ShoulderForward(new(4096, 4096, 0), 70, 2, 12, 8) == new FixedVector3(286720, 191146, 0),
            "Shoulder reach uses separate horizontal and full-vector lengths.");
    }

    private static FixedVector3 Vector(JsonElement sample, string property)
    {
        int[] components = sample.GetProperty(property).EnumerateArray().Select(value => value.GetInt32()).ToArray();
        return new(components[0], components[1], components[2]);
    }
}
