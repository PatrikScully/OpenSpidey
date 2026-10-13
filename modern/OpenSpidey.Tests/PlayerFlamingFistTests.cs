using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerFlamingFistTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerFlamingFistTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-flaming-fists.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            long[] input = row.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
            FixedVector3 left = Vector(5), right = Vector(8);
            uint seed = (uint)input[3];
            var events = new List<long>();
            PlayerFlamingFists.Emit(unchecked((byte)input[0]), (int)input[1], unchecked((ushort)input[2]), left, right, Random, Emit);
            JsonElement expected = row.GetProperty("expected");
            long[] actual = [unchecked((byte)input[0]), input[1], unchecked((ushort)input[2]), seed];
            long[] expectedEvents = expected[4].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(4).Select(value => value.GetInt64())) && events.SequenceEqual(expectedEvents),
                $"Original flaming fists{index++}: exact gates, left-before-right simby5, borrowed hand endpoints, scale256 and constructor RNG interleaving. Actual=[{string.Join(',', actual)};{string.Join(',', events)}], expected=[{string.Join(',', expectedEvents)}].");

            int Random(int maximum)
            {
                seed = unchecked(seed * 1664525 + 1013904223);
                int value = (int)(seed % (uint)maximum);
                events.AddRange([1, maximum, value]);
                return value;
            }
            void Emit(FixedVector3 position, int scale)
            {
                events.AddRange([2, position == left ? 0 : 1, position.X, position.Y, position.Z, scale]);
                // This real source constructor consumes the same shared RNG before the next hand's selection.
                if (input[4] != 0) _ = new FireEmber(position, scale, Random);
            }
        }
    }
}
