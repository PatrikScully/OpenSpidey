using System.Text;
using Godot;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Core.Frontend;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.UI;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.Frontend;

public enum FrontendFont { Normal, Small, Big }

/// <summary>
/// One shell frame in the 512x240 design space, centred as a 4:3 frame: textured polys, the additive title bar and Mess text.
/// Mess state mirrors mess.cpp: justify 0 centre, 1 left, 2 right; only the top colour reaches the PC font draw.
/// </summary>
public partial class FrontendCanvas : Node2D
{
    private sealed record Quad(Texture2D Texture, Rect2 Design, Rect2 Uv, Color Colour);
    private sealed record Glow(Texture2D Texture, int X0, int Y0, int X1, int Y1);
    private readonly List<Quad> quads = [];
    private readonly List<Glow> glows = [];
    private readonly Dictionary<FrontendFont, GodotBitmapText> fonts = [];
    private Node2D back = null!, glow = null!;
    public FrontendFont Font { get; set; }
    public byte Justify { get; set; }
    public byte Red { get; set; } = 0x80;
    public byte Green { get; set; } = 0x80;
    public byte Blue { get; set; } = 0x80;
    public bool Shadows { get; set; } = true;
    public int GlyphDraws => fonts.Values.Sum(font => font.GlyphDrawCount);
    public int QuadDraws => quads.Count + glows.Count;

    public static FrontendCanvas Create(OriginalBitmapFont normal, OriginalBitmapFont? small = null, OriginalBitmapFont? big = null)
    {
        var canvas = new FrontendCanvas { Name = "FrontendCanvas", TextureFilter = TextureFilterEnum.Nearest };
        canvas.back = new Node2D { Name = "Polys" };
        canvas.back.Draw += () => canvas.DrawQuads(canvas.back);
        canvas.AddChild(canvas.back);
        canvas.glow = new Node2D { Name = "TitleBars", Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
        canvas.glow.Draw += () => canvas.DrawGlows(canvas.glow);
        canvas.AddChild(canvas.glow);
        canvas.AddFont(FrontendFont.Normal, normal);
        if (small is not null) canvas.AddFont(FrontendFont.Small, small);
        if (big is not null) canvas.AddFont(FrontendFont.Big, big);
        return canvas;
    }

    private void AddFont(FrontendFont kind, OriginalBitmapFont font)
    {
        GodotBitmapText text = GodotBitmapText.Create(font);
        text.Name = kind + "Font";
        text.State.Scale = 8 * 256;
        fonts.Add(kind, text);
        AddChild(text);
    }

    public void Begin()
    {
        quads.Clear();
        glows.Clear();
        foreach (GodotBitmapText font in fonts.Values) font.Clear();
        back.QueueRedraw();
        glow.QueueRedraw();
    }

    /// <summary>PShell_DefaultText 0x47A3B0.</summary>
    public void DefaultText()
    {
        Font = FrontendFont.Normal;
        Justify = 0;
        Colour(0x80, 0x80, 0x80);
    }

    /// <summary>PShell_InstructionalText 0x47A3F0.</summary>
    public void InstructionalText()
    {
        Font = FrontendFont.Small;
        Justify = 0;
        Colour(0x45, 0x3C, 0x6B);
    }

    public void Colour(byte red, byte green, byte blue) => (Red, Green, Blue) = (red, green, blue);

    public void Sprite(Texture2D texture, int x, int y, int width, int height, byte brightness = 128) =>
        quads.Add(new(texture, new(x, y, width, height), new(0, 0, 1, 1), Brightness(brightness)));

    /// <summary>Panel frame draws use the frame's own texture rectangle.</summary>
    public void Sprite(Texture2D texture, Rect2 uv, int x, int y, int width, int height, byte brightness = 128) =>
        quads.Add(new(texture, new(x, y, width, height), uv, Brightness(brightness)));

    /// <summary>PShell_DrawHighlight 0x47A590: additive GT4, (0x40,0x40,0x7B) at a1 fading to black, y from a2+3 to a2+a4+7.</summary>
    public void Highlight(Texture2D texture, int x, int y, int width, int height) =>
        glows.Add(new(texture, x, y + 3, x + width, y + 3 + height + 4));

    /// <summary>PShell_DrawMenuBox 0x47Axxx nine-slice menu chrome; shell PSX slices are not wired here yet.</summary>
    public void DrawMenuBox(MenuBoxDrawCall call) =>
        throw new NotSupportedException("PShell_DrawMenuBox needs the recovered gMenuBoxSlices shell PSX frames before menu zoom boxes can render.");

    public int TextWidth(string text) => Require(Font).Layout.Width(Ascii(text));

    /// <summary>Mess_DrawText 0x458700 with the current font, justification, top colour and shadows.</summary>
    public void Text(int x, int y, string text)
    {
        GodotBitmapText font = Require(Font);
        font.State.HorizontalAlignment = Justify switch
        {
            0 => BitmapHorizontalAlignment.Centre,
            1 => BitmapHorizontalAlignment.Left,
            2 => BitmapHorizontalAlignment.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(Justify), "Mess_DrawText maps justifications 0, 1 and 2.")
        };
        font.State.Red = Red;
        font.State.Green = Green;
        font.State.Blue = Blue;
        font.State.ShadowEnabled = Shadows;
        font.AddText(x, y, Ascii(text), HudHorizontalAnchor.Center, HudVerticalAnchor.Center);
    }

    private GodotBitmapText Require(FrontendFont font) => fonts.TryGetValue(font, out GodotBitmapText? text)
        ? text : throw new InvalidOperationException($"The {font} shell font is not loaded for this canvas.");

    private static byte[] Ascii(string text) => Encoding.Latin1.GetBytes(text);
    private static Color Brightness(byte value) => new(Math.Min(value * 2, 255) / 255f, Math.Min(value * 2, 255) / 255f, Math.Min(value * 2, 255) / 255f);

    private HudViewportTransform Viewport()
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        return new(size.X, size.Y);
    }

    private void DrawQuads(CanvasItem target)
    {
        HudViewportTransform viewport = Viewport();
        foreach (Quad quad in quads)
        {
            ScreenRectangle screen = viewport.Project(new ScreenRectangle(quad.Design.Position.X, quad.Design.Position.Y,
                quad.Design.Size.X, quad.Design.Size.Y), HudHorizontalAnchor.Center, HudVerticalAnchor.Center);
            Vector2 size = quad.Texture.GetSize();
            target.DrawTextureRectRegion(quad.Texture, new(screen.X, screen.Y, screen.Width, screen.Height),
                new(quad.Uv.Position * size, quad.Uv.Size * size), quad.Colour);
        }
    }

    private void DrawGlows(CanvasItem target)
    {
        HudViewportTransform viewport = Viewport();
        var bright = new Color(0x40 / 128f, 0x40 / 128f, 0x7B / 128f);
        foreach (Glow bar in glows)
        {
            ScreenRectangle a = viewport.Project(new ScreenRectangle(bar.X0, bar.Y0, 0, 0), HudHorizontalAnchor.Center, HudVerticalAnchor.Center);
            ScreenRectangle b = viewport.Project(new ScreenRectangle(bar.X1, bar.Y1, 0, 0), HudHorizontalAnchor.Center, HudVerticalAnchor.Center);
            float uMax = (bar.Texture.GetWidth() - 1) / (float)bar.Texture.GetWidth();
            target.DrawPolygon([new(a.X, a.Y), new(b.X, a.Y), new(b.X, b.Y), new(a.X, b.Y)],
                [bright, Colors.Black, Colors.Black, bright],
                [new(0, 0), new(uMax, 0), new(uMax, 1), new(0, 1)], bar.Texture);
        }
    }
}
