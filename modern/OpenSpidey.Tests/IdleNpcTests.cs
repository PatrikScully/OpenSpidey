using System.Text.Json;
using OpenSpidey.Core.Actors.Npcs;

internal static class IdleNpcTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(IdleNpcTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.idle-npc.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0, constructors = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            JsonElement[] input = test.GetProperty("input").EnumerateArray().ToArray();
            int[] values = input.Skip(1).Select(value => value.GetInt32()).ToArray();
            int[] expected = test.GetProperty("expected").EnumerateArray().Take(16).Select(value => value.GetInt32()).ToArray();
            int[] expectedEvents = test.GetProperty("expected")[16].EnumerateArray().Select(value => value.GetInt32()).ToArray();
            bool construct = input[0].GetString() == "C";
            var kind = (IdleNpcKind)values[0];
            int node = construct ? values[13] : values[5];
            var state = new IdleNpcState(kind, (ushort)node, 0x103)
            {
                BodyFlags = (uint)values[2], CombatFlags = unchecked((uint)values[3]), DeathPulseSent = values[4] != 0
            };
            var host = new Host(values[6] != 0) { ItemFlags = (ushort)values[1], NodeForPulse = node };
            var brain = new IdleNpcBrain(state, host);
            int[] actual;
            if (construct)
            {
                host.Events.Add(1);
                host.Events.AddRange([2, (int)KindFromModel(state.ModelName)]);
                brain.Construct();
                host.ItemFlags |= 0x480;
                // AttachTo(BaddyList) precedes the type, node and abort test in each constructor.
                int die = host.Events.IndexOf(7);
                host.Events.Insert(die < 0 ? host.Events.Count : die, 4);
                host.Events.Insert(die < 0 ? host.Events.Count : die + 1, 0x56E990);
                actual = [host.ItemFlags, (ushort)state.BodyFlags, state.DeathPulseSent ? 1 : 0, host.InputFlags, host.AnimationId,
                    state.BodyType, node, state.MinorRadius, (int)state.LightAddress,
                    values[7] << 12, values[8] << 12, values[9] << 12, (short)values[10], (short)values[11], (short)values[12], 0];
                assert(state.TriggerType == IdleNpcState.TriggerTypeOf(kind) && IdleNpcState.KindOf(state.TriggerType) == kind,
                    "Idle NPC trigger types round-trip through the original Trig_CreateObject dispatch.");
                constructors++;
            }
            else
            {
                host.AnimationId = (ushort)values[7];
                host.InputFlags = (ushort)values[8];
                host.HasMessages = values[9] != 0;
                brain.Advance();
                actual = [host.ItemFlags, (ushort)state.BodyFlags, state.DeathPulseSent ? 1 : 0, host.InputFlags, host.AnimationId,
                    0, node, 0, 0, 0, 0, 0, 0, 0, 0, host.HasMessages ? 1 : 0];
            }
            assert(actual.SequenceEqual(expected) && host.Events.SequenceEqual(expectedEvents),
                $"Original PC idle NPC case {index} ({input[0].GetString()} {kind}): actual=[{string.Join(',', actual)}] " +
                $"expected=[{string.Join(',', expected)}]; events=[{string.Join(',', host.Events)}] expected=[{string.Join(',', expectedEvents)}].");
            assert(state.ActorPhase == 3 && state.SourceConstructionOrdinal == 0x103, "Idle NPCs retain the full CBaddy construction ordinal.");
            index++;
        }
        assert(constructors > 0 && index > constructors, "Idle NPC fixture covers constructors and AI frames.");
        assert(IdleNpcState.KindOf(719) == IdleNpcKind.Submariner && new IdleNpcState(IdleNpcKind.Submariner, 1, 0).BodyType == 326 &&
            new IdleNpcState(IdleNpcKind.Submariner, 1, 0).ModuleName == "submarin" && new IdleNpcState(IdleNpcKind.Lizard, 1, 0).ModelName == "lizard",
            "CSubmariner keeps the original 326 body type, mariner model and submarin module name.");
        bool rejected = false;
        try { IdleNpcState.KindOf(306); } catch (NotSupportedException) { rejected = true; }
        assert(rejected, "Non-idle source types are rejected by the idle NPC family.");
    }

    private static IdleNpcKind KindFromModel(string model) => model switch
    {
        "mj" => IdleNpcKind.MaryJane,
        "lizard" => IdleNpcKind.Lizard,
        "mariner" => IdleNpcKind.Submariner,
        _ => throw new InvalidDataException(model)
    };

    private sealed class Host(bool fastForward) : IIdleNpcHost
    {
        private static readonly ushort[] links = [9, 4];
        public List<int> Events { get; } = [];
        public ushort InputFlags { get; set; }
        public ushort ItemFlags { get; set; }
        public int AnimationId { get; set; }
        public bool HasMessages { get; set; }
        public bool FastForwardRequested => fastForward;
        public void CleanUpMessages(bool all, bool keep) { HasMessages = false; Events.AddRange([5, all ? 1 : 0, keep ? 1 : 0]); }
        public void CycleAnimation(int animation, int direction) { AnimationId = (ushort)animation; Events.AddRange([3, animation, direction]); }
        public void BuildTransform() => Events.Add(6);
        public void SendDeathPulse() { Events.AddRange([7, NodeForPulse]); foreach (ushort link in links) Events.AddRange([8, link]); }
        public void MarkDead() { }
        public int NodeForPulse { get; set; }
    }
}
