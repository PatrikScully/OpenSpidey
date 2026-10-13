using System.Text.Json;
using OpenSpidey.Core.Actors.Thugs;

internal static class ThugTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-behavior.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            if (input[0] == 0) CheckGuard(input, test.GetProperty("expected"), assert);
            else CheckConfiguration(input, test.GetProperty("expected"), assert);
        }

        var flags = new ThugStateFlags();
        ushort[] expectedFlags = [19,96,96,0,0,0,0,0,0,0,0,0,1,5,7,7,0,7,1,3,3,0,27,64,64,96,11,11];
        for (int state = 0; state < expectedFlags.Length; state++)
        {
            assert(flags.Check(state) == expectedFlags[state], $"Thug state {state} uses original 0x557CA0 flags.");
            assert(flags.Check(state) == expectedFlags[state], "Cached thug state flag lookup preserves result.");
        }
        var defaults = new ThugState(10, 513);
        defaults.SetThugType(304);
        assert(defaults.MinorRadius == 150 && defaults.Field380 == 400,
            "Setting thug type from script does not retroactively recompute constructor collision and distance defaults.");
        var pretyped = new ThugState(10, 513, initialType: 304);
        assert(pretyped.MinorRadius == 96 && pretyped.Field380 == 300, "Constructor reads existing type for its two conditional defaults.");
    }

    private static void CheckGuard(long[] input, JsonElement expected, Action<bool, string> assert)
    {
        var state = new ThugState(0, 0);
        state.SetThugType((int)input[1]);
        state.Substate = (int)input[2];
        state.ActionCounter = (int)input[3];
        state.PauseUpdates = (int)input[4];
        state.BodyFlags = (uint)input[5];
        var host = new Host
        {
            AnimationId = (int)input[6], AnimationFrame = (int)input[7], RunFinished = (byte)input[8]
        };
        int frame = 0;
        foreach (JsonElement step in expected.EnumerateArray())
        {
            int offset = 10 + frame * 3;
            host.AnimationFinished = (byte)input[offset];
            host.SpitResult = input[offset + 2] != 0;
            ThugGuardBehavior.Step(state, host, bound =>
            {
                host.Events.Add(1000 + bound);
                return (int)input[offset + 1] % bound;
            });
            long[] actual = [state.Substate, state.ActionCounter, state.PauseUpdates, state.BodyFlags,
                host.AnimationId, host.AnimationFrame, host.AnimationFinished];
            long[] wanted = step.EnumerateArray().Take(7).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), "Guard counters, flags, animation and fallthrough match compiled CThug::Guard.");
            assert(host.Events.SequenceEqual(step[7].EnumerateArray().Select(value => value.GetInt32())),
                "Guard animation, random bounds, neutralization and spit effects occur in source order.");
            host.Events.Clear();
            frame++;
        }
    }

    private static void CheckConfiguration(long[] input, JsonElement expected, Action<bool, string> assert)
    {
        var state = new ThugState((int)input[2], (int)input[3], 123);
        int hooks = -1;
        state.SetThugType((int)input[4], type => hooks = type == 304 ? 0 : 1);
        for (int i = 0; i < input[5]; i++)
            state.SetParamByIndex((int)input[6 + i * 2], (int)input[7 + i * 2]);
        int model = state.ModelName switch { "thug" => 1, "henchngt" => 2, _ => 3 };
        long[] actual = [state.Type, model, hooks, state.SurfaceClearance, state.AnimationWord294, state.AnimationWord298,
            state.Field374, state.Field370, state.Field378, state.Field37C, state.Field380, state.Field384,
            state.Field388, state.Field38C, state.Field1FC, state.Field394, state.CombatFlags, state.MinorRadius,
            state.PushValue, state.LevelId, state.AttackRelatedSnapshot, state.SourceNode, state.SourceNode,
            state.Field216, state.BehaviorFlags, state.State, state.Field212, state.ShadowScale];
        assert(actual.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt64())),
            "Thug constructor, type models/hooks/animation tables and parameter flags match compiled recovered C++.");
        assert(state.ShadowEnabled, "Thug constructor enables its shadow.");
        assert(state.BodyFlags == 0x216, "Thug retains CBody 0x16 and CBaddy 0x200 constructor body flags.");
    }

    private sealed class Host : IThugGuardHost
    {
        public int AnimationId { get; set; }
        public int AnimationFrame { get; set; }
        public byte AnimationFinished { get; set; }
        public byte RunFinished { get; init; }
        public bool SpitResult { get; set; }
        public List<int> Events { get; } = [];
        public void Neutralize() => Events.Add(1);
        public void RunAnimation(ushort clip, int from = 0, int to = -1)
        {
            Events.AddRange([2, clip, from, to]);
            AnimationId = clip;
            AnimationFrame = from;
            AnimationFinished = RunFinished;
        }
        public void CycleAnimation(ushort clip, sbyte direction = 1)
        {
            Events.AddRange([3, clip, direction]);
            AnimationId = clip;
            AnimationFinished = 0;
        }
        public bool MonitorSpitPlease() { Events.Add(4); return SpitResult; }
    }
}
