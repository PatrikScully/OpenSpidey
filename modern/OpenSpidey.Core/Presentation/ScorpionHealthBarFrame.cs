using OpenSpidey.Assets.Animation;

namespace OpenSpidey.Core.Presentation;

public readonly record struct BossHealthBarBody(ushort Flags, short Health);
public abstract record BossHealthBarPrimitive<TTexture>(float Depth) where TTexture : class;
public sealed record BossHealthBarSprite<TTexture>(float Depth, TTexture Texture, HudSourceQuad Quad)
    : BossHealthBarPrimitive<TTexture>(Depth) where TTexture : class;
public sealed record BossHealthBarQuad<TTexture>(float Depth, ScreenRectangle Rectangle, uint LeftColour, uint RightColour)
    : BossHealthBarPrimitive<TTexture>(Depth) where TTexture : class;

/// <summary>Original464270 Scorpion/Jonah presentation, consuming the shared panel owners and canonical body/flash fields.</summary>
public static class ScorpionHealthBarFrame
{
    public static IReadOnlyList<BossHealthBarPrimitive<TTexture>> Build<TBody, TTexture>(BossHealthBars<TBody, TTexture> bars,
        Func<TBody, BossHealthBarBody> readBody, Func<TBody, int> readHitFlashUpdates,
        TTexture pipTexture, SpriteAnimationFrame pipFrame, int screenOffsetY = 0)
        where TBody : class where TTexture : class
    {
        ArgumentNullException.ThrowIfNull(bars);
        ArgumentNullException.ThrowIfNull(readBody);
        ArgumentNullException.ThrowIfNull(readHitFlashUpdates);
        ArgumentNullException.ThrowIfNull(pipTexture);
        if (bars.Primary is not { } primary) return [];
        BossHealthBarBody body = readBody(primary);
        if ((body.Flags & 0x40) != 0) { bars.Destroy(); return []; }
        if (bars.PrimaryType != 310)
            throw new NotSupportedException("This frame owner implements the original Scorpion/Jonah panel display slice.");

        var primitives = new List<BossHealthBarPrimitive<TTexture>>();
        unchecked
        {
            int width = DamageWidth(bars.PrimaryInitialHealth, body.Health, 163);
            TTexture portrait = Require(bars.Textures[readHitFlashUpdates(primary) != 0 ? 3 : 0]);
            Texture(portrait, 448, screenOffsetY + 16, 28, 31);
            TTexture label = Require(bars.Textures[1]);
            Texture(label, 283, screenOffsetY + 24, 12, 16);
            for (int x = 0; x < 150; x += 25) Pip(x + 325, screenOffsetY + 40, 16, 16);
            Meter(288, screenOffsetY + 29, width, bars.PrimaryInitialHealth, 81, 82);

            if (bars.Textures[2] is { } jonahTexture)
            {
                TBody secondary = bars.Secondary ?? throw new InvalidOperationException("The original Jonah texture gate requires its real secondary panel body.");
                Texture(jonahTexture, 448, screenOffsetY + 45, 28, 30);
                Texture(label, 407, screenOffsetY + 53, 12, 15);
                Pip(450, screenOffsetY + 69, 16, 15);
                // Original464270 does not inspect Jonah's dead flag; its signed health remains visible until primary destruction.
                int jonahWidth = DamageWidth(bars.SecondaryInitialHealth, readBody(secondary).Health, 38);
                Meter(413, screenOffsetY + 57, jonahWidth, bars.SecondaryInitialHealth, 19, 19);
            }
        }
        return primitives.AsReadOnly();

        void Texture(TTexture texture, int x, int y, int width, int height) =>
            primitives.Add(new BossHealthBarSprite<TTexture>(1, texture, HudSpriteLayout.Stretch(default, x, y, width, height)));
        void Pip(int x, int y, int width, int height) =>
            primitives.Add(new BossHealthBarSprite<TTexture>(1, pipTexture, HudSpriteLayout.Stretch(pipFrame, x, y, width, height)));
        void Meter(int x, int y, int width, int initialHealth, int firstHalf, int secondHalf)
        {
            if (width != 0) Quad(3, x, y, width, 0, 0);
            // Preserve the actual PC comparison against captured health, rather than replacing it with a half-width threshold.
            if (width <= initialHealth / 2) Quad(4, x, y, firstHalf, 0x0000FF00, 0x0000FFFF);
            Quad(4, x + firstHalf, y, secondHalf, 0x0000FFFF, 0x000000FF);
        }
        void Quad(float depth, int x, int y, int width, uint left, uint right) =>
            primitives.Add(new BossHealthBarQuad<TTexture>(depth, new(x, y, width, 8), left, right));
        static TTexture Require(TTexture? texture) => texture ?? throw new InvalidDataException("The original boss display requires its cached real portrait/label texture.");
    }

    private static int DamageWidth(int initialHealth, short health, int fullWidth) =>
        unchecked(fullWidth * (unchecked((initialHealth - health) << 7) / initialHealth)) / 128;
}
