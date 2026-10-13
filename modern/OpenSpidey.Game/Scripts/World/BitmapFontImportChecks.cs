using Godot;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.UI;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.World;

public static class BitmapFontImportChecks
{
    public static void Verify(Node owner, byte[] originalBytes, string sourceName)
    {
        OriginalBitmapFont original = OriginalBitmapFontReader.Read(originalBytes, sourceName);
        var text = GodotBitmapText.Create(original);
        owner.AddChild(text);
        try
        {
            for (int glyph = 0; glyph < original.Glyphs.Count; glyph++)
            {
                if (original.Glyphs[glyph].Height == 0) continue;
                using Image image = text.GlyphTextures[glyph]!.GetImage();
                Require(image.GetWidth() == original.Glyphs[glyph].TextureWidth && image.GetHeight() == original.Glyphs[glyph].Height &&
                    image.GetData().AsSpan().SequenceEqual(original.Glyphs[glyph].RgbaPixels.Span), "Native font must upload the actual original glyph pixels/palette without replacing or trimming its padded image.");
            }
            text.State.Scale = 4096;
            text.State.Spacing = -1;
            text.State.ShadowEnabled = true;
            text.ShowText(16, 48, "0123456789:"u8);
            Require(text.GlyphDrawCount == 22, "Actual source digits/colon each retain their foreground and black shadow submission.");
            BitmapGlyphDraw[] draws = text.Layout.Draw(16, 48, "0"u8, 8, 4).ToArray();
            byte[] brightness = Enumerable.Range(0, 256).Select(value => (byte)Math.Min(value * 2, 255)).ToArray();
            var normal = GodotBitmapText.Project(draws[0], original.Glyphs[draws[0].GlyphIndex], new(640, 480), HudHorizontalAnchor.Left, HudVerticalAnchor.Top, brightness)!.Value;
            var wide = GodotBitmapText.Project(draws[0], original.Glyphs[draws[0].GlyphIndex], new(1280, 480), HudHorizontalAnchor.Right, HudVerticalAnchor.Top, brightness)!.Value;
            Require(normal.Destination.Size == wide.Destination.Size && wide.Destination.Position.X - normal.Destination.Position.X == 640 &&
                normal.Destination.Size.X / original.Glyphs[draws[0].GlyphIndex].TextureWidth == 1.25f && normal.Destination.Size.Y / original.Glyphs[draws[0].GlyphIndex].Height == 2,
                "Glyph dimensions, metric spacing and edge anchoring must share the source512x240 pixel aspect; widescreen adds group offset without stretching.");
            Require(normal.Colour == Colors.White, "Default128 font RGB uses actual PC brightness4 without the world/glow colour prebias.");
            var shadow = GodotBitmapText.Project(draws[1], original.Glyphs[draws[1].GlyphIndex], new(640, 480), HudHorizontalAnchor.Left, HudVerticalAnchor.Top, brightness)!.Value;
            Require(shadow.Colour == Colors.Black && shadow.Depth == normal.Depth + 1,
                "Original shadow intensity255 must invert to black at source depth+1.");
            BitmapGlyphDraw clippedDraw = draws[0] with { X = -2, Y = -3 };
            var clipped = GodotBitmapText.Project(clippedDraw, original.Glyphs[clippedDraw.GlyphIndex], new(640, 480), HudHorizontalAnchor.Left, HudVerticalAnchor.Top, brightness)!.Value;
            Require(clipped.Destination.Position == Vector2.Zero && clipped.Source.Position == new Vector2(2, 3),
                "Font viewport clipping must advance actual glyph UVs rather than shifting or resizing its visible content.");
            text.Free();
            Require(text.ResourcesDisposed, "Font node teardown must release all uploaded original glyph textures.");
            GD.Print($"Verified genuine bitmap font {sourceName}: {original.SourceGlyphCount}+fallback glyph uploads, source digits/metrics/shadow/brightness, proportional viewport groups and clipped UVs.");
        }
        finally { if (GodotObject.IsInstanceValid(text)) text.Free(); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
