using System.Text.Json;
using OpenSpidey.Core.Effects;

internal static class SourceGameFadeTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(SourceGameFadeTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.source-game-fade.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        var state = new SourceGameFadeState();
        int index = 0;
        foreach (JsonElement test in document.RootElement.GetProperty("frames").EnumerateArray())
        {
            state.Set(test.GetProperty("initial").GetUInt32());
            int frame = 0;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                state.AdvanceVblank();
                assert(state.Word == expected.GetUInt32() && state.Active == (state.Word != 0),
                    $"Source fade {index}:{frame++}: original packed upper/lower countdown and wrapped uint state match actual PC instructions.");
            }
            index++;
        }
        state.BeginSepia();
        for (int frame = 0; frame < 119; frame++) state.AdvanceVblank();
        assert(state.Word == 1, "Sepia keeps its completion barrier active for the first119 original vblanks.");
        state.AdvanceVblank();
        assert(!state.Active, "Sepia completes exactly after30 upper-word and90 lower-word vblanks.");
        assert(SourceSepiaPixel.Convert(0) == 0 && SourceSepiaPixel.Convert(0x8000) == 0x8000 &&
            SourceSepiaPixel.Convert(0x7FFF) == 0x79EF,
            "Source sepia converts black/white through exact Q12 channel weights and preserves the original pixel high bit.");
        foreach (JsonElement pair in document.RootElement.GetProperty("pixels").EnumerateArray())
        {
            uint word = pair[0].GetUInt32();
            uint converted = (uint)SourceSepiaPixel.Convert((ushort)word) | (uint)SourceSepiaPixel.Convert((ushort)(word >> 16)) << 16;
            assert(converted == pair[1].GetUInt32(), "Both packed sepia pixels, Q12 truncation and original high bits match the actual PC rendering loop.");
        }
    }
}
