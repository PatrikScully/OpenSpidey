using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.UI;

namespace OpenSpidey.Game.UI;

public sealed class GodotMissionTimerConfiguration
{
    public required Func<int> LevelId { get; init; }
    public required Func<bool> PlayerPresent { get; init; }
    public required Func<int> PlayerScriptGate { get; init; }
    public required Func<bool> PlayerSynthesizedInput { get; init; }
    public required Func<int, int> Random { get; init; }
    public required Action<ushort, short, int> PlaySound { get; init; }
    public required Action<string, uint> CallModule { get; init; }
    public Action? BeginModulePresentation { get; init; }
    public Action<CanvasItem, HudViewportTransform>? DrawModulePresentation { get; init; }
}

/// <summary>Actual timer border/font/scan bars in the HUD draw phase, using the shared source countdown and module callbacks.</summary>
public partial class GodotMissionTimerPresenter : Node, IMissionTimerDisplayHost
{
    private sealed record Draw(float Depth, Action<CanvasItem, HudViewportTransform> Submit);
    private readonly List<Draw> draws = [];
    private readonly Dictionary<GodotBitmapText, FontPreset> fontPresets = [];
    private MissionCountdown countdown = null!;
    private GodotMissionTimerConfiguration configuration = null!;
    private GodotBitmapText mainFont = null!, timerFont = null!, currentFont = null!;
    private ImageTexture borderTexture = null!;
    private SpriteAnimationFrame borderFrame;
    private ushort textScale = 256;
    private byte textJustification = 1;
    private bool disposed;
    public MissionTimerDisplayState State { get; private set; } = null!;
    public ushort SourceTextSort { get; set; } = 4095;
    public int LevelId => configuration.LevelId();
    public bool PlayerPresent => configuration.PlayerPresent();
    public int PlayerScriptGate => configuration.PlayerScriptGate();
    public bool PlayerSynthesizedInput => configuration.PlayerSynthesizedInput();
    public bool BorderTexturePresent => true;
    internal SpriteAnimationFrame SourceBorderFrame => borderFrame;
    internal ImageTexture SourceBorderTexture => borderTexture;
    internal GodotBitmapText SourceTimerFont => timerFont;
    internal bool ResourcesDisposed => disposed;
    internal int PrimitiveCount => draws.Count;

    public static GodotMissionTimerPresenter Create(PkrArchive archive, MissionCountdown countdown, MissionTimerDisplayState state, GodotBitmapText mainFont,
        GodotMissionTimerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(archive); ArgumentNullException.ThrowIfNull(countdown); ArgumentNullException.ThrowIfNull(mainFont);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configuration.LevelId); ArgumentNullException.ThrowIfNull(configuration.PlayerPresent);
        ArgumentNullException.ThrowIfNull(configuration.PlayerScriptGate); ArgumentNullException.ThrowIfNull(configuration.PlayerSynthesizedInput);
        ArgumentNullException.ThrowIfNull(configuration.Random); ArgumentNullException.ThrowIfNull(configuration.PlaySound); ArgumentNullException.ThrowIfNull(configuration.CallModule);
        var result = new GodotMissionTimerPresenter { Name = "OriginalMissionTimer", countdown = countdown, mainFont = mainFont,
            currentFont = mainFont, State = state, configuration = configuration };
        try
        {
            byte[] bits = archive.Read("data/bits.psx");
            result.borderFrame = SpriteAnimationReader.Read(bits).Single(animation => animation.Name.Equals("Misc", StringComparison.OrdinalIgnoreCase)).Frames[19];
            DecodedTexture texture = PsxTextureReader.Read(bits).Single(value => value.Checksum == result.borderFrame.TextureChecksum);
            using Image image = Image.CreateFromData(texture.Width, texture.Height, false, Image.Format.Rgba8, texture.RgbaPixels);
            result.borderTexture = ImageTexture.CreateFromImage(image);
            result.timerFont = GodotBitmapText.Create(OriginalBitmapFontReader.Read(archive.Read("data/sp_fnt01.fnt"), "sp_fnt01.fnt"));
            result.timerFont.Visible = false; result.AddChild(result.timerFont);
            result.fontPresets.Add(mainFont, FontPreset.Capture(mainFont.State));
            result.fontPresets.Add(result.timerFont, FontPreset.Capture(result.timerFont.State));
            return result;
        }
        catch { result.Release(); result.Free(); throw; }
    }

    public override void _Ready() => timerFont.SetProcess(false);
    /// <summary>Call after countdown.Update and before the source HUD-enabled/script-control gate, exactly as Panel_Display does.</summary>
    public void DrawOn(CanvasItem canvas, HudViewportTransform viewport, int screenOffsetY)
    {
        ObjectDisposedException.ThrowIf(disposed, this); ArgumentNullException.ThrowIfNull(canvas);
        draws.Clear(); timerFont.Clear();
        configuration.BeginModulePresentation?.Invoke();
        MissionTimerDisplay.Display(countdown, State, screenOffsetY, this);
        foreach (Draw draw in draws.OrderByDescending(draw => draw.Depth)) draw.Submit(canvas, viewport);
        configuration.DrawModulePresentation?.Invoke(canvas, viewport);
    }
    public int Random(int range) => configuration.Random(range);
    public void CallModule(string module, uint function) => configuration.CallModule(module, function);
    public void PlaySound(ushort sound, short volume, int pitch) => configuration.PlaySound(sound, volume, pitch);
    public void DrawBorder(int x, int y, int width, int height, bool mirrored)
    {
        // Texture overload has no SAnimFrame offsets. Its original 6554 rounding turns width27 into43.
        int stretched = unchecked(width * 6554);
        if ((stretched & 4095) >= 2048) stretched = unchecked(stretched + 4096);
        stretched >>= 12;
        HudSourceQuad quad = new(unchecked((short)x), unchecked((short)y), unchecked((short)(x + stretched)), unchecked((short)(y + height)));
        draws.Add(new(.6f, (canvas, viewport) =>
        {
            ScreenRectangle rectangle = viewport.Project(quad, HudHorizontalAnchor.Center);
            Vector2[] points = [new(rectangle.X, rectangle.Y), new(rectangle.X + rectangle.Width, rectangle.Y),
                new(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height), new(rectangle.X, rectangle.Y + rectangle.Height)];
            Vector2[] uvs = mirrored ? [new(1, 0), new(0, 0), new(0, 1), new(1, 1)] : [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];
            Color[] colours = [Colors.White, Colors.White, Colors.White, Colors.White];
            canvas.DrawPolygon(points, colours, uvs, borderTexture);
        }));
    }
    public void SetTextScale(int scale) => textScale = unchecked((ushort)scale);
    public void SetTextJustification(byte justification) => textJustification = justification;
    public void SetTextColour(byte red, byte green, byte blue, int argument)
    { currentFont.State.Red = red; currentFont.State.Green = green; currentFont.State.Blue = blue; }
    public string GetCurrentFontName() => currentFont.State.Font.Name;
    public void SetCurrentFont(string name)
    {
        if (name.Equals(timerFont.State.Font.Name, StringComparison.OrdinalIgnoreCase)) currentFont = timerFont;
        else if (name.Equals(mainFont.State.Font.Name, StringComparison.OrdinalIgnoreCase)) currentFont = mainFont;
        else throw new NotSupportedException($"Timer font restore requires its actual registered source font {name}.");
        fontPresets[currentFont].Restore(currentFont.State);
    }
    public void DrawText(int x, int y, ReadOnlyMemory<byte> text, int argument, uint depth)
    {
        currentFont.State.Scale = textScale * 8;
        currentFont.State.HorizontalAlignment = textJustification switch { 0 => BitmapHorizontalAlignment.Centre,
            1 => BitmapHorizontalAlignment.Left, 2 => BitmapHorizontalAlignment.Right, _ => currentFont.State.HorizontalAlignment };
        float sourceDepth = SourceTextSort switch { 4093 => 6.1f, 4094 => -4, 4095 => -3, _ => 4 };
        GodotBitmapText drawingFont = currentFont;
        drawingFont.ShowText(x, y, text.Span, HudHorizontalAnchor.Center, depth: sourceDepth);
        draws.Add(new(sourceDepth, (canvas, viewport) => drawingFont.DrawOn(canvas, viewport)));
    }
    public void DrawFlat(float depth, int x, int y, int width, int height, byte red, byte green, byte blue, int argument, int blend)
    {
        byte alpha = blend switch { 1 or 2 => 127, 3 => 220, _ => 255 };
        Color colour = new(Math.Min(red * 2, 255) / 255f, Math.Min(green * 2, 255) / 255f, Math.Min(blue * 2, 255) / 255f, alpha / 255f);
        draws.Add(new(depth, (canvas, viewport) =>
        {
            ScreenRectangle rectangle = viewport.Project(new ScreenRectangle(x, y, width, height), HudHorizontalAnchor.Center);
            Vector2[] points = [new(rectangle.X, rectangle.Y), new(rectangle.X + rectangle.Width, rectangle.Y),
                new(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height), new(rectangle.X, rectangle.Y + rectangle.Height)];
            Color[] colours = [colour, colour, colour, colour]; canvas.DrawPolygon(points, colours);
        }));
    }
    private void Release()
    { if (disposed) return; disposed = true; draws.Clear(); fontPresets.Clear(); borderTexture?.Dispose(); }
    public override void _Notification(int what)
    { if (what == NotificationPredelete) Release(); }

    private sealed record FontPreset(int Scale, int Spacing, BitmapHorizontalAlignment Horizontal, BitmapVerticalAlignment Vertical,
        int Map, bool FixedWidth, bool Shadow, int ShadowX, int ShadowY, int ShadowIntensity, uint Red, uint Green, uint Blue, BitmapTextColour Saved)
    {
        public static FontPreset Capture(BitmapFontState state) => new(state.Scale, state.Spacing, state.HorizontalAlignment, state.VerticalAlignment,
            state.CharacterMap, state.FixedWidth, state.ShadowEnabled, state.ShadowOffsetX, state.ShadowOffsetY, state.ShadowIntensity,
            state.Red, state.Green, state.Blue, state.SavedColour);
        public void Restore(BitmapFontState state)
        {
            state.Scale = Scale; state.Spacing = Spacing; state.HorizontalAlignment = Horizontal; state.VerticalAlignment = Vertical;
            state.CharacterMap = Map; state.FixedWidth = FixedWidth; state.ShadowEnabled = Shadow; state.ShadowOffsetX = ShadowX;
            state.ShadowOffsetY = ShadowY; state.ShadowIntensity = ShadowIntensity; state.Red = Red; state.Green = Green; state.Blue = Blue; state.SavedColour = Saved;
        }
    }
}
