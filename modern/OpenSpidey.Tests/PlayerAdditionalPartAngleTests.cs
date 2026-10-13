using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Player;

internal static class PlayerAdditionalPartAngleTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerAdditionalPartAngleTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.player-additional-part-angles.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            short[] input = row.GetProperty("input").EnumerateArray().Select(value => value.GetInt16()).ToArray();
            FixedJointTransform Matrix(int offset) => new(input[offset], input[offset + 1], input[offset + 2],
                input[offset + 3], input[offset + 4], input[offset + 5], input[offset + 6], input[offset + 7], input[offset + 8], 0, 0, 0);
            ActorAngles actual = PlayerAdditionalBodyRules.PartAngles(Matrix(0), Matrix(9));
            assert(new[] { actual.X, actual.Y, actual.Z }.SequenceEqual(row.GetProperty("expected").EnumerateArray().Select(value => value.GetInt16())),
                $"Whole original M3dUtils part angles{index++}: fixed actor/posed-part world product, original square-root, signed Euler components and degenerate fallback.");
        }
    }
}
