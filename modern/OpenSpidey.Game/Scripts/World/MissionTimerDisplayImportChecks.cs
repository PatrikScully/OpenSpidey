using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.UI;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.World;

internal static class MissionTimerDisplayImportChecks
{
    public static async Task VerifyAsync(Node owner, PkrArchive archive, GodotPlayer player)
    {
        byte[] bits = archive.Read("data/bits.psx"), fontBytes = archive.Read("data/sp_fnt01.fnt");
        OriginalBitmapFont originalFont = OriginalBitmapFontReader.Read(fontBytes, "sp_fnt01.fnt");
        foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
        {
            var viewport = new SubViewport { Name = "OriginalTimerDisplayProof", Size = size, Disable3D = true,
                TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            owner.AddChild(viewport);
            var hud = GodotPlayerHud.Create(archive, player, () => 1000); hud.Enabled = false; viewport.AddChild(hud);
            hud.SetProcess(false);
            var countdown = new MissionCountdown { DisplayEnabled = 1, RemainingTicks = 599 };
            int secondsSounds = 0;
            var sourceRandom = new GameRandom(0x461D00);
            var presenter = GodotMissionTimerPresenter.Create(archive, countdown, new MissionTimerDisplayState(), hud.Text, new()
            {
                LevelId = () => 0x101, PlayerPresent = () => true, PlayerScriptGate = () => player.Combat.State.FieldE18,
                PlayerSynthesizedInput = () => player.Combat.State.SynthesizedInputActive, Random = sourceRandom.Next,
                PlaySound = (sound, volume, pitch) =>
                {
                    if (sound != 32 || volume != 8192 || pitch != 0) throw new InvalidDataException("Original timer second sound contract changed.");
                    secondsSounds++; player.Combat.PlaySound(sound, volume, pitch);
                },
                CallModule = (_, _) => throw new InvalidOperationException("Ordinary L1A1 timer proof entered an excluded special-level relocation.")
            });
            var canvas = new MissionTimerProofCanvas { Presenter = presenter }; viewport.AddChild(canvas); canvas.AddChild(presenter);
            presenter.State.FirstScanActive = presenter.State.SecondScanActive = 1;
            presenter.State.FirstScanPosition = 17 << 12; presenter.State.SecondScanPosition = 23 << 12;
            try
            {
                DecodedTexture border = PsxTextureReader.Read(bits).Single(texture => texture.Checksum == presenter.SourceBorderFrame.TextureChecksum);
                using (Image image = presenter.SourceBorderTexture.GetImage())
                    Require(image.GetWidth() == border.Width && image.GetHeight() == border.Height && image.GetData().SequenceEqual(border.RgbaPixels),
                        "Timer must upload the genuine Misc19 border texture and palette.");
                GodotBitmapText nativeFont = presenter.SourceTimerFont;
                for (int index = 0; index < originalFont.Glyphs.Count; index++)
                {
                    BitmapFontGlyph glyph = originalFont.Glyphs[index]; if (glyph.Height == 0) continue;
                    using Image image = nativeFont.GlyphTextures[index]!.GetImage();
                    Require(image.GetWidth() == glyph.TextureWidth && image.GetHeight() == glyph.Height && image.GetData().AsSpan().SequenceEqual(glyph.RgbaPixels.Span),
                        "Timer must retain every original sp_fnt01 padded glyph image and palette.");
                }
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(canvas.FrameCount > 0 && presenter.PrimitiveCount == 6 && secondsSounds == 1 && presenter.State.LastSeconds == 9 && countdown.RemainingTicks == 599,
                    "Actual timer must draw two borders, its five source digits/colon, background and both scan bars, with one genuine seconds sound and no display-owned countdown decay.");
                Require(nativeFont.GlyphDrawCount == 10 && nativeFont.State.Scale == 2048 && nativeFont.State.Red == 128 && nativeFont.State.Green == 128 && nativeFont.State.Blue == 128 &&
                    nativeFont.State.CharacterMap == 0 && nativeFont.State.VerticalAlignment == BitmapVerticalAlignment.Baseline && nativeFont.State.ShadowEnabled,
                    "Mess_SetCurrentFont must replace pre-switch RGB96 with the genuine font01 preset128, retaining original scale2048, baseline, ordinary map and five shadows.");
                Require(presenter.State.FirstScanPosition == (17 << 12) + canvas.FrameCount * 3798 && presenter.State.SecondScanPosition == (23 << 12) + canvas.FrameCount * 2334,
                    "Both source scan bars must advance once per actual HUD draw, independently of source countdown clock.");
                using Image capture = viewport.GetTexture().GetImage();
                byte[] data = capture.GetData(); int visible = 0;
                var transform = new HudViewportTransform(size.X, size.Y);
                ScreenRectangle envelope = transform.Project(new ScreenRectangle(215, 15, 94, 34), HudHorizontalAnchor.Center);
                for (int pixel = 0; pixel < data.Length; pixel += 4)
                {
                    if (data[pixel + 3] == 0) continue;
                    int index = pixel / 4, x = index % size.X, y = index / size.X; visible++;
                    Require(x >= envelope.X - 1 && x <= envelope.X + envelope.Width + 1 && y >= envelope.Y - 1 && y <= envelope.Y + envelope.Height + 1,
                        "The native widescreen timer must keep its complete proportional source group at the viewport centre.");
                }
                Require(visible >= 512, "Original timer text, border and scan bars did not render visible pixels.");
                string path = $"/tmp/openspidey-timer-{size.X}x{size.Y}.png";
                if (capture.SavePng(path) != Error.Ok) throw new IOException("Could not save original timer display comparison.");
            }
            finally { viewport.Free(); }
            Require(presenter.ResourcesDisposed && presenter.SourceTimerFont.ResourcesDisposed && hud.ResourcesDisposed,
                "Timer teardown must release its original border/font textures and the actual registered HUD source font.");
        }
        GD.Print("Verified native mission timer: actual Misc19/sp_fnt01 pixels, unsigned00:09 digits, true source font preset and shadows, seconds audio, per-render scan bars, intact4:3/widescreen group and cleanup.");
    }

    private static void Require(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native panel timer: {evidence}"); }
}

internal partial class MissionTimerProofCanvas : Node2D
{
    public GodotMissionTimerPresenter Presenter { get; set; } = null!;
    public int FrameCount { get; private set; }
    public override void _Draw()
    {
        FrameCount++;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        Presenter.DrawOn(this, new HudViewportTransform(size.X, size.Y), 0);
    }
}
