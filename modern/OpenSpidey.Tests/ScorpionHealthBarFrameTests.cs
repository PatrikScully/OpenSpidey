using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Presentation;

internal static class ScorpionHealthBarFrameTests
{
    private sealed class Body { public ushort Flags; public short Health; public int Flash; }
    private sealed record Texture(uint Id);
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ScorpionHealthBarFrameTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.scorpion-health-display.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            int[] v = row.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var scorpion = new Body { Health = (short)v[2] }; var jonah = new Body { Health = (short)v[6] };
            var bars = new BossHealthBars<Body, Texture>(body => body.Health);
            bars.Create(jonah, 316, _ => null);
            bars.Create(scorpion, 310, name => name switch { "scorpion" => new(10), "boss" => new(11),
                "jonah" => v[5] != 0 ? new(12) : null, "scorpion_wounded" => new(13), _ => throw new Exception("Unexpected source texture.") });
            if (v[0] == 0) bars.Create(jonah, 316, _ => throw new Exception("The source Jonah branch must not look up textures."));
            scorpion.Flags = (ushort)v[1]; scorpion.Health = (short)v[3]; scorpion.Flash = v[4];
            jonah.Health = (short)v[7]; jonah.Flags = (ushort)v[8];
            var pip = new Texture(14);
            var frame = new SpriteAnimationFrame((sbyte)v[10], (sbyte)v[11], (byte)v[12], (byte)v[13], 14);
            IReadOnlyList<BossHealthBarPrimitive<Texture>> primitives = ScorpionHealthBarFrame.Build(bars,
                body => new(body.Flags, body.Health), body => body.Flash, pip, frame, v[9]);
            JsonElement expected = row.GetProperty("expected");
            var actual = primitives.Select(primitive => Draw(primitive, v[14], v[15])).SelectMany(value => value).ToArray();
            assert(actual.SequenceEqual(expected.GetProperty("draws").EnumerateArray().SelectMany(draw => draw.EnumerateArray().Select(value => value.GetUInt32()))),
                $"Whole original464270 display{index++} preserves signed health widths,3EC portrait selection, all sprite short coordinates/offsets, quad order, UVs, four colours, depth and PC blend submission.");
            int primary = bars.Primary is null ? 0 : 1, secondary = bars.Secondary is null ? 0 : 1;
            assert(primary == expected.GetProperty("owners")[0].GetInt32() && secondary == expected.GetProperty("owners")[1].GetInt32(),
                "Primary flag40 clears both shared panel identities before display; absent primary and dead Jonah preserve the source's asymmetric guards.");
        }
    }

    private static uint[] Draw(BossHealthBarPrimitive<Texture> primitive, int width, int height)
    {
        uint texture, blend, left, right;
        float x, y, w, h;
        switch (primitive)
        {
            case BossHealthBarSprite<Texture> sprite:
                texture = sprite.Texture.Id; blend = 1; left = right = 0xFF808080;
                x = sprite.Quad.Left; y = sprite.Quad.Top; w = sprite.Quad.Right - sprite.Quad.Left; h = sprite.Quad.Bottom - sprite.Quad.Top;
                break;
            case BossHealthBarQuad<Texture> quad:
                texture = 1; blend = quad.Depth == 3 ? 0u : 1u;
                left = Argb(quad.LeftColour); right = Argb(quad.RightColour);
                x = quad.Rectangle.X; y = quad.Rectangle.Y; w = quad.Rectangle.Width; h = quad.Rectangle.Height;
                break;
            default: throw new Exception("Unexpected source boss primitive.");
        }
        float sx = width / 512f, sy = height / 240f;
        float x0 = x * sx, y0 = y * sy;
        // The flat helper scales its width before adding it; original gouraud/icons scale their short/source endpoints.
        float x1 = primitive.Depth == 3 ? x0 + w * sx : (x + w) * sx;
        float y1 = primitive.Depth == 3 ? y0 + h * sy : (y + h) * sy;
        return [texture, blend, Bits(primitive.Depth), Bits(x0), Bits(y0), Bits(x1), Bits(y0), Bits(x0), Bits(y1), Bits(x1), Bits(y1),
            Bits(0), Bits(0), Bits(1), Bits(0), Bits(0), Bits(1), Bits(1), Bits(1), left, right, left, right];
    }
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);
    private static uint Argb(uint bgr) => 0xFF000000 | ((bgr & 255) << 16) | (bgr & 0xFF00) | ((bgr >> 16) & 255);
}
