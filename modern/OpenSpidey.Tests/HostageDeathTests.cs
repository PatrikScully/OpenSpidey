using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Hostages;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;

internal static class HostageDeathTests
{
    private sealed class Host : IHostageDeathHost
    {
        public List<int> Events { get; } = [];
        public int Marks;
        public void SendDeathPulse() => Events.Add(1);
        public void KillShadow() => Events.Add(2);
        public void MarkDead() => Marks++;
    }
    private sealed class InitializationHost : IHostageInitializationHost
    {
        public int ScriptOffset;
        public void AttachToSourceBaddyList() { }
        public void EnableShadow() { }
        public void ParseScript(ActorScriptCursor script) => ScriptOffset = script.AbsolutePosition;
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(HostageDeathTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.hostage-death.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new HostageState(0) { BodyFlags = (uint)input[0], ItemFlags = (ushort)input[2], Transparency = (uint)input[3], TintColor = (uint)input[4],
                DeathFadeUpdates = (int)input[5], DeathUpdates = 17 };
            var actor = new ActorProcedureState(new()) { BehaviorFlags = (uint)input[1] }; var host = new Host();
            int frame = 0;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                host.Events.Clear(); bool previouslyDead = (state.BodyFlags & 0x40) != 0; int previousMarks = host.Marks;
                bool result = HostageDeathLifecycle.Step(state, actor, host, (int)input[6 + frame]);
                long[] actual = [result ? 1 : 0, state.BodyFlags, state.ItemFlags, state.Transparency, state.TintColor, state.DeathFadeUpdates];
                assert(actual.SequenceEqual(expected.EnumerateArray().Take(6).Select(value => value.GetInt64())) && host.Events.SequenceEqual(expected[6].EnumerateArray().Select(value => value.GetInt32())),
                    $"Hostage common-death{index}:{frame} matches original PC staged flags/alpha/tint, old-counter expiry, wrapping fade and explicit pulse/shadow ownership.");
                assert(state.DeathUpdates == 17 && actor.BehaviorFlags == (uint)input[1] && host.Marks - previousMarks == (!previouslyDead && (state.BodyFlags & 0x40) != 0 ? 1 : 0),
                    "Common field1F8 fade preserves separate CHostage field328 flash counter and notifies native retirement only on actual death transition.");
                frame++;
            }
            index++;
        }
        var initialization = new InitializationHost(); var hostage = new HostageState(0);
        var definition = new TriggerActorDefinition(0, 1, 305, [], [], new(0, 0, 0), new(0, 0, 0), 4, new byte[2]);
        HostageInitialization.Construct(hostage, new(new ActorBodyMotion()), definition, initialization, sourceNodeOffset: 1024);
        assert(initialization.ScriptOffset == 1046 && (hostage.BodyFlags & 8) != 0,
            "Hostage script cursor retains the true file-absolute node offset and source body shadow flag for aligned operands.");
    }
}
