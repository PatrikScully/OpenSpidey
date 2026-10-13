using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Core.Actors.Scripting;

internal static class ActorLinkEventCommandsTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorLinkEventCommandsTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-link-events.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            ushort operand = row.GetProperty("operand").GetUInt16(), resolved = row.GetProperty("resolved").GetUInt16();
            ushort[] links = row.GetProperty("links").EnumerateArray().Select(value => value.GetUInt16()).ToArray();
            ActorScriptState[] bodies = row.GetProperty("signals").EnumerateArray().Select((value, body) =>
                new ActorScriptState { NodeId = (ushort)(2 + body % 3), PendingSignals = value.GetUInt16() }).ToArray();
            var host = new Host(links, bodies);
            byte[] words = new byte[4]; BinaryPrimitives.WriteUInt16LittleEndian(words, operand); BinaryPrimitives.WriteUInt16LittleEndian(words.AsSpan(2), 77);
            var cursor = new ActorScriptCursor(words);
            int reads = 0;
            bool result = ActorLinkEventCommands.TryExecute(row.GetProperty("opcode").GetUInt16(), cursor, (variable, source) =>
            {
                reads++;
                if (variable == 0x212B) assert(source.ReadWord() == 77, "First-link variable consumes its actual nested node selector before link-event dispatch.");
                return unchecked((short)resolved);
            }, host);
            JsonElement expected = row.GetProperty("expected");
            assert(result == (expected.GetProperty("result").GetInt32() != 0) && cursor.Position == expected.GetProperty("consumed").GetInt32() &&
                reads == expected.GetProperty("reads").GetInt32() && host.RequestedNode == resolved &&
                bodies.Select(body => body.PendingSignals).SequenceEqual(expected.GetProperty("signals").EnumerateArray().Select(value => value.GetUInt16())) &&
                host.Spawns.SequenceEqual(expected.GetProperty("spawns").EnumerateArray().Select(value => value.GetUInt16())) &&
                host.Pending.SequenceEqual(expected.GetProperty("pending").EnumerateArray().Select(value => value.GetUInt16())) &&
                host.Pulses == expected.GetProperty("pulses").GetByte(),
                $"Original42B1/42B2 case{index++}: actual node{host.RequestedNode}, cursor{cursor.Position}, reads{reads}, signals[{string.Join(',', bodies.Select(body => body.PendingSignals))}], spawns[{string.Join(',', host.Spawns)}], pending[{string.Join(',', host.Pending)}], pulses{host.Pulses}; expected{expected}.");
        }
        var cursorUntouched = new ActorScriptCursor(new byte[] { 0x2A, 0x21 });
        assert(!ActorLinkEventCommands.TryExecute(0x42B3, cursorUntouched, (_, _) => throw new Exception("Unrelated command read a variable."),
                new Host([], [])) && cursorUntouched.Position == 0,
            "Unrelated inherited commands do not resolve operands or invoke scene event owners.");
    }

    private sealed class Host(IReadOnlyList<ushort> links, ActorScriptState[] bodies) : IActorLinkEventHost
    {
        private static readonly ushort[] kinds = [3, 3, 1, 7, 3, 6, 5, 20];
        public ushort RequestedNode { get; private set; }
        public List<ushort> Spawns { get; } = [];
        public List<ushort> Pending { get; } = [];
        public byte Pulses { get; private set; } = 255;
        public IReadOnlyList<ushort> Links(ushort nodeId) { RequestedNode = nodeId; return links; }
        public void SendSignal(ushort nodeId) => ActorLinkEventCommands.SendSignal(nodeId, kinds[nodeId],
            bodies.Take(3), bodies.Skip(3).Take(3), bodies.Skip(6).Take(3));
        public void SendPulse(ushort nodeId)
        {
            switch (kinds[nodeId])
            {
                case 1: case 7: case 5: case 20: Spawns.Add(nodeId); break;
                case 6: Pulses = unchecked((byte)(Pulses + 1)); Pending.Add(nodeId); break;
            }
        }
    }
}
