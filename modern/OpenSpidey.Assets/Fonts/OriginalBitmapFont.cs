namespace OpenSpidey.Assets.Fonts;

/// <summary>Original FontCharacter byte metrics and its padded four-bit glyph image.</summary>
public sealed record BitmapFontGlyph(int Index, int SliceWidth, byte Width, byte Height, byte Baseline,
    ReadOnlyMemory<byte> PackedPixels, ReadOnlyMemory<byte> RgbaPixels, bool IsFallback)
{
    public int TextureWidth => checked(SliceWidth * 4);
}

public sealed class OriginalBitmapFont
{
    public string Name { get; }
    public int SourceGlyphCount { get; }
    public IReadOnlyList<ushort> Palette { get; }
    public IReadOnlyList<BitmapFontGlyph> Glyphs { get; }
    internal OriginalBitmapFont(string name, ushort[] palette, BitmapFontGlyph[] glyphs)
    { Name = name; SourceGlyphCount = glyphs.Length - 1; Palette = Array.AsReadOnly(palette); Glyphs = Array.AsReadOnly(glyphs); }
}
