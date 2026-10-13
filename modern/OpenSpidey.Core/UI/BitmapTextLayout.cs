using OpenSpidey.Assets.Fonts;

namespace OpenSpidey.Core.UI;

public enum BitmapHorizontalAlignment { Left, Centre, Right }
public enum BitmapVerticalAlignment { Top, Centre, Bottom, Baseline }
public readonly record struct BitmapTextColour(byte Red, byte Green, byte Blue);
public readonly record struct BitmapGlyphDraw(int CharacterIndex, int GlyphIndex, int X, int Y, int Scale,
    BitmapTextColour Colour, uint DrawFlags, float Depth, bool Shadow);

/// <summary>Font constructor defaults; the persistent saved colour belongs to this source font instance.</summary>
public sealed class BitmapFontState(OriginalBitmapFont font)
{
    public OriginalBitmapFont Font { get; } = font ?? throw new ArgumentNullException(nameof(font));
    public int Scale { get; set; } = 3800;
    public int Spacing { get; set; } = 2;
    public BitmapHorizontalAlignment HorizontalAlignment { get; set; }
    public BitmapVerticalAlignment VerticalAlignment { get; set; } = BitmapVerticalAlignment.Baseline;
    public int CharacterMap { get; set; }
    public bool FixedWidth { get; set; }
    public bool ShadowEnabled { get; set; } = true;
    public int ShadowOffsetX { get; set; } = 1;
    public int ShadowOffsetY { get; set; } = 1;
    public int ShadowIntensity { get; set; } = 255;
    public uint Red { get; set; } = 128;
    public uint Green { get; set; } = 128;
    public uint Blue { get; set; } = 128;
    public BitmapTextColour SavedColour { get; set; }
}

/// <summary>The three optional Font::draw callback slots, with their exact mutable metrics/colour boundaries.</summary>
public interface IBitmapTextCallbacks
{
    void PrepareGlyphMetrics(int characterIndex, ref int widthBefore, ref int baseline);
    void PrepareGlyphColour(int characterIndex, ref BitmapTextColour colour);
    void GlyphDrawn(int xBefore, int baselineBefore, int baselineAfter, int characterIndex);
}

/// <summary>Source Font ASCII mapping, measurements and two-pass glyph layout; render submission remains explicit.</summary>
public sealed class BitmapTextLayout(BitmapFontState state)
{
    public int GetGlyphIndex(byte character)
    {
        int count = state.Font.SourceGlyphCount;
        int index;
        if (state.CharacterMap == 1)
            index = character is >= (byte)'0' and <= (byte)'9' ? character - '0' : character == ':' ? 10 : character == ' ' ? -1 : IsEscape(character) ? -1 : count;
        else if (state.CharacterMap is 0 or 2)
        {
            if (character is >= (byte)'A' and <= (byte)'Z') index = character - 'A';
            else if (character is >= (byte)'a' and <= (byte)'z') index = character - (state.CharacterMap == 2 ? 0x30 : 'a');
            else if (character is >= (byte)'0' and <= (byte)'9') index = character - 0x16;
            else if (character == ' ' || IsEscape(character)) index = -1;
            else
            {
                int punctuation = character switch { (byte)'_' => 36, (byte)'?' => 37, (byte)'!' => 38,
                    (byte)':' => 39, (byte)'.' => 40, (byte)'-' => 41, (byte)'\'' => 42, (byte)'+' => 43, _ => -1 };
                index = punctuation >= 0 && count > punctuation ? punctuation : count;
            }
        }
        else throw new ArgumentOutOfRangeException(nameof(state.CharacterMap), "Only original character maps0,1,2 are defined.");
        if (index >= state.Font.Glyphs.Count) throw new InvalidDataException("Original font character map addresses an absent glyph.");
        return index;
    }

    public int FixedCharWidth(byte character)
    {
        if (IsEscape(character)) return 0;
        int glyph = state.CharacterMap == 1 && character == ':' ? 10 : 0;
        return unchecked(state.Scale * unchecked(state.Spacing + state.Font.Glyphs[glyph].Width)) >> 12;
    }

    public int Width(ReadOnlySpan<byte> text)
    {
        Validate(text);
        int width = 0;
        foreach (byte character in text)
        {
            if (character == 0) break;
            int glyph = GetGlyphIndex(character);
            if (glyph >= 0) width = unchecked(width + state.Spacing + state.Font.Glyphs[glyph].Width);
            else if (!IsEscape(character)) width = unchecked(width + 80 * state.Font.Glyphs[0].Width / 100);
        }
        // Source width() intentionally ignores the draw-only fixed-width switch.
        return unchecked(width * state.Scale) >> 12;
    }
    public int HeightAboveBaseline(ReadOnlySpan<byte> text) => Height(text, true);
    public int HeightBelowBaseline(ReadOnlySpan<byte> text) => Height(text, false);
    public int Height(ReadOnlySpan<byte> text) => unchecked(HeightAboveBaseline(text) + HeightBelowBaseline(text));
    private int Height(ReadOnlySpan<byte> text, bool above)
    {
        Validate(text);
        int maximum = 0;
        foreach (byte character in text)
        {
            if (character == 0) break;
            int index = GetGlyphIndex(character);
            if (index < 0) continue;
            BitmapFontGlyph glyph = state.Font.Glyphs[index];
            maximum = Math.Max(maximum, above ? glyph.Baseline : glyph.Height - glyph.Baseline);
        }
        return unchecked(maximum * state.Scale) >> 12;
    }

    public IReadOnlyList<BitmapGlyphDraw> Draw(int x, int y, ReadOnlySpan<byte> text, int drawFlags = 0,
        float depth = 0, IBitmapTextCallbacks? callbacks = null, Action<BitmapGlyphDraw>? submit = null)
    {
        Validate(text);
        if (state.Scale == 0 && !state.FixedWidth) return Array.Empty<BitmapGlyphDraw>();
        int length = text.IndexOf((byte)0);
        if (length < 0) length = text.Length;
        if (length > 64) throw new ArgumentException("Original font draw has64 character metric slots.", nameof(text));
        if (state.Scale is <= 0 or >= 1048576) throw new ArgumentOutOfRangeException(nameof(state.Scale), "Original glyph rendering requires0<scale<256Q12.");
        var widthAt = new int[length];
        var baselines = new int[length];
        var widthBefore = new int[length];
        var baselineBefore = new int[length];
        int total = 0, maximumAbove = 0, maximumBelow = 0;
        for (int characterIndex = 0; characterIndex < length; characterIndex++)
        {
            byte character = text[characterIndex];
            int glyphIndex = GetGlyphIndex(character);
            if (glyphIndex < 0)
            {
                if (!IsEscape(character)) total = unchecked(total + unchecked(state.Font.Glyphs[0].Width * state.Scale * 80) / 100);
                continue;
            }
            BitmapFontGlyph glyph = state.Font.Glyphs[glyphIndex];
            widthAt[characterIndex] = total;
            int below = unchecked((glyph.Height - glyph.Baseline) * state.Scale), above = unchecked(glyph.Baseline * state.Scale);
            maximumBelow = Math.Max(maximumBelow, below); maximumAbove = Math.Max(maximumAbove, above);
            baselines[characterIndex] = unchecked(-above);
            if (callbacks is not null)
            {
                widthBefore[characterIndex] = total;
                baselineBefore[characterIndex] = unchecked(-above);
                callbacks.PrepareGlyphMetrics(characterIndex, ref widthAt[characterIndex], ref baselines[characterIndex]);
            }
            if (state.FixedWidth)
            {
                widthAt[characterIndex] = unchecked(total + (FixedCharWidth(character) << 12) - glyph.Width * state.Scale);
                total = unchecked(total + (FixedCharWidth(character) << 12));
            }
            else total = unchecked(total + unchecked((glyph.Width + state.Spacing) * state.Scale));
        }
        int xStart = state.HorizontalAlignment switch
        {
            BitmapHorizontalAlignment.Left => x << 12,
            BitmapHorizontalAlignment.Centre => unchecked((x << 12) - (total >> 1)),
            BitmapHorizontalAlignment.Right => unchecked((x << 12) - total),
            _ => throw new ArgumentOutOfRangeException(nameof(state.HorizontalAlignment))
        };
        int yStart = state.VerticalAlignment switch
        {
            BitmapVerticalAlignment.Top => unchecked((y << 12) + maximumAbove),
            BitmapVerticalAlignment.Centre => unchecked((y << 12) + ((maximumAbove + maximumBelow) >> 1) - maximumBelow),
            BitmapVerticalAlignment.Bottom => unchecked((y << 12) - maximumBelow),
            BitmapVerticalAlignment.Baseline => y << 12,
            _ => throw new ArgumentOutOfRangeException(nameof(state.VerticalAlignment))
        };
        var draws = new List<BitmapGlyphDraw>();
        for (int characterIndex = 0; characterIndex < length; characterIndex++)
        {
            byte character = text[characterIndex];
            int glyph = GetGlyphIndex(character);
            if (glyph < 0) { if (IsEscape(character)) HandleEscape(character); continue; }
            var colour = new BitmapTextColour(unchecked((byte)state.Red), unchecked((byte)state.Green), unchecked((byte)state.Blue));
            callbacks?.PrepareGlyphColour(characterIndex, ref colour);
            int drawX = unchecked((unchecked(widthAt[characterIndex] + xStart) >> 12) - 1);
            int drawY = (int)((unchecked(baselines[characterIndex] + yStart) >> 12) - .5);
            Emit(new(characterIndex, glyph, drawX, drawY, state.Scale, colour, unchecked((uint)drawFlags) | 1, depth, false));
            if (state.ShadowEnabled)
            {
                byte shadow = (byte)(255 - unchecked((byte)state.ShadowIntensity));
                int shadowX = unchecked((unchecked(widthAt[characterIndex] + state.ShadowOffsetX * state.Scale + xStart) >> 12) - 1);
                int shadowY = (int)((unchecked(baselines[characterIndex] + state.ShadowOffsetY * state.Scale + yStart) >> 12) - .5);
                Emit(new(characterIndex, glyph, shadowX, shadowY, state.Scale, new(shadow, shadow, shadow), unchecked((uint)drawFlags) | 1, depth + 1, true));
            }
            callbacks?.GlyphDrawn(unchecked(widthBefore[characterIndex] + xStart), unchecked(baselineBefore[characterIndex] + yStart),
                unchecked(baselines[characterIndex] + yStart), characterIndex);
        }
        return draws.AsReadOnly();
        void Emit(BitmapGlyphDraw glyph) { draws.Add(glyph); submit?.Invoke(glyph); }
    }

    private static bool IsEscape(byte character) => character is (byte)']' or (byte)'[' or (byte)'}' or (byte)'{' or
        (byte)'>' or (byte)'<' or (byte)'|' or (byte)'~' or (byte)'^';
    private void HandleEscape(byte character)
    {
        if (character == '^') return;
        if (character is (byte)']' or (byte)'}' or (byte)'>')
        { state.Red = state.SavedColour.Red; state.Green = state.SavedColour.Green; state.Blue = state.SavedColour.Blue; return; }
        state.SavedColour = new(unchecked((byte)state.Red), unchecked((byte)state.Green), unchecked((byte)state.Blue));
        (state.Red, state.Green, state.Blue) = character switch
        { (byte)'[' => (0u, 128u, 255u), (byte)'{' => (255u, 128u, 0u), (byte)'<' => (64u, 64u, 64u),
            (byte)'|' => (128u, 64u, 64u), (byte)'~' => (64u, 128u, 64u), _ => throw new ArgumentOutOfRangeException(nameof(character)) };
    }
    private static void Validate(ReadOnlySpan<byte> text)
    {
        foreach (byte character in text)
        {
            if (character == 0) return;
            if (character >= 128) throw new NotSupportedException("This source font contract covers ASCII; signed-byte extended indexing requires separate recovery.");
        }
    }
}
