using System.Text.Json;
using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Player;

internal static class PlayerStandTests
{
    public static void Run(Action<bool, string> assert)
    {
        using JsonDocument document = JsonDocument.Parse(typeof(PlayerStandTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-stand.json")!);
        assert(document.RootElement.GetProperty("originalPcStandVerified").GetBoolean(), "Stand choices require original PC proof.");
        int index = 0;
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            int[] expected = test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            AnimationPlaybackState playback = AnimationPlaybackState.Initial with { ClipId = (ushort)input[0], Frame = (short)input[1], Direction = (sbyte)input[2] };
            PlayerAnimationPlan plan = PlayerLocomotionAnimations.Stand(playback, input[3] != 0, input[4] == -1 ? null : input[4]);
            assert(plan == new PlayerAnimationPlan(expected[1], expected[2], expected[3], expected[0] != 0),
                $"Stand PC case {index}: input [{string.Join(',', input)}], expected [{string.Join(',', expected)}], actual {plan}.");
            assert(expected[6] == (int)PlayerAction.Idle && expected[5] == (plan.Restart ? input[5] : 0) &&
                expected[4] == (plan.Restart ? input[5] == 0 ? -1 : plan.Clip : 299),
                "Stand source boundaries preserve idle state, selected sound entry and reset only when starting an animation.");
            index++;
        }
    }
}
