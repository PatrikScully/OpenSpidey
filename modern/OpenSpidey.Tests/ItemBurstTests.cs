using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class ItemBurstTests
{
    private sealed class Host(int[] input) : IItemBurstHost
    {
        private int cursor = input[6];
        public List<long> Events { get; } = [];
        public int Random(int bound) { int value = bound != 0 ? (cursor * 7 + 1) % bound : 0; cursor++; Events.AddRange([2, bound, value]); return value; }
        public int GetGroundY(FixedVector3 position) { Events.AddRange([1, position.X, position.Y, position.Z]); return input[5]; }
        public void Emit(ItemBurstFragment fragment)
        {
            int indices = 0;
            for (int corner = 0; corner < 4; corner++) indices |= fragment.Face.VertexIndices[corner] << (corner * 8);
            Events.AddRange([3, indices, fragment.Velocity.X, fragment.Velocity.Y, fragment.Velocity.Z, fragment.GroundY, fragment.Lifetime]);
            foreach (FixedVector3 point in fragment.Points) Events.AddRange([point.X, point.Y, point.Z]);
        }
    }
    public static void Run(Action<bool, string> assert)
    {
        PsxVertex[] vertices = Enumerable.Range(0, 6).Select(index => new PsxVertex(index == 0 ? default :
            new System.Numerics.Vector3(index * 23 - 31, index * 17 - 13, index * 11 + 7), 0)).ToArray();
        var model = new PsxModel(8, ushort.MaxValue, vertices,
            [new(0, [0, 1, 2, 3], 0x808080, null, []), new(0x10, [0, 4, 5], 0x808080, null, []), new(0, [1, 4, 5, 3], 0x808080, null, [])]);
        using Stream stream = typeof(ItemBurstTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.item-burst.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var host = new Host(input);
            ItemBurst.Create(model, new(input[0], input[1], input[2]), default, input[3], input[4], host, host.Random);
            assert(host.Events.SequenceEqual(test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt64())),
                $"CItem::Burst source/PC case {index}: actual ground-first, truncated source vertex transform, triangle/zero-length skip and per-face speed/lifetime shared RNG order.");
            index++;
        }
    }
}
