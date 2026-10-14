using System.Text.Json;
using OpenSpidey.Core.Effects;

internal static class VertexWobbleTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(VertexWobbleTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.vertex-wobble.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0, counts = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            string mode = test.GetProperty("mode").GetString()!;
            int[] input = Words(test.GetProperty("input"));
            JsonElement expected = test.GetProperty("expected");
            if (mode == "C") Construct(assert, index, input, expected, moveFrames: 0);
            else if (mode == "M") Construct(assert, index, input, expected, moveFrames: input[6]);
            else Make(assert, index, input, expected);
            index++;
            counts++;
        }
        assert(counts == 118, "Actual PC CVertexWobble fixture keeps all constructor, Move and MakeVertexWibbler cases.");
    }

    private static void Construct(Action<bool, string> assert, int index, int[] input, JsonElement expected, int moveFrames)
    {
        int count = input[0], a5 = input[1], a6 = input[2], a7 = input[3], a8 = input[4];
        uint seed = unchecked((uint)input[5]);
        byte[] indices = input.Skip(7).Take(count).Select(value => (byte)value).ToArray();
        var source = new List<(short X, short Y, short Z)>();
        for (int i = 0; i < 16; i++)
        {
            int o = 7 + count + i * 3;
            source.Add(((short)input[o], (short)input[o + 1], (short)input[o + 2]));
        }
        var model = new VertexWobbleModel(source);
        int Random(int bound) { seed = unchecked(seed * 1664525u + 1013904223u); return bound == 0 ? 0 : (int)(seed % (uint)bound); }
        var state = new VertexWobbleState(model, indices, a5, a6, a7, a8, Random);
        AssertSnapshot(assert, index, "constructor", state, model, expected[0], expected[1]);
        if (moveFrames == 0) return;
        JsonElement moves = expected[2];
        bool safe = state.Entries.All(entry => entry.Distance != 0);
        if (!safe)
        {
            assert(moves.GetArrayLength() == 0, $"Case {index}: zero-distance Move stays empty like the original harness.");
            return;
        }
        assert(moves.GetArrayLength() == moveFrames, $"Case {index}: Move frame count.");
        for (int frame = 0; frame < moveFrames; frame++)
        {
            state.Move();
            AssertVerts(assert, index, $"move{frame}", model, moves[frame]);
        }
    }

    private static void Make(Action<bool, string> assert, int index, int[] input, JsonElement expected)
    {
        uint seed = unchecked((uint)input[1]);
        var source = new List<(short X, short Y, short Z)>();
        for (int i = 0; i < 16; i++) source.Add(((short)(i * 23 - 31), (short)(i * 17 - 13), (short)(i * 11 + 7)));
        var model = new VertexWobbleModel(source);
        int Random(int bound) { seed = unchecked(seed * 1664525u + 1013904223u); return bound == 0 ? 0 : (int)(seed % (uint)bound); }
        VertexWobbleState[] pair = MakeVertexWibbler.Create(model, Random);
        AssertSnapshot(assert, index, "wibblerA", pair[0], model, expected[0], expected[2], compareVerts: false);
        AssertSnapshot(assert, index, "wibblerB", pair[1], model, expected[1], expected[2], compareVerts: true);
    }

    private static void AssertSnapshot(Action<bool, string> assert, int index, string label, VertexWobbleState state,
        VertexWobbleModel model, JsonElement entryBlock, JsonElement verts, bool compareVerts = true)
    {
        int[] header = [state.CentreX, state.CentreY, state.CentreZ, state.Entries.Count];
        int[] expectedHeader = Words(entryBlock).Take(4).ToArray();
        assert(header.SequenceEqual(expectedHeader),
            $"Case {index} {label} centre/count actual=[{string.Join(',', header)}] expected=[{string.Join(',', expectedHeader)}].");
        JsonElement entries = entryBlock[4];
        assert(entries.GetArrayLength() == state.Entries.Count, $"Case {index} {label} entry count.");
        for (int i = 0; i < state.Entries.Count; i++)
        {
            VertexWobbleEntry entry = state.Entries[i];
            int[] actual = [entry.Vx, entry.Vy, entry.Vz, entry.Dx, entry.Dy, entry.Dz, entry.VertexIndex, entry.Distance,
                entry.Amplitude, entry.PhaseSpeed, entry.Phase];
            int[] expected = Words(entries[i]);
            assert(actual.SequenceEqual(expected),
                $"Case {index} {label} entry{i} actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
        }
        if (compareVerts) AssertVerts(assert, index, label, model, verts);
    }

    private static void AssertVerts(Action<bool, string> assert, int index, string label, VertexWobbleModel model, JsonElement verts)
    {
        assert(verts.GetArrayLength() == model.Count, $"Case {index} {label} vertex count.");
        for (int i = 0; i < model.Count; i++)
        {
            (short x, short y, short z) = model[i];
            int[] actual = [x, y, z];
            int[] expected = Words(verts[i]);
            assert(actual.SequenceEqual(expected),
                $"Case {index} {label} vertex{i} actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
        }
    }

    private static int[] Words(JsonElement element) => element.ValueKind == JsonValueKind.Array
        ? element.EnumerateArray().SelectMany(value => value.ValueKind == JsonValueKind.Array ? Words(value)
            : [checked((int)value.GetInt64())]).ToArray()
        : throw new InvalidDataException("Fixture words require a JSON array.");
}
