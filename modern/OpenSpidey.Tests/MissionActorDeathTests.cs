using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;

internal static class MissionActorDeathTests
{
    private sealed class Body(int id, JsonElement input)
    {
        public int Id { get; } = id;
        public ushort Node { get; } = input.GetProperty("node").GetUInt16();
        public ushort Flags { get; set; } = input.GetProperty("flags").GetUInt16();
        public FixedVector3 Position { get; } = Vector(input.GetProperty("position"));
        public int Mutation { get; } = input.GetProperty("mutation").GetInt32();
        public bool Deleted { get; set; }
        public Body? Next { get; set; }
    }
    private sealed class Host : IMissionActorDeathHost
    {
        public Body[] Bodies { get; }
        public Body?[] Heads { get; } = new Body?[5];
        public ushort[] EnvironmentFlags { get; } = [0x400, 0x80];
        public byte[] SpecialDead { get; } = [0, 0, 0];
        public List<long> Events { get; } = [];
        public Host(JsonElement input)
        {
            Bodies = input.GetProperty("bodies").EnumerateArray().Select((body, index) => new Body(index, body)).ToArray();
            for (int index = 0; index < 20; index++) if (index % 4 != 3) Bodies[index].Next = Bodies[index + 1];
            for (int list = 0; list < 5; list++) Heads[list] = Bodies[list * 4];
        }
        public object? ReadHead(SourceMissionDeathList list) => Heads[(int)list];
        public object? ReadNext(SourceMissionDeathList list, object body) => ((Body)body).Next;
        public ushort ReadNode(object body) => ((Body)body).Node;
        public bool IsDead(object body) => (((Body)body).Flags & 0x40) != 0;
        public FixedVector3 ReadPosition(object body) => ((Body)body).Position;
        public void Die(object identity)
        {
            Body body = (Body)identity; Events.AddRange([10, body.Id]);
            if (body.Mutation != 4) body.Flags |= 0x40;
            if (body.Mutation == 1) body.Next = null;
            if (body.Mutation == 2 && body.Next is { } next) body.Next = next.Next;
            if (body.Mutation == 3 && !Bodies[20].Deleted && !ReferenceEquals(Heads[1], Bodies[20]))
            { Bodies[20].Next = Heads[1]; Heads[1] = Bodies[20]; }
        }
        public void DeleteSuspendedBody(object identity)
        {
            Body body = (Body)identity; Events.AddRange([20, body.Id]); body.Deleted = true;
            int group = body.Id / 4;
            if (ReferenceEquals(Heads[group], body)) Heads[group] = body.Next;
            else for (Body? current = Heads[group]; current is not null; current = current.Next)
                if (ReferenceEquals(current.Next, body)) { current.Next = body.Next; break; }
            body.Next = null;
        }
        public void HideEnvironmentItem(uint checksum)
        { Events.AddRange([30, checksum]); if (checksum == 0x11111111) EnvironmentFlags[0] |= 1; else if (checksum == 0x22222222) EnvironmentFlags[1] |= 1; }
        public void KillSpecialDisplay(ushort node) { if (node == 7) SpecialDead[0] = 1; }
        public int[] Order(int list)
        { List<int> result = []; for (Body? body = Heads[list]; body is not null; body = body.Next) result.Add(body.Id); return result.ToArray(); }
    }
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(MissionActorDeathTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.mission-actor-death.json")!;
        using JsonDocument fixture = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in fixture.RootElement.GetProperty("boundaries").EnumerateArray())
        {
            JsonElement input = test.GetProperty("input"), expected = test.GetProperty("expected");
            var host = new Host(input);
            int kind = input.GetProperty("kind").GetInt32(), count = -1;
            var triggers = Triggers(input.GetProperty("alignment").GetInt32());
            if (kind == 0) MissionActorDeath.SendKill(triggers, triggers.Nodes[0], host);
            else if (kind == 1) count = MissionActorDeath.KillEverythingInBox(Vector(input.GetProperty("minimum")), Vector(input.GetProperty("maximum")), host);
            else count = MissionActorDeath.KillObjectsInBox(Vector(input.GetProperty("minimum")), Vector(input.GetProperty("maximum")),
                (SourceMissionDeathList)input.GetProperty("selected").GetInt32(), host);
            string context = $"Original mission death{index++} kind{kind} min{Vector(input.GetProperty("minimum"))} max{Vector(input.GetProperty("maximum"))}";
            assert(count == expected.GetProperty("count").GetInt32() && host.Events.SequenceEqual(Longs(expected.GetProperty("events"))),
                $"{context}: whole PC4E3190/4E69D0/4E6940 matches exact virtual/destructor event order, current-next reread versus captured-next and counted inclusive selection.");
            assert(host.Bodies.Select(body => (long)body.Flags).SequenceEqual(Longs(expected.GetProperty("flags"))) &&
                host.Bodies.Select(body => body.Deleted ? 1L : 0).SequenceEqual(Longs(expected.GetProperty("deleted"))),
                $"{context}: real IsDead/Die tests distinguish zombie40 from ready80 and preserve virtual overrides which leave their body alive.");
            assert(host.EnvironmentFlags.Select(value => (long)value).SequenceEqual(Longs(expected.GetProperty("environmentFlags"))) &&
                host.SpecialDead.Select(value => (long)value).SequenceEqual(Longs(expected.GetProperty("specialDead"))),
                $"{context}: kind2/9 checksum operands set only ENV flag1, and409 scans only actual SpecialDisplay9 node matches.");
            for (int list = 0; list < 5; list++) assert(host.Order(list).Select(value => (long)value).SequenceEqual(Longs(expected.GetProperty("orders")[list])),
                $"{context}: canonical list{list} retains original pointer mutations, newborn control head and suspended destructor retirement.");
            if (kind == 0)
            {
                var commandHost = new Host(input);
                new MissionActorDeathCommandHandler(11, triggers, commandHost).Execute(new(triggers.Nodes[0], 0), new(11, 4096, ReadOnlyMemory<byte>.Empty));
                assert(commandHost.Events.SequenceEqual(host.Events), "Source11 handler delegates the actual node's unchanged links.");
            }
            else if (kind == 1)
            {
                var commandHost = new Host(input);
                // Bounded coordinates in this check are source whole units; the directly captured PC leaf receives the same Q12 values.
                FixedVector3 minimum = Vector(input.GetProperty("minimum")), maximum = Vector(input.GetProperty("maximum"));
                byte[] operands = new byte[28];
                WriteVector(operands.AsSpan(2), minimum); WriteVector(operands.AsSpan(14), maximum);
                BinaryPrimitives.WriteUInt16LittleEndian(operands.AsSpan(26), 255);
                new MissionActorDeathCommandHandler(133, triggers, commandHost).Execute(new(triggers.Nodes[0], 0), new(133, 4096, operands));
                var scaledHost = new Host(input);
                MissionActorDeath.KillEverythingInBox(minimum << 12, maximum << 12, scaledHost);
                assert(commandHost.Events.SequenceEqual(scaledHost.Events), "Source133 handler retains absolute dword padding, whole-unit Q12 conversion and255 sentinel.");
            }
        }
    }
    private static TriggerFile Triggers(int alignment)
    {
        var nodes = new TriggerNode[9];
        for (int index = 0; index < nodes.Length; index++)
        {
            ushort kind = (ushort)(index == 0 ? 6 : index is 1 or 2 or 7 ? 1 : index == 3 ? 2 : index == 4 ? 9 : index == 5 ? 5 : index == 6 ? 20 : 7);
            byte[] bytes = new byte[32]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, kind);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), (ushort)(index == 7 ? 409 : 304));
            if (index == 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 8);
                for (int link = 0; link < 8; link++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4 + link * 2), (ushort)(link + 1));
            }
            if (index is 3 or 4)
            { BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 0); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(alignment == 0 ? 4 : 6), index == 3 ? 0x11111111u : 0x22222222u); }
            nodes[index] = new((ushort)index, kind, 4096 + index * 64 + alignment, bytes);
        }
        return new(nodes);
    }
    private static IEnumerable<long> Longs(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64());
    private static FixedVector3 Vector(JsonElement value) => new(value[0].GetInt32(), value[1].GetInt32(), value[2].GetInt32());
    private static void WriteVector(Span<byte> bytes, FixedVector3 value)
    { BinaryPrimitives.WriteInt32LittleEndian(bytes, value.X); BinaryPrimitives.WriteInt32LittleEndian(bytes[4..], value.Y); BinaryPrimitives.WriteInt32LittleEndian(bytes[8..], value.Z); }
}
