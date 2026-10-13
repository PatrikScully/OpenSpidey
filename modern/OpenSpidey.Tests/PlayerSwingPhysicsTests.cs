using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerSwingPhysicsTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerSwingPhysicsTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-swing-physics.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
            var world = new Probe((int)input[12], new(new(Vector(13), Vector(16), 0, 0, false), 0, 0, 0));
            PlayerSwingPhysicsResult result = PlayerSwingPhysics.Step(new(Vector(0), Vector(3), Vector(6), Vector(9), (ushort)input[19]), world);
            long[] actual = [result.Position.X, result.Position.Y, result.Position.Z, result.Velocity.X, result.Velocity.Y,
                result.Velocity.Z, result.CollisionFlags, result.Contact.HasValue ? 1 : 0];
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(8).Select(value => value.GetInt64())),
                $"Actual PC swing physics {index}: source position/velocity, collision bits and world contact.");
            assert(world.Events.SequenceEqual(expected[8].EnumerateArray().Select(value => value.GetInt64())),
                $"Actual PC swing physics {index++}: body-hook ray first, then positive512/negative128 probes, with wrapped X/Z extension.");
        }
    }
    private sealed class Probe(int hitAt, PlayerWorldHit hit) : IPlayerWorldQuery
    {
        private int count;
        public List<long> Events { get; } = [];
        public PlayerWorldHit? Trace(FixedVector3 start, FixedVector3 end)
        {
            Events.AddRange([start.X, start.Y, start.Z, end.X, end.Y, end.Z]);
            return ++count == hitAt ? hit : null;
        }
    }
}
