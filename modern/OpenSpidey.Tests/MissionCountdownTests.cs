using System.Text.Json;
using OpenSpidey.Core.Gameplay;

internal static class MissionCountdownTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(MissionCountdownTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.mission-countdown.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            uint[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetUInt32()).ToArray();
            var state = new MissionCountdown { Running = (byte)input[0], Rate = input[1], RemainingTicks = input[2], LastUpdateTick = input[3] };
            state.Update(input[4], input[5] != 0 || input[6] != 0);
            JsonElement expected = test.GetProperty("expected");
            assert(state.RemainingTicks == expected[0].GetUInt32() && state.LastUpdateTick == expected[1].GetUInt32() &&
                state.Running == input[0] && state.Rate == input[1],
                $"Panel_UpdateTimer source case {index}: unsigned clock/rate wrapping, clamp-to-zero, disabled timer clock and both scripted-control pause flags.");
            index++;
        }
    }
}
