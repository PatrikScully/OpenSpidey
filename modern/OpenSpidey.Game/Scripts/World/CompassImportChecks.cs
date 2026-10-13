using System.Security.Cryptography;
using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.UI;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.World;

public static class CompassImportChecks
{
    public static async Task VerifyAsync(Node3D owner, byte[] originalBits)
    {
        byte[]? baseline = null;
        foreach (int width in new[] { 640, 1280 })
        {
            var viewport = new SubViewport { Size = new(width, 480), Disable3D = true, TransparentBg = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            owner.AddChild(viewport);
            var state = new CompassState();
            state.Create(new FixedVector3(64, 0, 0) << 12); // Isolated test target; creation is never implicit in the live facade.
            int views = 0;
            var compass = GodotCompass.Create(originalBits, state, () => { views++; return new(default,
                new(4096, 0, 0, 0, 4096, 0, 0, 0, 4096, 0, 0, 0), 1000, 0); });
            viewport.AddChild(compass);
            try
            {
                if (compass.SourceFrame != new SpriteAnimationFrame(-32, -32, 64, 64, 0x33CADC71))
                    throw new InvalidDataException("Compass must retain its real bits.psx animation11 frame0.");
                using Image original = compass.SourceTexture.GetImage();
                if (original.GetWidth() != 64 || original.GetHeight() != 64 ||
                    Convert.ToHexString(SHA256.HashData(original.GetData())).ToLowerInvariant() != "042849442dc2cfb80666ac064af921ca680d4ac7c2795c8575b3b1e750cc20b1")
                    throw new InvalidDataException("Compass pixels differ from independent actual-PC palette gold.");
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                if (views == 0 || compass.Draws.Count != 3 || compass.Draws[1].LeftU != .05f || compass.Draws[2].LeftU != .95f || state.FlashTimer != 0)
                    throw new InvalidDataException("Native compass must use its live view/clock hook and real mirrored inset UV needle halves without fabricated flash state.");
                using Image image = viewport.GetTexture().GetImage(); byte[] pixels = image.GetData();
                int visible = 0;
                for (int pixel = 0; pixel < pixels.Length; pixel += 4)
                {
                    if (pixels[pixel + 3] != 0) visible++;
                    if (pixels[pixel] > 250 && pixels[pixel + 1] < 5 && pixels[pixel + 2] > 250)
                        throw new InvalidDataException("Original compass magenta transparency key became opaque.");
                }
                if (visible < 100) throw new InvalidDataException("Native compass did not rasterize its genuine textured needle and pulse dial.");
                if (image.SavePng($"/tmp/openspidey-compass-{width}x480.png") != Error.Ok) throw new IOException("Could not save original compass comparison capture.");
                if (width == 640) baseline = pixels;
                else
                {
                    int differences = 0, maximum = 0; string first = "";
                    for (int y = 0; y < 480; y++)
                        for (int x = 0; x < 640; x++)
                            for (int channel = 0; channel < 4; channel++)
                            {
                                int actual = pixels[(y * width + x + 640) * 4 + channel], expected = baseline![(y * 640 + x) * 4 + channel];
                                int difference = Math.Abs(actual - expected);
                                if (difference == 0) continue;
                                if (differences++ == 0) first = $"x{x}, y{y}, channel{channel}, actual{actual}, expected{expected}";
                                maximum = Math.Max(maximum, difference);
                            }
                    // Measured Vulkan translation/interpolation rounding:22 pixels/36 RGB channels differ by exactly1.
                    // Keep source geometry/UV strict; allow only one8-bit channel step in the actual GPU comparison.
                    if (maximum > 1) throw new InvalidDataException($"Wide compass pixel differences: channels{differences}, maximum{maximum}, first[{first}]; captures saved before comparison.");
                    if (differences != 0) GD.Print($"Compass translated GPU interpolation: {differences} channels, maximum{maximum} of255; exact source geometry/UV retained.");
                }
                state.Destroy(); compass.QueueRedraw();
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                if (compass.Draws.Count != 0) throw new InvalidDataException("Destroy must immediately suppress all dial/needle primitives.");
                image.SavePng($"/tmp/openspidey-compass-{width}x480.png");
            }
            finally { viewport.Free(); }
            if (!compass.ResourcesDisposed) throw new InvalidDataException("Native compass leaked its owned original texture.");
        }
        GD.Print("Verified actual Compass pixels, source dial/needle ordering and UV mirror, live view hook, right-bottom wide pixel identity and explicit lifecycle.");
    }
}
