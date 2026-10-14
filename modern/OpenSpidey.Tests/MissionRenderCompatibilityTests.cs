using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;

internal static class MissionRenderCompatibilityTests
{
    private static readonly ushort[][] Patterns = [[2, 1, 9, 7, 5, 20], [1, 2, 7, 9, 20, 5],
        [2, 9, 2, 9, 2, 9], [1, 7, 5, 20, 3, 8]];
    private static readonly ushort[] Links = [1, 2, 3, 4, 5, 6, 1];

    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(MissionRenderCompatibilityTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.mission-render-compatibility.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        int index = 0;
        foreach (JsonElement fixture in document.RootElement.EnumerateArray())
        {
            int[] input = fixture.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            bool linked = input[0] != 0;
            ushort[] program = [linked ? (ushort)13 : (ushort)151, (ushort)input[2], 147, 0x5678, 302, 1234, 65535];
            TriggerNode context = Context(linked, program);
            TriggerNode[] references = Patterns[input[6]].Select((kind, ordinal) => Reference((ushort)(ordinal + 1), kind, input[1], input[5])).ToArray();
            var file = new TriggerFile(new[] { context }.Concat(references).ToArray());
            var host = new Host((ushort)input[3], input[4]);
            var invalid = new List<ushort>();
            IMissionCommandHandler[] handlers = [new DualBufferSizeCommandHandler(), new GameLevelCommandHandler(host),
                new EndLevelNodeCommandHandler(host), new LinkedEnvironmentVisibilityCommandHandler(file, host, node => invalid.Add(node.Id))];
            var runtime = new MissionRuntime(file, new NoActors(), handlers);
            var completed = new List<ushort>();
            runtime.Trace += trace => { if (trace.Kind == MissionTraceKind.CommandCompleted && trace.Opcode is { } opcode) completed.Add(opcode); };
            runtime.Queue(0); runtime.ExecutePending();
            JsonElement expected = fixture.GetProperty("expected");
            assert(host.GameLevel == expected.GetProperty("gameLevel").GetInt32() && host.EndNode == expected.GetProperty("endNode").GetInt32() &&
                completed.SequenceEqual(new ushort[] { linked ? (ushort)13 : (ushort)151, 147, 302 }),
                $"Whole originalPC rendering-command case{index} consumes exactly one operand and retains following147/302 execution in order.");
            assert(host.Lookups.SequenceEqual(expected.GetProperty("lookups").EnumerateArray().Select(value => value.GetUInt32())),
                $"Whole original13 case{index} preserves mixed-kind link order, aligned checksum lookup and duplicate identity.");
            var wanted = expected.GetProperty("items").EnumerateArray().Select(value => (value[0].GetUInt32(), value[1].GetUInt16()));
            assert(host.Items.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value)).SequenceEqual(wanted),
                $"Whole original13 case{index} changes only source ENVflag0 and preserves absent lookups and all other flags.");
            assert(invalid.Count == expected.GetProperty("invalidLinks").GetInt32() &&
                (!linked || invalid.SequenceEqual(Links.Where(id => Patterns[input[6]][id - 1] is not (2 or 9)))),
                $"Whole original13 case{index} diagnoses non-ENV links and continues to later authored entries.");
            int[] watched = expected.GetProperty("watched").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            assert(watched.SequenceEqual(Enumerable.Range(0, 6).Select(offset => 0x13570000 + offset * 37).Append(7)) &&
                expected.GetProperty("dualCalls").GetInt32() == (linked ? 0 : 1),
                $"Whole originalPC151/13 case{index++} preserves screen/shadow/flash/node-count globals;151 performs only its original diagnostic call.");
        }
        var bare = new TriggerNode(0, 4, 100, new byte[] { 4, 0, 255, 255 });
        foreach (byte[] wrong in new[] { Array.Empty<byte>(), new byte[1], new byte[3], new byte[4] })
        {
            bool rejected = false;
            try { new DualBufferSizeCommandHandler().Execute(new(bare, 0), new(151, 102, wrong)); }
            catch (InvalidDataException) { rejected = true; }
            assert(rejected, "Only the exact original151 one-word encoding is accepted; it cannot swallow another command.");
        }
    }

    private static TriggerNode Context(bool linked, ushort[] program)
    {
        int commandOffset = linked ? 24 : 2;
        var bytes = new byte[commandOffset + program.Length * 2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, linked ? (ushort)6 : (ushort)4);
        if (linked)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), (ushort)Links.Length);
            for (int index = 0; index < Links.Length; index++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4 + index * 2), Links[index]);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), 0x77778888);
        }
        for (int index = 0; index < program.Length; index++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(commandOffset + index * 2), program[index]);
        return new(0, linked ? (ushort)6 : (ushort)4, 100, bytes);
    }
    private static TriggerNode Reference(ushort id, ushort kind, int alignment, int count)
    {
        int sourceOffset = 1000 + alignment;
        if (kind == 8)
        {
            int nameOffset = ((sourceOffset + 7) & ~3) - sourceOffset + 18;
            var restart = new byte[nameOffset + 4];
            BinaryPrimitives.WriteUInt16LittleEndian(restart, kind);
            BinaryPrimitives.WriteUInt16LittleEndian(restart.AsSpan(nameOffset + 2), 65535);
            return new(id, kind, sourceOffset, restart);
        }
        int checksumOffset = 4 + count * 2;
        checksumOffset += (-(sourceOffset + checksumOffset)) & 3;
        var bytes = new byte[checksumOffset + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, kind);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), (ushort)count);
        for (int link = 0; link < count; link++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4 + link * 2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(checksumOffset), (id & 1) == 0 ? 0x22222222u : 0x11111111u);
        return new(id, kind, sourceOffset, bytes);
    }
    private sealed class NoActors : IMissionActorFactory
    {
        public void Spawn(TriggerNode node) => throw new InvalidOperationException("Source151/13 never constructs an actor.");
    }
    private sealed class Host(ushort flags, int lookupMode) : IEnvironmentVisibilityHost, IMissionGameLevelHost, IMissionEndLevelNodeHost
    {
        public int GameLevel { get; set; } = -123;
        public int EndNode { get; private set; } = -234;
        public List<uint> Lookups { get; } = [];
        public Dictionary<uint, ushort> Items { get; } = [];
        public void SetEndLevelNode(ushort node) => EndNode = node;
        public void SetVisibility(uint checksum, bool visible)
        {
            Lookups.Add(checksum);
            if (lookupMode == 0 || (lookupMode == 2 && checksum == 0x22222222)) return;
            Items[checksum] = EnvironmentVisibilityPolicy.SetVisible(Items.GetValueOrDefault(checksum, flags), visible);
        }
    }
}
