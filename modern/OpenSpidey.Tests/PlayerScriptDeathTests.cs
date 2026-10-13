using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;

internal static class PlayerScriptDeathTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(PlayerScriptDeathTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-script-death.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            long[] input = Read(row.GetProperty("input"));
            var host = new DeathHost((int)input[0], (int)input[1]);
            string before = JsonSerializer.Serialize(host.Player);
            PlayerScriptDeathRequest.Request(host);
            long[] actual = [host.Status, host.LevelQueries, host.TrainingCalls, JsonSerializer.Serialize(host.Player) == before ? 1 : 0], expected = Read(row.GetProperty("expected"));
            assert(actual.SequenceEqual(expected), $"Whole original scripted death case{index++}: actual[{string.Join(',', actual)}], expected[{string.Join(',', expected)}], input[{string.Join(',', input)}].");
        }
        // A real kind6 point proves absent-player152 still consumes exactly zero operands and continues its list.
        var bytes = new byte[16]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 6);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 152);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 213);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 321);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), ushort.MaxValue);
        var node = new TriggerNode(0, 6, 0, bytes); var actor = new EmptyActors(); var music = new MusicHost();
        var absent = new MissionRuntime(new TriggerFile([node]), actor,
            [new PlayerScriptDeathCommandHandler(() => throw new Exception("Absent152 resolved a player."), () => false), new FightMusicTimeCommandHandler(music)]);
        absent.Queue(0); absent.ExecutePending();
        assert(music.FightMusicTime == 321 && absent.PendingCount == 0, "Source152 missing-player branch preserves continuation into the next actual command's operands.");
        var actualHost = new DeathHost(0x104, 0);
        new PlayerScriptDeathCommandHandler(() => actualHost).Execute(new(node, 0), TriggerCommandReader.Read(node)[0]);
        assert(actualHost.Status == 2 && actualHost.TrainingCalls == 0, "Typed152 reaches the real status-only first-level branch.");
        try { PlayerScriptDeathRequest.Request(new MissingTraining()); throw new Exception("Required training dependency was swallowed."); }
        catch (NotSupportedException) { assert(true, "Training152 requires a genuine separate module boundary; it cannot fall through to ordinary death."); }
    }
    private static long[] Read(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private sealed class DeathHost(int levelId, int status) : IPlayerScriptDeathHost
    {
        public PlayerCombatState Player { get; } = new(73, 100)
        {
            ActionState = 0x400, AnimationId = 280, Field54C = 3, IsCrawling = true,
            WebPresent = true, DeathEffectPresent = true, HeldObjectPresent = true,
            Velocity = new(12345, -23456, 34567), Field8E8 = 1, Field8E9 = 2
        };
        public int LevelQueries { get; private set; }
        public int TrainingCalls { get; private set; }
        public int Status { get; private set; } = status;
        public int LevelId { get { LevelQueries++; return levelId; } }
        public void SetLevelStatus(int value) => Status = value;
        public void CallTrainingDeathModule() => TrainingCalls++;
    }
    private sealed class MissingTraining : IPlayerScriptDeathHost
    {
        public int LevelId => 0x901;
        public void SetLevelStatus(int value) => throw new Exception("Training selected ordinary level death.");
        public void CallTrainingDeathModule() => throw new NotSupportedException("The required training module is absent.");
    }
    private sealed class EmptyActors : IMissionActorFactory { public void Spawn(TriggerNode node) => throw new Exception("Unexpected constructor."); }
    private sealed class MusicHost : IMissionFightMusicHost { public int FightMusicTime { get; set; } }
}
