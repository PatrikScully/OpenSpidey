using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Presentation;

public abstract record PlayerHudPrimitive(float Depth);
public sealed record PlayerHudSprite(float Depth, SpriteAnimationFrame Frame, HudSourceQuad Quad,
    byte Red, byte Green, byte Blue) : PlayerHudPrimitive(Depth);
public sealed record PlayerHudQuad(float Depth, ScreenRectangle Rectangle, uint LeftColour, uint RightColour) : PlayerHudPrimitive(Depth);
public sealed record PlayerHudText(float Depth, int X, int Y, ReadOnlyMemory<byte> Text) : PlayerHudPrimitive(Depth);

/// <summary>Original player portion of Panel_Display: source icon groups, cartridge flare, health/armor and web meters.</summary>
public static class PlayerHudFrame
{
    public static IReadOnlyList<PlayerHudPrimitive> Build(PlayerCombatState combat, PlayerWebInventory inventory,
        uint clock, uint lastHealthPickupClock, IReadOnlyList<SpriteAnimationFrame> spFrames, SpriteAnimationFrame cartridge,
        int screenOffsetY = 0, SpriteAnimationFrame? healthIcon = null)
    {
        if (combat.MaxHealth <= 0) throw new ArgumentOutOfRangeException(nameof(combat), "A source health meter needs positive maximum health.");
        if (spFrames.Count < 5) throw new ArgumentException("The source Sp sequencer needs all five frames.", nameof(spFrames));
        var draws = new List<PlayerHudPrimitive>();
        unchecked
        {
            HudSourceQuad cartridgeQuad = HudSpriteLayout.Stretch(cartridge, 80, screenOffsetY + 58, 20, 16);
            byte red = 128;
            byte pulse = inventory.FireWebbing != 0 ? (byte)((Math.Abs((int)GameAngle.Heading((int)(clock << 5)).Sine) << 7) >> 12) : (byte)128;
            byte green = pulse, blue = pulse;
            int age = (int)(clock - inventory.LastCartridgeClock);
            if (age < 32)
            {
                red = green = blue = (byte)(255 - 4 * age);
                int growth = (32 - age) << 7;
                int dx = (growth * (cartridgeQuad.Right - cartridgeQuad.Left)) >> 12;
                int dy = (growth * (cartridgeQuad.Bottom - cartridgeQuad.Top)) >> 12;
                cartridgeQuad = new((short)(cartridgeQuad.Left - dx), (short)(cartridgeQuad.Top - dy),
                    (short)(cartridgeQuad.Right + dx), (short)(cartridgeQuad.Bottom + dy));
            }
            byte[] digits = [(byte)(inventory.Cartridges >= 10 ? '1' : '0'),
                (byte)(inventory.Cartridges + (inventory.Cartridges >= 10 ? 38 : 48))];
            draws.Add(new PlayerHudText(6.1f, 95, screenOffsetY + 56, digits));
            draws.Add(new PlayerHudSprite(6, cartridge, cartridgeQuad, red, green, blue));
            Sprite(healthIcon ?? spFrames[0], 67, screenOffsetY + 45, 28, 30);
            Sprite(spFrames[3], 85, screenOffsetY + 36, 15, 15);
            Sprite(spFrames[3], 110, screenOffsetY + 36, 15, 15);
            Sprite(spFrames[4], 132, screenOffsetY + 36, 11, 16);
            Sprite(spFrames[1], 53, screenOffsetY + 59, 14, 16);
            Sprite(spFrames[2], 53, screenOffsetY + 73, 14, 12);
            if (combat.ArmorActive)
            {
                int fraction = ((combat.MaxHealth - combat.ArmorAmount) << 7) / combat.MaxHealth;
                int shade = 255 - 255 * fraction / 128;
                Quad(2, 58, screenOffsetY + 25, 61 - 61 * fraction / 128, 6, 0xFF0000,
                    (uint)(0xFF0000 | (shade << 8) | shade));
            }
            int damageFraction = ((combat.MaxHealth - combat.Health) << 7) / combat.MaxHealth;
            int damageWidth = 61 * damageFraction / 128;
            if (damageWidth != 0) Quad(3, 119 - damageWidth, screenOffsetY + 25, damageWidth, 6, 0, 0);
            int pickupAge = (int)(clock - lastHealthPickupClock);
            int flash = pickupAge >= 32 ? 0 : 255 - 8 * pickupAge;
            if (damageWidth <= 30)
                Quad(4, 88, screenOffsetY + 25, 30, 6, (uint)((flash << 16) | 0xFFFF),
                    (uint)((flash << 16) | 0xFF00 | flash));
            if (damageFraction > 76)
            {
                int wave = GameAngle.Heading((int)clock * (175 * (damageFraction - 76) / 52 + 25)).Sine;
                uint beat = (uint)((wave * wave) | 0xFF00) >> 8;
                Quad(4, 58, screenOffsetY + 25, 31, 6, beat, beat);
            }
            else Quad(4, 58, screenOffsetY + 25, 31, 6, (uint)((flash << 16) | (flash << 8) | 0xFF),
                (uint)((flash << 16) | 0xFFFF));
            int emptyWeb = 26 * (((4096 - inventory.Webbing) << 7) / 4096) / 128;
            if (emptyWeb != 0) Quad(3, 31, screenOffsetY - emptyWeb + 67, 11, emptyWeb, 0, 0);
            uint webColour = inventory.FireWebbing != 0 ? 0x0000FFu : 0xA04040u;
            Quad(4, 31, screenOffsetY + 41, 11, 26, webColour, webColour);
        }
        return draws.AsReadOnly();

        void Sprite(SpriteAnimationFrame frame, int x, int y, int width, int height) =>
            draws.Add(new PlayerHudSprite(1, frame, HudSpriteLayout.Stretch(frame, x, y, width, height), 128, 128, 128));
        void Quad(float depth, int x, int y, int width, int height, uint left, uint right) =>
            draws.Add(new PlayerHudQuad(depth, new(x, y, width, height), left, right));
    }
}
