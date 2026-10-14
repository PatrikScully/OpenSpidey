using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.UI;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.UI;

/// <summary>Original player sprites, meters and bitmap lettering in one proportional viewport group.</summary>
public partial class GodotPlayerHud : Node2D
{
    private readonly Dictionary<uint, ImageTexture> textures = [];
    private SpriteAnimationFrame[] spFrames = [];
    private SpriteAnimationFrame cartridge;
    private GodotBitmapText text = null!;
    private GodotPlayer player = null!;
    private Func<uint> clock = null!;
    private bool disposed;
    private GodotPlayerPowerUpResources? powerUps;
    public uint LastHealthPickupClock { get; set; }
    public int ScreenOffsetY { get; set; }
    public Func<int>? SourceScreenOffsetY { get; set; }
    public bool Enabled { get; set; } = true;
    public MissionCountdown? Countdown { get; set; }
    public GodotMissionTimerPresenter? Timer { get; set; }
    internal IReadOnlyDictionary<uint, ImageTexture> Textures => textures;
    internal IReadOnlyList<SpriteAnimationFrame> SpFrames => spFrames;
    internal GodotBitmapText Text => text;
    internal bool ResourcesDisposed => disposed;

    public static GodotPlayerHud Create(PkrArchive archive, GodotPlayer player, Func<uint> clock)
    {
        ArgumentNullException.ThrowIfNull(archive); ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(clock);
        if (player.Inventory is null) throw new InvalidOperationException("Player HUD requires the actual source web inventory.");
        byte[] bits = archive.Read("data/bits.psx");
        IReadOnlyList<SpriteAnimation> animations = SpriteAnimationReader.Read(bits);
        var hud = new GodotPlayerHud { Name = "PlayerHud", player = player, clock = clock,
            TextureFilter = TextureFilterEnum.Linear,
            spFrames = animations.Single(animation => animation.Name.Equals("Sp", StringComparison.OrdinalIgnoreCase)).Frames.ToArray(),
            cartridge = animations.Single(animation => animation.Name.Equals("Webcart", StringComparison.OrdinalIgnoreCase)).Frames[0] };
        try
        {
            IReadOnlyList<DecodedTexture> decoded = PsxTextureReader.Read(bits);
            foreach (uint checksum in hud.spFrames.Select(frame => frame.TextureChecksum).Append(hud.cartridge.TextureChecksum).Distinct())
            {
                DecodedTexture texture = decoded.Single(texture => texture.Checksum == checksum);
                using Image image = Image.CreateFromData(texture.Width, texture.Height, false, Image.Format.Rgba8, texture.RgbaPixels);
                hud.textures.Add(checksum, ImageTexture.CreateFromImage(image));
            }
            hud.text = GodotBitmapText.Create(OriginalBitmapFontReader.Read(archive.Read("data/sp_fnt00.fnt"), "sp_fnt00.fnt"));
            hud.text.State.Scale = 8 * 256; // Mess_SetScale(256), used by Panel_Display.
            hud.text.State.HorizontalAlignment = BitmapHorizontalAlignment.Left; // Mess_SetTextJustify(1).
            hud.text.State.Red = hud.text.State.Green = hud.text.State.Blue = 128;
            // The owning draw callback supplies the original independent depth ordering.
            hud.text.Visible = false;
            hud.AddChild(hud.text);
            return hud;
        }
        catch { hud.Release(); hud.Free(); throw; }
    }

    public void BindPowerUpResources(GodotPlayerPowerUpResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (powerUps is not null && !ReferenceEquals(powerUps, resources)) throw new InvalidOperationException("The HUD already has its source pickup resource owner.");
        foreach (DecodedTexture decoded in resources.HealthIconTextures)
        {
            if (textures.ContainsKey(decoded.Checksum)) continue;
            using Image image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.RgbaPixels);
            textures.Add(decoded.Checksum, ImageTexture.CreateFromImage(image));
        }
        powerUps = resources;
    }

    internal IReadOnlyList<PlayerHudPrimitive> CurrentFrame() => PlayerHudFrame.Build(player.Combat.State,
        player.Inventory!.State, clock(), powerUps?.Collection.LastHealthPickupClock ?? LastHealthPickupClock,
        spFrames, cartridge, ScreenOffsetY, powerUps?.HealthIcon);

    public override void _Ready() => text.SetProcess(false);
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        if (disposed) return;
        if (SourceScreenOffsetY is { } readOffset) ScreenOffsetY = readOffset();
        Countdown?.Update(clock(), player.Combat.State.FieldE18 != 0 || player.Combat.State.SynthesizedInputActive);
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (size.X <= 0 || size.Y <= 0) return;
        var viewport = new HudViewportTransform(size.X, size.Y);
        Timer?.DrawOn(this, viewport, ScreenOffsetY);
        if (!Enabled || player.Scripts?.Active == true || player.Combat.State.FieldE18 != 0) return;
        foreach (PlayerHudPrimitive primitive in CurrentFrame().OrderByDescending(primitive => primitive.Depth))
            switch (primitive)
            {
                case PlayerHudSprite sprite:
                    DrawTextureRect(textures[sprite.Frame.TextureChecksum], Rectangle(viewport.Project(sprite.Quad)), false,
                        new(Shade(sprite.Red), Shade(sprite.Green), Shade(sprite.Blue)));
                    break;
                case PlayerHudQuad quad:
                    ScreenRectangle projected = viewport.Project(quad.Rectangle);
                    if (projected.Width == 0 || projected.Height == 0) break;
                    Vector2[] points = [new(projected.X, projected.Y), new(projected.X + projected.Width, projected.Y),
                        new(projected.X + projected.Width, projected.Y + projected.Height), new(projected.X, projected.Y + projected.Height)];
                    Color left = Colour(quad.LeftColour), right = Colour(quad.RightColour);
                    DrawPolygon(points, [left, right, right, left]);
                    break;
                case PlayerHudText lettering:
                    if (Timer is not null) Timer.SourceTextSort = 4093;
                    text.ShowText(lettering.X, lettering.Y, lettering.Text.Span, depth: lettering.Depth);
                    text.DrawOn(this, viewport);
                    break;
            }
    }

    private static Rect2 Rectangle(ScreenRectangle rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    private static float Shade(byte channel) => Math.Min(channel * 2, 255) / 255f;
    private static Color Colour(uint sourceBgr) => new(Shade((byte)sourceBgr), Shade((byte)(sourceBgr >> 8)), Shade((byte)(sourceBgr >> 16)));
    private void Release()
    {
        if (disposed) return;
        disposed = true;
        foreach (ImageTexture texture in textures.Values) texture.Dispose();
        textures.Clear();
    }
    public override void _Notification(int what)
    { if (what == NotificationPredelete) Release(); }
}
