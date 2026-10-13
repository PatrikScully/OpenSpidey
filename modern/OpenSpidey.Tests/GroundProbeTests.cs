using System.Reflection;
using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class GroundProbeTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.ground-probes.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement sample in document.RootElement.GetProperty("words").EnumerateArray())
        {
            uint bits = sample.GetProperty("bits").GetUInt32();
            float value = BitConverter.Int32BitsToSingle(unchecked((int)bits));
            assert(GameWord.TruncateToInt32(value) == sample.GetProperty("word").GetInt32(),
                $"Float pattern 0x{bits:X8} preserves original x87 truncation and low-word behavior.");
        }
        foreach (JsonElement sample in document.RootElement.GetProperty("probes").EnumerateArray())
        {
            GroundProbePoint point = GroundProbeFan.Point(Float(sample, "x"), Float(sample, "z"),
                Float(sample, "spread"), sample.GetProperty("index").GetUInt32());
            uint[] expected = sample.GetProperty("result").EnumerateArray().Select(value => value.GetUInt32()).ToArray();
            assert(unchecked((uint)BitConverter.SingleToInt32Bits(point.X)) == expected[0] &&
                unchecked((uint)BitConverter.SingleToInt32Bits(point.Z)) == expected[1],
                "Wide-ground probe order and float-coordinate quantization match recovered C++.");
        }
        assert(GameWord.TruncateToInt32(-7.75f) == -7 && GameWord.TruncateToInt32(7.75f) == 7,
            "Ground coordinates truncate toward zero before keeping the low word.");
        assert(GameWord.TruncateToInt32(2147483648.0f) == int.MinValue && GameWord.TruncateToInt32(4294967296.0f) == 0,
            "Large finite ground coordinates retain signed low-word wrapping.");
        assert(GameWord.TruncateToInt32(float.NaN) == 0 && GameWord.TruncateToInt32(float.PositiveInfinity) == 0 &&
            GameWord.TruncateToInt32(float.NegativeInfinity) == 0 && GameWord.TruncateToInt32(float.MaxValue) == 0,
            "Invalid x87 coordinates return the low word of the indefinite integer.");
        assert(GroundProbeFan.Point(100, 200, 10, 8) == new GroundProbePoint(100, 200) &&
            GroundProbeFan.Point(100, 200, 10, uint.MaxValue) == new GroundProbePoint(100, 200),
            "The final ground probe and out-of-range indices repeat the center.");
    }

    private static float Float(JsonElement sample, string property) =>
        BitConverter.Int32BitsToSingle(unchecked((int)sample.GetProperty(property).GetUInt32()));
}
