using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Core.Actors.Scripting;

internal static class ActorDirectEventCommandsTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorDirectEventCommandsTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-direct-events.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            ushort operand = row.GetProperty("operand").GetUInt16(), resolved = row.GetProperty("resolved").GetUInt16();
            ushort[] ids = row.GetProperty("ids").EnumerateArray().Select(value => value.GetUInt16()).ToArray();
            ActorScriptState[] bodies = row.GetProperty("signals").EnumerateArray().Select((value, body) =>
                new ActorScriptState { NodeId = ids[body], PendingSignals = value.GetUInt16() }).ToArray();
            var host = new Host(row.GetProperty("kind").GetUInt16(), bodies);
            byte[] words = new byte[4]; BinaryPrimitives.WriteUInt16LittleEndian(words, operand);
            BinaryPrimitives.WriteUInt16LittleEndian(words.AsSpan(2), 77);
            var cursor = new ActorScriptCursor(words);
            int reads = 0;
            bool result = ActorDirectEventCommands.TryExecute(row.GetProperty("opcode").GetUInt16(), cursor, (variable, source) =>
            {
                reads++;
                if (variable == 0x212B) assert(source.ReadWord() == 77, "Direct first-link variables consume their actual nested node selector.");
                return unchecked((short)resolved);
            }, host);
            JsonElement expected = row.GetProperty("expected");
            assert(result == (expected.GetProperty("result").GetInt32() != 0) && cursor.Position == expected.GetProperty("consumed").GetInt32() &&
                reads == expected.GetProperty("reads").GetInt32() && host.RequestedNode == resolved &&
                bodies.Select(body => body.PendingSignals).SequenceEqual(expected.GetProperty("signals").EnumerateArray().Select(value => value.GetUInt16())) &&
                host.Spawns.SequenceEqual(expected.GetProperty("spawns").EnumerateArray().Select(value => value.GetUInt16())) &&
                host.Pending.SequenceEqual(expected.GetProperty("pending").EnumerateArray().Select(value => value.GetUInt16())) &&
                host.Pulses == expected.GetProperty("pulses").GetByte(),
                $"Original direct event case {index++}: actual node {host.RequestedNode}, cursor {cursor.Position}, reads {reads}, " +
                $"signals [{string.Join(',', bodies.Select(body => body.PendingSignals))}], spawns [{string.Join(',', host.Spawns)}], " +
                $"pending [{string.Join(',', host.Pending)}], pulses {host.Pulses}; expected {expected}.");
        }
        var traversal = new List<string>();
        var control = new ActorScriptState { NodeId = 42, PendingSignals = 2 };
        var enemy = new ActorScriptState { NodeId = 42, PendingSignals = 4 };
        var environment = new ActorScriptState { NodeId = 42, PendingSignals = 8 };
        ActorDirectEventCommands.SignalActors(42, Read("control", control), Read("baddy", enemy), Read("environment", environment));
        assert(traversal.SequenceEqual(new[] { "control", "baddy", "environment" }) &&
            control.PendingSignals == 3 && enemy.PendingSignals == 5 && environment.PendingSignals == 9,
            "Direct42B3 visits the original Control/Baddy/Environment heads in call order and ORs each matching live flag without trigger-kind filtering.");
        var untouched = new ActorScriptCursor(new byte[] { 0x2A, 0x21 });
        assert(!ActorDirectEventCommands.TryExecute(0x42B1, untouched, (_, _) => throw new Exception("Unrelated opcode resolved a variable."),
            new Host(6, [])) && untouched.Position == 0,
            "Linked and unrelated instructions retain their separate dispatcher and consume no direct-event operand.");

        IEnumerable<ActorScriptState> Read(string list, ActorScriptState body)
        { traversal.Add(list); yield return body; }
    }

    private sealed class Host(ushort kind, ActorScriptState[] bodies) : IActorDirectEventHost
    {
        public ushort RequestedNode { get; private set; }
        public List<ushort> Spawns { get; } = [];
        public List<ushort> Pending { get; } = [];
        public byte Pulses { get; private set; } = 255;
        public void SignalActors(ushort nodeId)
        {
            RequestedNode = nodeId;
            ActorDirectEventCommands.SignalActors(nodeId, bodies.Take(3), bodies.Skip(3).Take(3), bodies.Skip(6).Take(3));
        }
        public void PulseNode(ushort nodeId)
        {
            RequestedNode = nodeId;
            switch (kind)
            {
                case 1: case 7: case 5: case 20: Spawns.Add(nodeId); break;
                case 6: Pulses = unchecked((byte)(Pulses + 1)); Pending.Add(nodeId); break;
            }
        }
    }
}
