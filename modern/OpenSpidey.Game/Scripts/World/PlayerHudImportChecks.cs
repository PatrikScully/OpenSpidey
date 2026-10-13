using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.World;

internal static class PlayerHudImportChecks
{
    public static async Task VerifyAsync(Node owner, PkrArchive archive, GodotPlayer player)
    {
        IReadOnlyList<DecodedTexture> source = PsxTextureReader.Read(archive.Read("data/bits.psx"));
        foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
        {
            var viewport = new SubViewport { Size = size, Disable3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            owner.AddChild(viewport);
            GodotPlayerHud hud = GodotPlayerHud.Create(archive, player, () => 1000);
            viewport.AddChild(hud);
            try
            {
                foreach ((uint checksum, ImageTexture uploaded) in hud.Textures)
                {
                    DecodedTexture original = source.Single(texture => texture.Checksum == checksum);
                    using Image pixels = uploaded.GetImage();
                    if (pixels.GetWidth() != original.Width || pixels.GetHeight() != original.Height ||
                        !pixels.GetData().AsSpan().SequenceEqual(original.RgbaPixels))
                        throw new InvalidDataException("Player HUD must retain each actual original Sp/Webcart texture and palette.");
                }
                PlayerHudSprite face = hud.CurrentFrame().OfType<PlayerHudSprite>().Single(sprite => sprite.Frame == hud.SpFrames[0]);
                if (face.Quad != new HudSourceQuad(15, 13, 60, 43))
                    throw new InvalidDataException("Native health icon differs from independently verified PC source vertices.");
                var transform = new HudViewportTransform(size.X, size.Y);
                ScreenRectangle rectangle = transform.Project(face.Quad);
                float scale = size.Y / 480f;
                if (rectangle != new ScreenRectangle(18.75f * scale, 26 * scale, 56.25f * scale, 60 * scale))
                    throw new InvalidDataException("Wide native health icon lost original aspect, anchor or group proportions.");
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using Image capture = viewport.GetTexture().GetImage();
                byte[] data = capture.GetData();
                int visible = 0;
                for (int pixel = 0; pixel < data.Length; pixel += 4)
                {
                    if (data[pixel] != 0 || data[pixel + 1] != 0 || data[pixel + 2] != 0) visible++;
                    if (data[pixel] > 250 && data[pixel + 1] < 5 && data[pixel + 2] > 250)
                        throw new InvalidDataException("The native HUD rendered its source magenta transparency key as a visible background.");
                }
                if (visible < 256 || hud.Text.GlyphDrawCount != 4)
                    throw new InvalidDataException("Native player HUD did not render its original sprites, meters and both cartridge digits/shadows.");
                string path = $"/tmp/openspidey-hud-{size.X}x{size.Y}.png";
                if (capture.SavePng(path) != Error.Ok) throw new IOException("Could not save native HUD comparison capture.");
            }
            finally { viewport.Free(); }
            if (!hud.ResourcesDisposed) throw new InvalidDataException("Native HUD teardown leaked its owned sprite textures.");
        }
        GD.Print("Verified native player HUD: genuine Sp/Webcart/glyph pixels, PC source vertices, proportional4:3/wide group layout, meters, cartridge digits and resource cleanup.");
    }
}
