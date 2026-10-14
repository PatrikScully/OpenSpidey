using System.Text.Json;
using OpenSpidey.Core.Presentation;

internal static class PcVertexFadeTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PcVertexFadeTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.pc-vertex-fade.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int cases = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            int start = (int)input[0], end = (int)input[1], count = (int)input[6];
            bool superFade = input[2] != 0, fog = input[3] != 0, low = input[4] != 0;
            float[] rhw = Enumerable.Range(0, count).Select(index => BitConverter.UInt32BitsToSingle((uint)input[7 + index * 2])).ToArray();
            uint[] source = Enumerable.Range(0, count).Select(index => (uint)input[8 + index * 2]).ToArray();
            byte[] corners = input.Skip(7 + count * 2).Take(4).Select(value => (byte)value).ToArray();
            string name = $"PC vertex fade[{string.Join(',', input)}]";
            (float near, float far, float scale) = PcVertexFade.Limits(start, end);
            uint[] limits = [BitConverter.SingleToUInt32Bits(near), BitConverter.SingleToUInt32Bits(far), BitConverter.SingleToUInt32Bits(scale)];
            assert(limits.SequenceEqual(expected.EnumerateArray().Take(3).Select(value => (uint)value.GetInt64())), $"{name} M3d_RenderSetup near/far/scale bits.");
            bool modelFaded = PcVertexFade.ModelFaded(superFade, fog, low, rhw, far);
            assert(modelFaded == (expected[3].GetInt32() != 0), $"{name} DCModel blend-mode-1 model decision.");
            uint[] model = (uint[])source.Clone();
            PcVertexFade.ApplyModel(model, rhw, superFade, (uint)input[5], modelFaded, near, far, scale);
            uint[] wantedModel = expected[4].EnumerateArray().Select(value => (uint)value.GetInt64()).ToArray();
            assert(model.SequenceEqual(wantedModel), $"{name} DCModel colours; actual[{string.Join(',', model)}], expected[{string.Join(',', wantedModel)}].");
            uint[] face = (uint[])source.Clone();
            bool faceFaded = PcVertexFade.ApplyFace(face, rhw, corners, fog, low, near, far, scale);
            assert(faceFaded == (expected[5].GetInt32() != 0), $"{name} DC_PSXModel blend-mode-1 face decision.");
            uint[] wantedFace = expected[6].EnumerateArray().Select(value => (uint)value.GetInt64()).ToArray();
            assert(face.SequenceEqual(wantedFace), $"{name} DC_PSXModel colours; actual[{string.Join(',', face)}], expected[{string.Join(',', wantedFace)}].");
            cases++;
        }
        assert(cases == 1200, $"The original PC vertex-fade fixture keeps all 1200 cases, actual {cases}.");
    }
}
