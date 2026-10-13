using System.Text.Json;
using OpenSpidey.Core.Player;

internal static class PlayerTorsoAimFinalizerTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerTorsoAimFinalizerTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-torso-aim.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            long[] input = row.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100)
            { ActionState = (uint)input[0], Field8E8 = (byte)input[5], SynthesizedInputActive = input[6] != 0, FieldAE6 = (byte)input[7] };
            var melee = new PlayerMeleeState(combat);
            melee.Input.Sideways = (sbyte)input[1]; melee.Input.Forward = (sbyte)input[2];
            var host = new Host();
            short camera = (short)input[4];
            short result = PlayerTorsoAimFinalizer.Update(melee, (short)input[3], camera, host);
            assert(result == camera && host.Events.SequenceEqual(row.GetProperty("expected").EnumerateArray().Select(value => value.GetInt64())),
                $"Original whole torso aim{index++}: source action16 priority, wall/script fast flag, short-wrapped desired angle and separateAE6 airborne/idle lock.");
        }
    }

    private sealed class Host : IPlayerTorsoAimFinalizerHost
    {
        public List<long> Events { get; } = [];
        public void SetTargetTorsoAngle(short wanted, bool fast) => Events.AddRange([1,wanted,fast ? 1 : 0]);
        public void LockTargetTorsoAngle() => Events.Add(2);
    }
}
