using System.Text.Json;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Modules;
using OpenSpidey.Core.Player;

internal static class ScorpionApproachCountdownTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(ScorpionApproachCountdownTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.scorpion-approach.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            int family = row.GetProperty("family").GetInt32();
            JsonElement input = row.GetProperty("input"), expected = row.GetProperty("expected");
            if (family == 2)
            {
                long[] v = Read(input);
                var hook = new PlayerModuleHook(); hook.Set(v[0] != 0 ? "l2a1lsc" : null, (uint)v[1]);
                var events = new List<long>();
                hook.Run((name, slot) =>
                {
                    events.AddRange([name == "l2a1lsc" ? 0 : 1, slot]);
                    if (events.Count != 2) return;
                    if (v[2] == 1) hook.Set("thug", 0x84);
                    else if (v[2] == 2) hook.Set(null, 0);
                    else if (v[2] == 3) hook.Set("thug", 0xFF);
                });
                long nameId = hook.ModuleName is null ? -1 : hook.ModuleName == "l2a1lsc" ? 0 : 1;
                assert(nameId == expected[0].GetInt64() && hook.FunctionMask == expected[1].GetUInt32() && events.SequenceEqual(Read(expected[2])),
                    $"Whole post-AI user-hook case{index}: exact slot0..7 order, null-name/upper-mask behavior and per-callback reread of source name and mask.");
            }
            else
            {
                long[] v = Read(family == 0 ? input.GetProperty("state") : input);
                var countdown = new MissionCountdown { DisplayEnabled = (byte)v[0], Running = (byte)v[1], RemainingTicks = (uint)v[2],
                    Rate = (uint)v[3], LastUpdateTick = (uint)v[4] };
                var state = new ScorpionApproachState();
                for (int warning = 0; warning < 5; warning++)
                { state.PrimaryWarnings[warning] = (byte)v[5 + warning]; state.SecondaryWarnings[warning] = (byte)v[10 + warning]; }
                var host = new CountdownHost((int)v[15], (uint)v[16], family == 0 ? input : default);
                if (family == 0) ScorpionApproachCountdown.Monitor(countdown, state, host);
                else
                {
                    uint[] textures = [12345, 12346, 12347, 12348, 12349]; var released = new List<int>();
                    ScorpionApproachCountdown.Clear(countdown, host.Clock, texture =>
                    {
                        assert(countdown.DisplayEnabled == 0 && countdown.Running == 0 && countdown.LastUpdateTick == host.Clock,
                            "Source module clear resets both panel flags and source clock before the first cached texture release.");
                        released.Add(texture); textures[texture] = 0;
                    });
                    assert(released.SequenceEqual(new[] { 0, 1, 2, 3, 4 }) && textures.Select(value => (long)value).SequenceEqual(Read(expected[1])),
                        "Source module clear releases exactly five cached progress pointers in original order.");
                }
                long[] actual = Snapshot(countdown, state), wanted = Read(expected[0]);
                assert(actual.SequenceEqual(wanted), $"Whole l2a1lsc case{index}/family{family}: actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
                if (family == 0) assert(host.Events.SequenceEqual(Read(expected[1])),
                    $"Whole l2a1lsc monitor{index} preserves ascending kind1 authored search from node1, signed Q12 floor comparison, first-match pulse and missing-target diagnostic.");
            }
            index++;
        }
        VerifyParameterlessOwnership(assert);
    }

    private static void VerifyParameterlessOwnership(Action<bool, string> assert)
    {
        var registry = new SourceModuleRegistry<int, int>();
        var hook = new PlayerModuleHook(); var events = new List<string>();
        registry.Register("thug", module => { module.SetClear(() => events.Add("clear-thug")); module.SetUserFunction(0, value => value + 1); });
        registry.Register("l2a1lsc", module =>
        {
            assert(registry.IsLoaded("l2a1lsc") && registry.SourceOrder[0] == module,
                "The parameterless level-script initializer observes its genuine module at the same canonical source head as actor modules.");
            module.SetClear(() => { assert(registry.IsLoaded("l2a1lsc"), "Module clear observes its actual linked record before unlink."); events.Add("clear-level"); });
            module.SetParameterlessUserFunction(0, () => events.Add("monitor"));
            module.SetParameterlessUserFunction(1, () => events.Add("display"));
            hook.Set("l2a1lsc", 1);
        });
        registry.Load("thug"); registry.Load("l2a1lsc"); registry.Load("l2a1lsc");
        hook.Run(registry.CallParameterlessUserFunction);
        registry.CallParameterlessUserFunction("l2a1lsc", 1);
        assert(registry.SourceOrder.Select(module => module.Name).SequenceEqual(new[] { "l2a1lsc", "thug" }) &&
            registry.CallUserFunction("thug", 0, 17) == 18 && events.SequenceEqual(new[] { "monitor", "display" }),
            "Level monitor0 and HUD display1 share the real module list without replacing factory functions or inventing an actor result.");
        registry.UnloadAll();
        assert(events.SequenceEqual(new[] { "monitor", "display", "clear-thug", "clear-level" }) && registry.SourceOrder.Count == 0 && hook.ModuleName == "l2a1lsc",
            "Actual Reloc_UnloadAll order clears actor and level modules once while retaining the process player hook until Front_LoadLevel explicitly resets it.");
        hook.Set(null, 0); hook.Run(registry.CallParameterlessUserFunction);
    }

    private static long[] Read(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private static long[] Snapshot(MissionCountdown countdown, ScorpionApproachState state) => [countdown.DisplayEnabled, countdown.Running,
        countdown.RemainingTicks, countdown.Rate, countdown.LastUpdateTick, .. state.PrimaryWarnings.Select(value => (long)value),
        .. state.SecondaryWarnings.Select(value => (long)value)];
    private sealed class CountdownHost(int difficulty, uint clock, JsonElement input) : IScorpionApproachCountdownHost
    {
        public int Difficulty => difficulty;
        public uint Clock => clock;
        public int NodeCount => input.GetProperty("count").GetInt32();
        public List<long> Events { get; } = [];
        public ushort NodeKind(int node) => input.GetProperty("nodes")[node][0].GetUInt16();
        public FixedVector3 NodePosition(int node)
        {
            Events.AddRange([1, node]); JsonElement point = input.GetProperty("nodes")[node];
            return new(point[1].GetInt32(), point[2].GetInt32(), point[3].GetInt32());
        }
        public void SendPulse(int node) => Events.AddRange([2, node]);
        public void ReportMissingFailureNode() => Events.Add(3);
    }
}
