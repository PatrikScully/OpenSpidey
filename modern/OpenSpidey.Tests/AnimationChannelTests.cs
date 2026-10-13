using System.Reflection;
using System.Text.Json;
using OpenSpidey.Assets.Animation;

internal static class AnimationChannelTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.animation-channels.json")
            ?? throw new InvalidOperationException("Missing animation reference vectors.");
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement vector in document.RootElement.EnumerateArray())
        {
            byte[] source = Convert.FromHexString(vector.GetProperty("source").GetString()!);
            short[] expected = vector.GetProperty("values").EnumerateArray().Select(value => value.GetInt16()).ToArray();
            short[] destination = Enumerable.Repeat((short)12345, expected.Length * 3).ToArray();
            int consumed = AnimationChannelDecoder.Decode(source, destination, expected.Length, stride: 3);
            assert(consumed == source.Length, $"Animation mode {source[0]:X2} consumed bytes match reference.");
            assert(expected.Select((value, index) => destination[index * 3] == value).All(value => value),
                $"Animation mode {source[0]:X2} samples match recovered C++ reference.");
            assert(destination.Where((_, index) => index % 3 != 0).All(value => value == 12345),
                "Decoding one channel preserves the neighboring channels.");
            if (source.Length > 1)
            {
                bool rejected = false;
                try { AnimationChannelDecoder.Decode(source.AsSpan(0, source.Length - 1), destination, expected.Length, 3); }
                catch (InvalidDataException) { rejected = true; }
                assert(rejected, "Truncated animation input is rejected without an out-of-range read.");
            }
        }
        bool invalidStride = false;
        try { AnimationChannelDecoder.Decode([15], new short[1], 1, 0); }
        catch (ArgumentOutOfRangeException) { invalidStride = true; }
        assert(invalidStride, "Zero stride is rejected.");
        bool shortDestination = false;
        try { AnimationChannelDecoder.Decode([15], new short[1], int.MaxValue, int.MaxValue); }
        catch (ArgumentException) { shortDestination = true; }
        assert(shortDestination, "Destination sizing rejects overflow before writing samples.");
    }
}
