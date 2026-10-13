using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Presentation;

internal static class PlayerHudTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerHudTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-hud.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        SpriteAnimationFrame[] frames = Enumerable.Range(0, 5).Select(index => new SpriteAnimationFrame(
            (sbyte)(index == 0 ? -32 : -16), (sbyte)(index == 0 ? -32 : -16),
            (byte)(index == 0 ? 64 : 32), (byte)(index == 0 ? 64 : 32), (uint)index)).ToArray();
        var cartridge = new SpriteAnimationFrame(-16, -16, 32, 32, 5);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var combat = new PlayerCombatState((short)input[0], input[1]) { ArmorActive = input[5] != 0, ArmorAmount = input[6] };
            var inventory = new PlayerWebInventory { Webbing = input[2], Cartridges = input[3], FireWebbing = (byte)input[4], LastCartridgeClock = unchecked((uint)input[9]) };
            IReadOnlyList<PlayerHudPrimitive> draws = PlayerHudFrame.Build(combat, inventory, unchecked((uint)input[7]),
                unchecked((uint)input[8]), frames, cartridge, input[10]);
            JsonElement expected = test.GetProperty("expected");
            assert(draws.Count == expected.GetArrayLength(), $"Player HUD{index} retains source draw count.");
            for (int draw = 0; draw < draws.Count; draw++)
            {
                long[] actual = draws[draw] switch
                {
                    PlayerHudSprite sprite => [0, (int)Math.Round(sprite.Depth * 10), sprite.Frame.TextureChecksum,
                        sprite.Quad.Left, sprite.Quad.Top, sprite.Quad.Right, sprite.Quad.Top, sprite.Quad.Left, sprite.Quad.Bottom,
                        sprite.Quad.Right, sprite.Quad.Bottom, sprite.Red, sprite.Green, sprite.Blue],
                    PlayerHudQuad quad => [2, (int)Math.Round(quad.Depth * 10), (int)quad.Rectangle.X, (int)quad.Rectangle.Y,
                        (int)quad.Rectangle.Width, (int)quad.Rectangle.Height, quad.LeftColour, quad.RightColour, quad.LeftColour, quad.RightColour],
                    PlayerHudText text => [1, (int)Math.Round(text.Depth * 10), text.X, text.Y, text.Text.Span[0], text.Text.Span[1]],
                    _ => throw new InvalidDataException("Player HUD produced an unknown source primitive.")
                };
                assert(actual.SequenceEqual(expected[draw].EnumerateArray().Select(value => value.GetInt64())),
                    $"Player HUD{index}:{draw} source primitive mismatch: actual[{string.Join(',', actual)}], expected{expected[draw]}.");
            }
            index++;
        }
    }
}
