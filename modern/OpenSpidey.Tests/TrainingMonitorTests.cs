using System.Text.Json;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;

internal static class TrainingMonitorTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(TrainingMonitorTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.training-monitor.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        int index = 0, calls = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            JsonElement input = row.GetProperty("input");
            var state = new TrainingState();
            Array.Fill(state.Stuff, 0x5A5A5A5A); Array.Fill(state.Flags, (byte)7);
            var results = new TrainingResults { Seconds = input.GetProperty("seconds").GetInt32(), Score = input.GetProperty("score").GetInt32() };
            var countdown = new MissionCountdown { Rate = input.GetProperty("rate").GetUInt32() };
            var host = new Host(input.GetProperty("nodeCount").GetInt32(),
                input.GetProperty("nodes").EnumerateArray().Select(node => node.EnumerateArray().Select(value => value.GetInt32()).ToArray()).ToArray());
            int level = input.GetProperty("level").GetInt32();
            JsonElement[] expected = row.GetProperty("expected").EnumerateArray().ToArray();
            int frame = 0;
            foreach (JsonElement frameInput in input.GetProperty("frames").EnumerateArray())
            {
                int[] f = frameInput.EnumerateArray().Select(value => value.GetInt32()).ToArray();
                host.PlayerPresent = f[1] != 0; host.PlayerHealth = (short)f[2]; host.PlayerFieldE18 = f[3];
                host.PlayerField568 = f[4]; host.PlayerField5D0 = f[5]; host.Clock = unchecked((uint)f[6]); host.KillCount = f[7];
                if (f[8] >= 0) countdown.RemainingTicks = (uint)f[8];
                if (f[9] >= 0) state.TimeAttackComplete = (byte)f[9];
                host.CostumeUnlockFlags = unchecked((uint)f[10]); host.ScreenOffsetY = f[11];
                if (f[12] >= 0) countdown.DisplayEnabled = (byte)f[12];
                if (f[13] >= 0) countdown.Running = (byte)f[13];
                host.Events.Clear();
                switch (f[0])
                {
                    case 0:
                    {
                        var hook = new OpenSpidey.Core.Player.PlayerModuleHook();
                        TrainingMonitor.Initialize(state, countdown, hook, level);
                        if (hook.ModuleName == TrainingMonitor.ModuleName) host.Events.AddRange([6, hook.FunctionMask]);
                        break;
                    }
                    case 1: TrainingMonitor.Monitor(state, results, countdown, host); break;
                    case 2: TrainingMonitor.SpideyDied(state); break;
                    default: TrainingMonitor.Clear(countdown, host.Clock); break;
                }
                long[] actual = [.. state.Stuff, .. state.Flags, state.TimeAttackComplete, countdown.DisplayEnabled, countdown.Running,
                    countdown.RemainingTicks, countdown.Rate, countdown.LastUpdateTick, results.Score, host.CostumeUnlockFlags];
                long[] wantedState = expected[frame].GetProperty("state").EnumerateArray().Select(value => value.GetInt64()).ToArray();
                long[] wantedEvents = expected[frame].GetProperty("events").EnumerateArray().Select(value => value.GetInt64()).ToArray();
                assert(actual.SequenceEqual(wantedState) && host.Events.SequenceEqual(wantedEvents),
                    $"Original training case{index} level{level:X} call{frame} op{f[0]}: state [{string.Join(',', actual)}] expected [{string.Join(',', wantedState)}], " +
                    $"events [{string.Join(',', host.Events)}] expected [{string.Join(',', wantedEvents)}].");
                frame++; calls++;
            }
            index++;
        }
        assert(index == 1800 && calls > 20000, "The original training fixture covers every exercise family and module entry point.");
    }

    private sealed class Host(int nodeCount, int[][] nodes) : ITrainingMonitorHost
    {
        public List<long> Events { get; } = [];
        public bool PlayerPresent { get; set; }
        public short PlayerHealth { get; set; }
        public int PlayerFieldE18 { get; set; }
        public int PlayerField568 { get; set; }
        public int PlayerField5D0 { get; set; }
        public uint Clock { get; set; }
        public int KillCount { get; set; }
        public uint CostumeUnlockFlags { get; set; }
        public int ScreenOffsetY { get; set; }
        public int NodeCount => nodeCount;
        public ushort NodeKind(int node) => (ushort)nodes[node][0];
        public FixedVector3 NodePosition(int node) => new(nodes[node][1], nodes[node][2], nodes[node][3]);
        public void SendPulse(int node) => Events.AddRange([5, node]);
        public void SetTextScale(int scale) => Events.AddRange([1, scale]);
        public void SetTextJustification(byte justification) => Events.AddRange([2, justification]);
        public void DrawText(int x, int y, ReadOnlyMemory<byte> text, int argument, uint depth)
        {
            Events.AddRange([3, x, y, argument, depth, text.Length]);
            foreach (byte value in text.Span) Events.Add(value);
        }
        public void EndTraining() => Events.Add(4);
    }
}
