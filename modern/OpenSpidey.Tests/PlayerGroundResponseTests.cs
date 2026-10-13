using System.Reflection;
using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerGroundResponseTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.ground-response.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement sample in document.RootElement.EnumerateArray())
        {
            byte[] shifts = sample.GetProperty("damping").EnumerateArray().Select(value => value.GetByte()).ToArray();
            var frame = new PlayerGroundFrame(Vector(sample, "position"), Vector(sample, "initial"), Vector(sample, "velocity"),
                sample.GetProperty("collisions").GetUInt16(), sample.GetProperty("clearance").GetUInt16(),
                sample.GetProperty("holding").GetBoolean(), new(shifts[0], shifts[1], shifts[2]));
            var contact = new PlayerGroundContact(Vector(sample, "contact"), Vector(sample, "normal"),
                sample.GetProperty("distance").GetInt32(), sample.GetProperty("flags").GetUInt32(),
                sample.GetProperty("support").GetBoolean());
            PlayerGroundResponse response = PlayerGroundResponder.Resolve(frame, contact, sample.GetProperty("fall").GetInt32(),
                sample.GetProperty("savedVelocity").GetInt32(), sample.GetProperty("limit").GetBoolean());
            int[] actual = [response.Position.X, response.Position.Y, response.Position.Z,
                response.Velocity.X, response.Velocity.Y, response.Velocity.Z, response.CollisionFlags,
                response.GroundContactGraceUpdates, response.SurfaceNormal.X, response.SurfaceNormal.Y, response.SurfaceNormal.Z,
                response.ShadowPosition.X, response.ShadowPosition.Y, response.ShadowPosition.Z,
                response.NotifySupport ? 1 : 0, response.UseAlternateBrightness ? 1 : 0];
            int[] expected = sample.GetProperty("result").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            assert(actual.SequenceEqual(expected), $"Ground response {index++} matches the recovered controller method.");
        }

        var input = new PlayerGroundFrame(new(100, 200, 300), new(10, 20, 30), new(1, 2, 3), 0x40, 96, false, new(1, 4, 1));
        var floor = new PlayerGroundContact(new(100, -1, 300), new(0, -4096, 0), 10, 0x800000, true);
        PlayerGroundResponse landed = PlayerGroundResponder.Resolve(input, floor, 8192, 100, true);
        assert(landed.Position == new FixedVector3(100, -397312, 300) && landed.Velocity == new FixedVector3(1, 0, 3),
            "Floor landings quantize negative height downward, apply surface clearance and stop vertical velocity.");
        assert(landed.ShadowPosition.Y == -4096 && landed.CollisionFlags == 0x42,
            "Landing records the quantized shadow contact and preserves existing collision bits.");
        assert(landed.GroundContactGraceUpdates == 4 && landed.SurfaceNormal == new FixedVector3(0, -4096, 0) &&
            landed.NotifySupport && landed.UseAlternateBrightness, "Contact response carries support, brightness and grace state to adapters.");

        PlayerGroundResponse restricted = PlayerGroundResponder.Resolve(input with { HoldingObject = true },
            floor with { SurfaceFlags = 0x4000000 }, 8192, 100, true);
        assert(restricted.Position == input.InitialPosition && restricted.Velocity == input.Velocity,
            "A carried object on restricted ground restores the whole initial position without changing velocity.");
        assert(restricted.ShadowPosition == floor.Position && restricted.NotifySupport && !restricted.UseAlternateBrightness,
            "Restricted ground still updates the unquantized shadow and support notifications.");

        var slope = floor with { Normal = new(4096, -2600, 2048), Position = new(100, 409600, 300), SurfaceFlags = 0, SupportsPlayer = false };
        PlayerGroundResponse sliding = PlayerGroundResponder.Resolve(input, slope, 8193, 100, true);
        assert(sliding.Position == new FixedVector3(4196, 24065, 2348) && sliding.Velocity == new FixedVector3(1, 100, 3),
            "A normal at the exact threshold takes the slope branch and damps fall independently per axis.");
        assert(sliding.CollisionFlags == input.CollisionFlags, "Slope response does not invent a grounded collision bit.");
        PlayerGroundResponse limited = PlayerGroundResponder.Resolve(input, slope with { Distance = 0 }, 8192, 100, true);
        PlayerGroundResponse unlimited = PlayerGroundResponder.Resolve(input, slope with { Distance = 0 }, 8192, 100, false);
        PlayerGroundResponse upward = PlayerGroundResponder.Resolve(input, slope with { Distance = 0 }, -8192, 100, true);
        assert(limited.Position.Y == 200 && unlimited.Position.Y == 8392 && upward.Position.Y == -7992,
            "The vertical policy limits only nonnegative fall at a nonpositive-distance hit.");
    }

    private static FixedVector3 Vector(JsonElement sample, string property)
    {
        int[] values = sample.GetProperty(property).EnumerateArray().Select(value => value.GetInt32()).ToArray();
        return new(values[0], values[1], values[2]);
    }
}
