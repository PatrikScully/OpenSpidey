using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;

internal static class InputSuppressionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(InputSuppressionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.input-suppression.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int exits = 0;
            bool look = test.GetProperty("look").GetBoolean();
            int speed = test.GetProperty("speed").GetInt32();
            var suppression = new PlayerInputSuppression(() => { exits++; look = false; });
            suppression.Set(test.GetProperty("duration").GetInt32(), speed, look);
            int frame = 0;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                bool scripted = test.GetProperty("scripted").GetBoolean() && frame++ < 3;
                PlayerInputSuppressionStep step = suppression.Advance(test.GetProperty("ticks").GetInt32(), scripted);
                if (step.RestoredAnimationSpeed is { } restored) speed = restored;
                int[] actual = new int[65];
                actual[0] = suppression.RemainingTicks; actual[1] = suppression.SavedAnimationSpeed;
                actual[2] = speed; actual[3] = exits; actual[4] = look ? 1 : 0;
                for (int slot = 0; slot < 20; slot++)
                {
                    int pressed = step.ClearButtons && (slot < 12 || slot >= 16) ? 0 : 1;
                    actual[5 + slot * 3] = pressed; actual[6 + slot * 3] = pressed; actual[7 + slot * 3] = 7;
                }
                assert(actual.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt32())),
                    "Input-lock timer, short saved speed, lookaround exit and source pad clearing match recovered updates.");
                assert(step.BlockMovement == (suppression.RemainingTicks != 0 && !scripted),
                    "Physical movement uses the timer after its source prologue countdown while scripted axes bypass the lock.");
            }
        }
        ushort received = 0;
        byte[] command = [4, 0, 163, 0, 255, 255, 255, 255];
        var mission = new MissionRuntime(new([new(0, 4, 0, command)]), new MissionRuntimeTests.ActorFactory(),
            [new PlayerInputCommandHandler(duration => received = duration)]);
        mission.QueueAutoexec();
        assert(mission.ExecutePending() == 1 && received == ushort.MaxValue,
            "The mission input-lock command consumes the full unsigned duration without swallowing following commands.");
    }
}
