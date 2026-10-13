using System.Text.Json;
using OpenSpidey.Core.Player;

internal static class PlayerInputFinalizerTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerInputFinalizerTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-input-finalizer.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            byte[] raw = test.GetProperty("input").EnumerateArray().Select(value => value.GetByte()).ToArray();
            var input = new PlayerMeleeInput(); raw.AsSpan(0, 20).CopyTo(input.Triggered); raw.AsSpan(20, 20).CopyTo(input.Mirrors);
            Array.Fill(input.Pressed, (byte)255);
            PlayerInputFinalizer.ClearEdges(input);
            assert(input.Triggered.Concat(input.Mirrors).SequenceEqual(test.GetProperty("expected").EnumerateArray().Select(value => value.GetByte())),
                "Original PC finalizer clears every triggered/mirror slot except14/15, after the same-frame action/lookaround consumers.");
            assert(input.Pressed.All(value => value == 255), "Original triggered reset preserves held button state independently.");
        }
    }
}
