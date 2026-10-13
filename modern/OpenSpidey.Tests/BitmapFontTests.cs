using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Core.UI;

internal static class BitmapFontTests
{
    public static void Run(Action<bool, string> assert)
    {
        using JsonDocument document = Fixture();
        int caseIndex = 0;
        foreach (JsonElement fontCase in document.RootElement.GetProperty("fonts").EnumerateArray())
        {
            // Only pixel-free source metrics are embedded; original font textures stay in the owner's archive.
            byte[] bytes = MetricFixture(fontCase);
            OriginalBitmapFont font = OriginalBitmapFontReader.Read(bytes, fontCase.GetProperty("name").GetString()!);
            assert(font.Glyphs.Count == fontCase.GetProperty("glyphs").GetArrayLength(), "Original constructor appends one patterned fallback glyph.");
            foreach (JsonElement test in fontCase.GetProperty("cases").EnumerateArray())
            {
                int[] input = Integers(test.GetProperty("input"));
                byte[] text = Encoding.ASCII.GetBytes(test.GetProperty("text").GetString()!);
                var state = new BitmapFontState(font)
                {
                    CharacterMap = input[0], Scale = input[1], Spacing = input[2], HorizontalAlignment = (BitmapHorizontalAlignment)input[3],
                    VerticalAlignment = (BitmapVerticalAlignment)input[4], FixedWidth = input[5] != 0, ShadowEnabled = input[6] != 0,
                    ShadowOffsetX = input[7], ShadowOffsetY = input[8], ShadowIntensity = input[9],
                    Red = 128, Green = 64, Blue = 32, SavedColour = new(17, 19, 23)
                };
                var layout = new BitmapTextLayout(state);
                JsonElement expected = test.GetProperty("expected");
                int[] measure = [layout.Width(text), layout.HeightAboveBaseline(text), layout.HeightBelowBaseline(text)];
                assert(measure.SequenceEqual(Integers(expected.GetProperty("measure"))), $"Bitmap source case {caseIndex}: original width/ascent/descent, actual[{string.Join(',', measure)}].");
                var callbacks = new Callbacks();
                layout.Draw(input[10], input[11], text, 0, .125f, input[12] != 0 ? callbacks : null,
                    glyph => callbacks.Events.AddRange([4, glyph.GlyphIndex, glyph.X, glyph.Y, glyph.Scale,
                        glyph.Colour.Red, glyph.Colour.Green, glyph.Colour.Blue, (int)glyph.DrawFlags, (int)(glyph.Depth * 8), glyph.Shadow ? 1 : 0]));
                long[] colours = [state.Red, state.Green, state.Blue, state.SavedColour.Red, state.SavedColour.Green, state.SavedColour.Blue];
                assert(colours.SequenceEqual(expected.GetProperty("colour").EnumerateArray().Select(value => value.GetInt64())),
                    $"Bitmap source case {caseIndex}: single saved colour/escape state.");
                int[] expectedEvents = Integers(expected.GetProperty("events"));
                assert(callbacks.Events.SequenceEqual(expectedEvents), $"Bitmap source case {caseIndex++}, {font.Name}, input[{string.Join(',', input)}], text'{Encoding.ASCII.GetString(text)}': actual[{string.Join(',', callbacks.Events)}] expected[{string.Join(',', expectedEvents)}]");
            }
            foreach (int length in new[] { 0, 3, 4, 15, bytes.Length - 1 })
            {
                bool rejected = false;
                try { OriginalBitmapFontReader.Read(bytes.AsSpan(0, length), "truncated"); } catch (InvalidDataException) { rejected = true; }
                assert(rejected, "Font bounds checks reject truncated header, metrics, palette and padded glyph data.");
            }
        }
    }

    public static void VerifyOriginal(byte[] bytes, string name, Action<bool, string> assert)
    {
        using JsonDocument document = Fixture();
        string fileName = Path.GetFileName(name);
        JsonElement gold = document.RootElement.GetProperty("fonts").EnumerateArray().Single(font =>
            font.GetProperty("name").GetString()!.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        assert(Convert.ToHexString(SHA256.HashData(bytes)).Equals(gold.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase),
            $"Owner's original font {name} matches the source-reference asset.");
        OriginalBitmapFont decoded = OriginalBitmapFontReader.Read(bytes, name);
        assert(decoded.Palette.SequenceEqual(gold.GetProperty("palette").EnumerateArray().Select(value => (ushort)value.GetInt32())),
            $"Original font {name} retains all sixteen palette words.");
        int index = 0;
        foreach (JsonElement glyph in gold.GetProperty("glyphs").EnumerateArray())
        {
            BitmapFontGlyph actual = decoded.Glyphs[index++];
            int[] metrics = [actual.SliceWidth, actual.Height, actual.Baseline, actual.Width];
            assert(metrics.SequenceEqual(Integers(glyph.GetProperty("metrics"))), $"Original font {name} glyph {actual.Index} metrics preserve source byte narrowing.");
            assert(Convert.ToHexString(SHA256.HashData(actual.RgbaPixels.Span)).Equals(glyph.GetProperty("rgbaSha256").GetString(), StringComparison.OrdinalIgnoreCase),
                $"Original font {name} glyph {actual.Index} uses exact source nibble order, PC palette conversion and missing-glyph pattern.");
        }
        assert(index == decoded.SourceGlyphCount + 1 && decoded.Glyphs[^1].PackedPixels.Span.ToArray().All(value => value == 0x12),
            "Original missing-glyph image is allocated from glyph0 metrics and filled with0x12.");
    }
    private static JsonDocument Fixture() => JsonDocument.Parse(typeof(BitmapFontTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.bitmap-font.json")!);
    private static int[] Integers(JsonElement array) => array.EnumerateArray().Select(value => value.GetInt32()).ToArray();
    private static byte[] MetricFixture(JsonElement font)
    {
        JsonElement[] glyphs = font.GetProperty("glyphs").EnumerateArray().ToArray();
        int count = glyphs.Length - 1;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(count);
        for (int index = 0; index < count; index++) foreach (int metric in Integers(glyphs[index].GetProperty("metrics"))) writer.Write(metric);
        foreach (JsonElement colour in font.GetProperty("palette").EnumerateArray()) writer.Write((ushort)colour.GetInt32());
        for (int index = 0; index < count; index++)
        {
            int[] metrics = Integers(glyphs[index].GetProperty("metrics"));
            writer.Write(new byte[(metrics[0] * metrics[1] * 2 + 3) & ~3]);
        }
        return stream.ToArray();
    }
    private sealed class Callbacks : IBitmapTextCallbacks
    {
        public List<int> Events { get; } = [];
        public void PrepareGlyphMetrics(int characterIndex, ref int widthBefore, ref int baseline)
        { Events.AddRange([1, widthBefore, baseline]); widthBefore += 31; baseline -= 17; }
        public void PrepareGlyphColour(int characterIndex, ref BitmapTextColour colour)
        { Events.AddRange([2, colour.Red, colour.Green, colour.Blue]); colour = new(unchecked((byte)(colour.Red + 3)), unchecked((byte)(colour.Green + 5)), unchecked((byte)(colour.Blue + 7))); }
        public void GlyphDrawn(int xBefore, int baselineBefore, int baselineAfter, int characterIndex) => Events.AddRange([3, xBefore, baselineBefore, baselineAfter, characterIndex]);
    }
}
