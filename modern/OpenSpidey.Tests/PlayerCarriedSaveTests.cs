using System.Text;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;

internal static class PlayerCarriedSaveTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerCarriedSaveTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.player-carried-save.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            PlayerCarriedSaveHistory history = new(ReadState(test.GetProperty("saved")), ReadState(test.GetProperty("previous")));
            var exit = (LevelExitReason)test.GetProperty("status").GetInt32();
            JsonElement expected = test.GetProperty("expected");
            int mode = test.GetProperty("mode").GetInt32();
            if (mode == 0)
            {
                PlayerCarriedSaveHistory actual = PlayerCarriedSavePolicy.OnPlayerDestroyed(history, exit, ReadState(test.GetProperty("live")));
                VerifyHistory(actual, expected, $"Destructor {index}", assert);
                assert(expected.GetProperty("constructor")[6].GetByte() == 0,
                    $"Destructor {index}: original armour global clears after the carried fields are written.");
            }
            else if (mode == 1)
            {
                bool same = test.GetProperty("same").GetInt32() != 0;
                bool hasPlayer = test.GetProperty("hasPlayer").GetInt32() != 0;
                PlayerCarriedSaveHistory direct = PlayerCarriedSavePolicy.OnMissionLoad(history, same, exit, hasPlayer);
                VerifyHistory(direct, expected, $"Retry policy {index}", assert);
                VerifyMissionCommand(history, exit, same, hasPlayer, expected, index, assert);
            }
            else
            {
                VerifyConstructor(history.Saved, test, expected, index, assert);
                VerifyHistory(history, expected, $"Constructor {index} retains both carried snapshots", assert);
            }
            assert(expected.GetProperty("guards").GetBoolean(),
                $"Original case {index}: all unrelated save and backup bytes remain untouched.");
            index++;
        }
        VerifySuccessivePlayerLifetimes(assert);
    }

    private static void VerifyConstructor(PlayerCarriedSaveState saved, JsonElement test, JsonElement expected,
        int index, Action<bool, string> assert)
    {
        byte textures = test.GetProperty("globalArmour").GetByte();
        byte processing = test.GetProperty("processing").GetByte();
        int suit = test.GetProperty("suit").GetInt32();
        PlayerCarriedConstructorState actual = PlayerCarriedSavePolicy.CreatePlayer(saved,
            test.GetProperty("difficulty").GetInt32(), suit, test.GetProperty("levelId").GetInt32(),
            test.GetProperty("suppressed").GetInt32() != 0, textures, processing, test.GetProperty("low").GetInt32() != 0);
        int[] state = [actual.Resources.Health, actual.Resources.Webbing, actual.Resources.Cartridges, actual.ArmourAmount,
            actual.ArmourOn, actual.ArmourAnimationVariant, actual.ArmourTexturesActive, actual.ArmourTextureProcessing];
        int[] reference = ReadInts(expected.GetProperty("constructor"));
        assert(state.SequenceEqual(reference),
            $"Constructor {index}: original resource clamps, independent suit-5 armour-on and raw texture bytes match; actual [{string.Join(',', state)}], expected [{string.Join(',', reference)}].");

        // Original executable leaf traces record state when costarm is loaded and when textures are swapped.
        // They also prove that resource restore precedes both calls and saved armour follows the texture call.
        var calls = new List<int>();
        if (actual.LoadArmourAnimation)
            calls.AddRange([1, actual.Resources.Webbing, actual.Resources.Cartridges, 0, textures, processing, 0, 0]);
        if (actual.SwapArmourTextures)
            calls.AddRange([2, suit, 0, actual.ArmourAmountBeforeSavedRestore, actual.Resources.Webbing,
                actual.Resources.Cartridges, textures, processing, 0, 0]);
        assert(calls.SequenceEqual(ReadInts(expected.GetProperty("events"))),
            $"Constructor {index}: genuine costarm/texture requirements preserve the original restore ordering and intermediate difficulty armour amount.");
    }

    private static void VerifyMissionCommand(PlayerCarriedSaveHistory history, LevelExitReason exit, bool same,
        bool hasPlayer, JsonElement expected, int index, Action<bool, string> assert)
    {
        var host = new TransitionHost(history, exit, hasPlayer);
        var node = new TriggerNode(0, 6, 0, ReadOnlyMemory<byte>.Empty);
        var command = new TriggerInstruction(181, 0, Encoding.Latin1.GetBytes((same ? "l1a1_t" : "L1A2_T") + '\0'));
        new LevelTransitionCommandHandler(MissionOpcode.LoadMission, host).Execute(new(node, 0), command);
        VerifyHistory(host.History, expected, $"Mission 181 composition {index}", assert);
        assert((int)host.ExitReason == expected.GetProperty("status").GetInt32() &&
            host.Killed == expected.GetProperty("killed").GetInt32() &&
            host.SavedAtKill == ReadState(expected.GetProperty("killSaved")) &&
            host.ExitAtKill == host.ExitReason && host.MissionAtKill == host.MissionName,
            $"Mission 181 {index}: real transition policy installs the restored save and new status/name before scene teardown.");
    }

    private static void VerifySuccessivePlayerLifetimes(Action<bool, string> assert)
    {
        var original = new PlayerCarriedSaveState(4096, 7, 91, 170, 187);
        var older = new PlayerCarriedSaveState(9, 8, 7, 6, 5);
        var live = new PlayerCarriedSaveState(2048, 3, 17, 128, 255);
        PlayerCarriedSaveHistory history = PlayerCarriedSavePolicy.OnPlayerDestroyed(new(original, older), LevelExitReason.LevelCompleted, live);
        assert(history.Previous == original && history.Saved == new PlayerCarriedSaveState(2048, 3, 17, 1, 1),
            "Completion backs up the previous save, then records the live player with normalized flag bytes.");
        var restored = PlayerCarriedSavePolicy.CreatePlayer(history.Saved, 2, 5, 0x101, false);
        assert(restored.Resources == new PlayerStartingResources(100, 2048, 3) && restored.ArmourAmount == 17 && restored.ArmourOn == 1,
            "The next constructor receives the live carried resources and suit-5 armour state.");
        history = PlayerCarriedSavePolicy.OnPlayerDestroyed(history, LevelExitReason.PlayerDied, new(1, 2, 3, 4, 5));
        assert(history.Previous == original && history.Saved == new PlayerCarriedSaveState(0, 0, 17, 1, 1),
            "Death clears only saved web resources and retains both saved armour and the earlier backup.");
        restored = PlayerCarriedSavePolicy.CreatePlayer(history.Saved, 3, 6, 0x901, false);
        assert(restored.Resources == new PlayerStartingResources(80, 4096, 2) && restored.ArmourAmount == 17 && restored.ArmourOn == 0,
            "Zeroed death inventory selects source defaults; suit/chapter clamps precede independent armour restore.");
        history = PlayerCarriedSavePolicy.OnMissionLoad(history, true, LevelExitReason.Running, false);
        assert(history.Saved == original && history.Previous == original && history.Saved.ArmourUnlocked == 170 && history.Saved.ArmourOn == 187,
            "A running no-player retry restores the previous save's raw bytes, never the dead player's live snapshot.");
        history = PlayerCarriedSavePolicy.OnPlayerDestroyed(history, LevelExitReason.Running, new(73, 4, 21, 0, 128));
        assert(history.Previous == original && history.Saved == new PlayerCarriedSaveState(73, 4, 21, 0, 1),
            "A subsequent running player teardown backs up the currently restored save before capturing new live fields.");
        assert(PlayerCarriedSavePolicy.OnPlayerDestroyed(history, (LevelExitReason)(-99), default) == history,
            "Unknown exit statuses preserve saved and previous state exactly.");
    }

    private static void VerifyHistory(PlayerCarriedSaveHistory actual, JsonElement expected, string context,
        Action<bool, string> assert)
    {
        var reference = new PlayerCarriedSaveHistory(ReadState(expected.GetProperty("saved")), ReadState(expected.GetProperty("previous")));
        assert(actual == reference,
            $"{context}: exact signed quantities and raw saved/backup bytes match original PC; actual {actual}, expected {reference}.");
    }

    private static int[] ReadInts(JsonElement array) => array.EnumerateArray().Select(value => value.GetInt32()).ToArray();
    private static PlayerCarriedSaveState ReadState(JsonElement array) =>
        new(array[0].GetInt32(), array[1].GetInt32(), array[2].GetInt32(), array[3].GetByte(), array[4].GetByte());

    private sealed class TransitionHost(PlayerCarriedSaveHistory history, LevelExitReason exit, bool hasPlayer) : ILevelTransitionHost
    {
        public PlayerCarriedSaveHistory History { get; private set; } = history;
        public string MissionName { get; private set; } = "L1A1_T";
        public LevelExitReason ExitReason { get; private set; } = exit;
        public bool HasPlayer => hasPlayer;
        public int Killed { get; private set; }
        public PlayerCarriedSaveState SavedAtKill { get; private set; }
        public LevelExitReason ExitAtKill { get; private set; }
        public string? MissionAtKill { get; private set; }
        public void PreserveCarriedPlayerState() => History = PlayerCarriedSavePolicy.RestorePrevious(History);
        public void SetExitReason(LevelExitReason reason) => ExitReason = reason;
        public void SetMissionName(string name) => MissionName = name;
        public void SetEndLevelNode(ushort node) => throw new InvalidOperationException("This carried-state proof only executes LoadMission.");
        public void KillAllSceneObjects()
        {
            Killed++; SavedAtKill = History.Saved; ExitAtKill = ExitReason; MissionAtKill = MissionName;
        }
    }
}
