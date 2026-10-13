using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Rendering;

internal static class PlayerBrightnessRenderDepthTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerBrightnessRenderDepthTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-brightness-render-depth.json")!;
        using JsonDocument data = JsonDocument.Parse(stream);
        var node = new TriggerNode(0, 6, 0, ReadOnlyMemory<byte>.Empty);
        int index = 0;
        foreach (JsonElement row in data.RootElement.GetProperty("commands").EnumerateArray())
        {
            int[] initial = Values(row, "initial"), arguments = Values(row, "arguments");
            var depth = new SourcePolygonDepthState(new((short)initial[0], (short)initial[1], (short)initial[2]));
            var brightness = new PlayerBrightnessState(Light(0));
            ushort opcode = row.GetProperty("opcode").GetUInt16();
            byte[] bytes = new byte[opcode == 184 ? 6 : 2];
            for (int word = 0; word < bytes.Length / 2; word++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(word * 2), (ushort)arguments[word]);
            IMissionCommandHandler handler = opcode == 184 ? new PlayerShadowBrightnessCommandHandler(brightness) : new OrderTablePushbackCommandHandler(opcode, depth);
            handler.Execute(new(node, 0), new(opcode, 0, bytes));
            depth.BeginRender();
            long[] actual = [depth.Globals.First, depth.Globals.Second, depth.Globals.Third, brightness.DarkSurfaceTarget, bytes.Length];
            assert(actual.SequenceEqual(LongValues(row.GetProperty("expected"))) && depth.RenderScratch == depth.Globals &&
                brightness.Current == 208 && brightness.LightUpdates == 0,
                $"Original166/169/184 command{index++}: signed independent OT words/render scratch, unsigned first RGB word, exact2/6-byte consumption and deferred surface lighting.");
        }

        index = 0;
        foreach (JsonElement row in data.RootElement.GetProperty("brightness").EnumerateArray())
        {
            int seed = row.GetProperty("seed").GetInt32();
            SourceModelLight initial = Light(seed);
            var state = new PlayerBrightnessState(initial)
            { Current = row.GetProperty("current").GetUInt32(), LastAppliedBrightness = row.GetProperty("last").GetUInt32() };
            int updates = 0, visit = 0;
            foreach (JsonElement result in row.GetProperty("expected").EnumerateArray())
            {
                long[] expected = LongValues(result);
                bool changed = state.LastAppliedBrightness != (uint)expected[0];
                bool updated = state.Adjust(row.GetProperty("target").GetUInt16(), row.GetProperty("ticks").GetInt32());
                if (changed) updates++;
                long[] actual = [state.Current, state.LastAppliedBrightness, ..Matrix(state.Light.Colours),
                    state.Light.AmbientRed, state.Light.AmbientGreen, state.Light.AmbientBlue];
                assert(actual.SequenceEqual(expected) && updated == changed && state.LightUpdates == updates && state.Light.Directions == initial.Directions,
                    $"Original full4BAD90 brightness{index}/visit{visit++}: unsigned clamp/wrap, exact short colour matrix/signed ambient, retained light directions and source cache. Actual=[{string.Join(',', actual)}], expected=[{string.Join(',', expected)}].");
            }
            index++;
        }

        index = 0;
        foreach (JsonElement row in data.RootElement.GetProperty("depth").EnumerateArray())
        {
            int[] values = Values(row, "scratch");
            var scratch = new SourceOrderTablePushback((short)values[0], (short)values[1], (short)values[2]);
            bool low = row.GetProperty("low").GetInt32() != 0;
            uint flags = row.GetProperty("flags").GetUInt32();
            int normal = row.GetProperty("normal").GetInt32(), lowModel = row.GetProperty("lowModel").GetInt32();
            float scale = row.GetProperty("scale").GetSingle();
            ushort face = SourcePolygonDepthState.FaceBias(flags, low, scratch);
            int key = SourcePolygonDepthState.BatchSortKey(normal, lowModel, face, low);
            float bias = SourcePolygonDepthState.RasterDepthBias(key, low, scale);
            long[] actual = [face, key, BitConverter.SingleToUInt32Bits(bias)];
            assert(actual.SequenceEqual(LongValues(row.GetProperty("expected"))),
                $"Original face/raster OT policy{index++}:6000 mask, unsigned per-face word, source model-bias selection, wrapping sort key and exact float quality gate.");
            ushort unmodifiedFace = SourcePolygonDepthState.FaceBias(flags, low, default);
            float unmodifiedBias = SourcePolygonDepthState.RasterDepthBias(
                SourcePolygonDepthState.BatchSortKey(normal, lowModel, unmodifiedFace, low), low, scale);
            assert(BitConverter.SingleToUInt32Bits(bias) == BitConverter.SingleToUInt32Bits(unmodifiedBias),
                "Original OT globals contribute zero raster depth because their face-output and raster-input quality gates are opposite.");
        }
    }

    private static int[] Values(JsonElement row, string name) => row.GetProperty(name).EnumerateArray().Select(value => value.GetInt32()).ToArray();
    private static long[] LongValues(JsonElement row) => row.EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private static long[] Matrix(FixedJointTransform m) => [m.M00, m.M01, m.M02, m.M10, m.M11, m.M12, m.M20, m.M21, m.M22];
    private static SourceModelLight Light(int seed) => new(new(4096, 0, 0, 0, 4096, 0, 0, 0, 4096, 0, 0, 0),
        new((short)(101 + seed), (short)(108 + seed), (short)(115 + seed), (short)(122 + seed), (short)(129 + seed),
            (short)(136 + seed), (short)(143 + seed), (short)(150 + seed), (short)(157 + seed), 0, 0, 0),
        9001 + seed, 9014 + seed, 9027 + seed);
}
