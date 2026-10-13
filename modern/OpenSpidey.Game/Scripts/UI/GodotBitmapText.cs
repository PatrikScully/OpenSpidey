using Godot;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.UI;

namespace OpenSpidey.Game.UI;

internal readonly record struct GodotBitmapGlyphQuad(int GlyphIndex, Rect2 Destination, Rect2 Source,
    Color Colour, float Depth, bool Shadow);

/// <summary>Original archived glyph images, source layout/brightness and one responsive viewport transform per text group.</summary>
public partial class GodotBitmapText : Node2D
{
    private sealed record Entry(BitmapGlyphDraw Draw, HudHorizontalAnchor Horizontal, HudVerticalAnchor Vertical, int Order);
    private readonly List<Entry> entries = [];
    private ImageTexture?[] textures = [];
    private byte[] brightness = Enumerable.Range(0, 256).Select(channel => (byte)Math.Min(channel * 2, 255)).ToArray();
    private Vector2 lastViewport;
    private bool disposed;
    public BitmapFontState State { get; private set; } = null!;
    public BitmapTextLayout Layout { get; private set; } = null!;
    internal IReadOnlyList<ImageTexture?> GlyphTextures => textures;
    internal bool ResourcesDisposed => disposed;
    public int GlyphDrawCount => entries.Count;

    public GodotBitmapText() => TextureFilter = TextureFilterEnum.Nearest;
    public static GodotBitmapText Create(OriginalBitmapFont original)
    {
        ArgumentNullException.ThrowIfNull(original);
        var result = new GodotBitmapText { Name = "OriginalBitmapText", State = new(original) };
        result.Layout = new(result.State);
        result.textures = new ImageTexture?[original.Glyphs.Count];
        try
        {
            foreach (BitmapFontGlyph glyph in original.Glyphs)
            {
                if (glyph.Height == 0) continue;
                using Image image = Image.CreateFromData(glyph.TextureWidth, glyph.Height, false, Image.Format.Rgba8, glyph.RgbaPixels.ToArray());
                result.textures[glyph.Index] = ImageTexture.CreateFromImage(image);
            }
            return result;
        }
        catch { result.Release(); result.Free(); throw; }
    }

    public void Clear() { ThrowIfDisposed(); entries.Clear(); QueueRedraw(); }
    public void ShowText(int x, int y, ReadOnlySpan<byte> ascii, HudHorizontalAnchor horizontal = HudHorizontalAnchor.Left,
        HudVerticalAnchor vertical = HudVerticalAnchor.Top, int drawFlags = 8, float depth = 4, IBitmapTextCallbacks? callbacks = null)
    { Clear(); AddText(x, y, ascii, horizontal, vertical, drawFlags, depth, callbacks); }

    public void AddText(int x, int y, ReadOnlySpan<byte> ascii, HudHorizontalAnchor horizontal = HudHorizontalAnchor.Left,
        HudVerticalAnchor vertical = HudVerticalAnchor.Top, int drawFlags = 8, float depth = 4, IBitmapTextCallbacks? callbacks = null)
    {
        ThrowIfDisposed();
        if ((drawFlags & ~8) != 0)
            throw new NotSupportedException("Bitmap HUD adapter supports original ordinary glyph blending with automatic or explicit source depth.");
        if (!float.IsFinite(depth)) throw new ArgumentOutOfRangeException(nameof(depth));
        if (entries.Count != 0 && ((entries[0].Draw.DrawFlags & 8) != (unchecked((uint)drawFlags) & 8) || entries[0].Draw.Depth != depth))
            throw new NotSupportedException("A bitmap canvas group uses one original depth layer; separate groups retain independent source UI layers.");
        Layout.Draw(x, y, ascii, drawFlags, depth, callbacks,
            draw => entries.Add(new(draw, horizontal, vertical, entries.Count)));
        QueueRedraw();
    }

    /// <summary>Actual PC brightness lookup; default source setting4 doubles/saturates without world/glow prebias.</summary>
    public void SetBrightness(ReadOnlySpan<byte> table)
    {
        ThrowIfDisposed();
        if (table.Length != 256) throw new ArgumentException("Source PC brightness requires256 channel entries.", nameof(table));
        brightness = table.ToArray(); QueueRedraw();
    }

    public override void _Process(double delta)
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (size != lastViewport) { lastViewport = size; QueueRedraw(); }
    }
    public override void _Draw()
    {
        if (disposed || State is null) return;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (size.X <= 0 || size.Y <= 0) return;
        DrawOn(this, new(size.X, size.Y));
    }

    /// <summary>May also submit inside an owning HUD CanvasItem's draw callback.</summary>
    public void DrawOn(CanvasItem canvas, HudViewportTransform viewport)
    {
        ThrowIfDisposed(); ArgumentNullException.ThrowIfNull(canvas);
        // PC explicit positive depth grows towards the far plane; default nearest calls advance away from the front.
        foreach (Entry entry in entries.OrderByDescending(entry => (entry.Draw.DrawFlags & 8) != 0 ? entry.Draw.Depth : entry.Order))
        {
            GodotBitmapGlyphQuad? quad = Project(entry.Draw, State.Font.Glyphs[entry.Draw.GlyphIndex], viewport,
                entry.Horizontal, entry.Vertical, brightness);
            if (quad is null || textures[quad.Value.GlyphIndex] is not { } texture) continue;
            canvas.DrawTextureRectRegion(texture, quad.Value.Destination, quad.Value.Source, quad.Value.Colour);
        }
    }

    internal static GodotBitmapGlyphQuad? Project(BitmapGlyphDraw draw, BitmapFontGlyph glyph, HudViewportTransform viewport,
        HudHorizontalAnchor horizontal, HudVerticalAnchor vertical, IReadOnlyList<byte> brightness)
    {
        float scale = draw.Scale / 4096f;
        int width = (int)(glyph.TextureWidth * scale), height = (int)(glyph.Height * scale);
        if (width == 0 || height == 0) return null;
        // PCGfx_DrawTexture2D clamps the origin and, for SlicedImage2 flags1, the full design viewport.
        int left = Math.Max(draw.X, 0), top = Math.Max(draw.Y, 0);
        int right = Math.Min(unchecked(draw.X + width), 512), bottom = Math.Min(unchecked(draw.Y + height), 240);
        if (right <= left || bottom <= top) return null;
        float xOffset = horizontal switch { HudHorizontalAnchor.Left => 0, HudHorizontalAnchor.Center => viewport.ExtraWidth / 2,
            HudHorizontalAnchor.Right => viewport.ExtraWidth, _ => throw new ArgumentOutOfRangeException(nameof(horizontal)) };
        float yOffset = vertical switch { HudVerticalAnchor.Top => 0, HudVerticalAnchor.Center => viewport.ExtraHeight / 2,
            HudVerticalAnchor.Bottom => viewport.ExtraHeight, _ => throw new ArgumentOutOfRangeException(nameof(vertical)) };
        var destination = new Rect2(xOffset + left * viewport.ScaleX, yOffset + top * viewport.ScaleY,
            (right - left) * viewport.ScaleX, (bottom - top) * viewport.ScaleY);
        var source = new Rect2((left - draw.X) * glyph.TextureWidth / (float)width, (top - draw.Y) * glyph.Height / (float)height,
            (right - left) * glyph.TextureWidth / (float)width, (bottom - top) * glyph.Height / (float)height);
        Color colour = new(brightness[draw.Colour.Red] / 255f, brightness[draw.Colour.Green] / 255f,
            brightness[draw.Colour.Blue] / 255f, 1);
        return new(draw.GlyphIndex, destination, source, colour, draw.Depth, draw.Shadow);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
    private void Release()
    {
        if (disposed) return;
        disposed = true;
        entries.Clear();
        foreach (ImageTexture? texture in textures) texture?.Dispose();
        textures = [];
    }
    public override void _Notification(int what)
    { if (what == NotificationPredelete) Release(); }
}
