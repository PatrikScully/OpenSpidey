using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class ItemBurstFragmentTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ItemBurstFragmentTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.item-burst-fragments.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            FixedVector3[] vertices = Enumerable.Range(0, 4).Select(vertex =>
                new FixedVector3(input[vertex * 3], input[vertex * 3 + 1], input[vertex * 3 + 2])).ToArray();
            var face = new PsxFace(0, [0, 1, 2, 3], 0x808080, null, []);
            int cursor = input[16]; var events = new List<int>();
            int Random(int bound) { int value = (cursor++ * 7 + 1) % bound; events.AddRange([bound, value]); return value; }
            var fragment = new ItemBurstFragment(face, vertices, new(input[12], input[13], input[14]), input[15], Random);
            assert(ReferenceEquals(fragment.Face, face), "CItemFrag keeps the exact source face packet owner rather than a replacement texture quad.");
            int frame = 0;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                if (frame != 0) { events.Clear(); fragment.AdvanceBits(Random); }
                long[] actual = fragment.Points.Concat(fragment.Corners).SelectMany(point => new long[] { point.X, point.Y, point.Z })
                    .Concat([fragment.Velocity.X, fragment.Velocity.Y, fragment.Velocity.Z, fragment.GroundY,
                        fragment.Age, fragment.Lifetime, fragment.Tint, fragment.Type, fragment.Alive ? 0 : 1, fragment.QuadCode]).ToArray();
                assert(actual.SequenceEqual(expected.EnumerateArray().Take(34).Select(value => value.GetInt64())),
                    $"CItemFrag actualPC case {index} frame {frame}: source four-corner rise/groundbounce, eight displayjitters, age/RGB and exact semitransparentquadcode.");
                assert(events.SequenceEqual(expected[34].EnumerateArray().Select(value => value.GetInt32())),
                    $"CItemFrag actualPC case {index} frame {frame}: ordered constructor30 and every-dying-frame eight41 RNG calls.");
                frame++;
            }
            index++;
        }
    }
}
