using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerTorsoTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerTorsoTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-torso.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var turn = new PlayerHeadingTurn((short)input[1], (ushort)input[7], input[6], input[8]);
            var camera = new FixedJointTransform((short)input[17], (short)input[18], (short)input[19],
                (short)input[20], (short)input[21], (short)input[22], (short)input[23], (short)input[24], (short)input[25], 31, 37, 41);
            turn = input[0] switch
            {
                0 => PlayerTorsoTargeting.TargetPoint(turn, Vector(11), Vector(14), camera, input[2] != 0, (uint)input[4], input[5], input[3] != 0),
                1 => PlayerHeadingTurning.Target(turn, (short)input[9], (uint)input[4], input[5], input[3] != 0, input[10] != 0),
                _ => PlayerTorsoTargeting.Lock(turn),
            };
            int[] actual = [turn.RemainingTicks, turn.Target, turn.Step];
            assert(actual.SequenceEqual(test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt32())) && turn.Heading == input[1],
                $"PlayerTorso{index} matches original PC point rotation, own/ceiling heading composition, shortest-turn asymmetry, crawl/fast/airborne/grab rates and lock retention; effective surface heading is the explicit caller-owned boundary.");
            index++;
            FixedVector3 Vector(int offset) => new(input[offset], input[offset + 1], input[offset + 2]);
        }
    }
}
