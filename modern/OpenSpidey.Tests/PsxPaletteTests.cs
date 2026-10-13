using System.Text.Json;
using OpenSpidey.Assets.Textures;

internal static class PsxPaletteTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PsxPaletteTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.psx-palette.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            ushort[] palette = test.GetProperty("input").EnumerateArray().Select(value => value.GetUInt16()).ToArray();
            PsxPalette.Process(palette);
            assert(palette.SequenceEqual(test.GetProperty("expected").EnumerateArray().Select(value => value.GetUInt16())),
                $"PSX palette{index++} matches actual PC magenta-key priority, preserved opaque black, transparent zero and RGB-preserving alpha.");
        }
    }
}
