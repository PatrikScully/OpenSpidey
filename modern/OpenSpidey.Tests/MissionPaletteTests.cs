using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;

internal static class MissionPaletteTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(MissionPaletteTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.mission-palette.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        int index = 0;
        foreach (JsonElement fixture in document.RootElement.EnumerateArray())
        {
            int[] input = fixture.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            ushort[] program = Program(input[0], (ushort)input[2], (ushort)input[3]);
            var file = new TriggerFile([Context(program, input[1])]);
            var host = new Host();
            IMissionCommandHandler[] handlers = [.. SpeedupCommandHandler.CreateHandlers(), .. PaletteFadeCommandHandler.CreateHandlers(host),
                new GameLevelCommandHandler(host), new EndLevelNodeCommandHandler(host)];
            var runtime = new MissionRuntime(file, new NoActors(), handlers);
            var completed = new List<ushort>();
            runtime.Trace += trace => { if (trace.Kind == MissionTraceKind.CommandCompleted && trace.Opcode is { } opcode) completed.Add(opcode); };
            runtime.Queue(0); runtime.ExecutePending();

            JsonElement expected = fixture.GetProperty("expected");
            assert(host.GameLevel == expected.GetProperty("gameLevel").GetInt32() && host.EndNode == expected.GetProperty("endNode").GetInt32() &&
                completed.SequenceEqual(Opcodes(input[0])),
                $"Whole original PC palette case{index} consumes the source operands and retains following147/302 in order.");
            var wanted = expected.GetProperty("calls").EnumerateArray()
                .Select(call => call.EnumerateArray().Select(value => value.GetUInt32()).ToArray()).ToArray();
            assert(host.Calls.Count == wanted.Length && host.Calls.Zip(wanted).All(pair => pair.First.SequenceEqual(pair.Second)),
                $"Whole original PC palette case{index} calls Mysterio slots1/2 in order with unsigned source arguments.");
            assert(expected.GetProperty("watched").EnumerateArray().Select(value => value.GetInt32())
                    .SequenceEqual(Enumerable.Range(0, 6).Select(offset => 0x13570000 + offset * 37)),
                $"Whole original PC palette case{index++} leaves speedup and presentation globals unchanged.");
        }

        var bare = new TriggerNode(0, 4, 100, new byte[] { 4, 0, 255, 255 });
        var paletteHost = new Host();
        foreach (var (opcode, wrong) in new (ushort, byte[])[] { (303, []), (303, new byte[2]), (303, new byte[6]), (304, new byte[2]),
            (300, new byte[2]), (301, new byte[4]) })
        {
            IMissionCommandHandler handler = opcode >= 303
                ? PaletteFadeCommandHandler.CreateHandlers(paletteHost).Single(candidate => candidate.Opcode == opcode)
                : SpeedupCommandHandler.CreateHandlers().Single(candidate => candidate.Opcode == opcode);
            bool rejected = false;
            try { handler.Execute(new(bare, 0), new(opcode, 102, wrong)); }
            catch (InvalidDataException) { rejected = true; }
            assert(rejected && paletteHost.Calls.Count == 0, $"Command{opcode} accepts only its exact original operand encoding.");
        }
    }

    private static ushort[] Opcodes(int mode)
    {
        var opcodes = new List<ushort>();
        if (mode != 1) opcodes.AddRange([300, 301]);
        if (mode != 0) opcodes.AddRange([303, 304, 303]);
        if (mode == 2) opcodes.AddRange([301, 300, 304]);
        opcodes.AddRange([147, 302]);
        return [.. opcodes];
    }

    private static ushort[] Program(int mode, ushort blue, ushort packed)
    {
        var words = new List<ushort>();
        if (mode != 1) words.AddRange([300, 301]);
        if (mode != 0) words.AddRange([303, blue, packed, 304, 303, packed, blue]);
        if (mode == 2) words.AddRange([301, 300, 304]);
        words.AddRange([147, 0x5678, 302, 1234, 65535]);
        return [.. words];
    }

    private static TriggerNode Context(ushort[] program, int alignment)
    {
        var bytes = new byte[2 + program.Length * 2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 4);
        for (int index = 0; index < program.Length; index++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2 + index * 2), program[index]);
        return new(0, 4, 100 + alignment, bytes);
    }

    private sealed class NoActors : IMissionActorFactory
    {
        public void Spawn(TriggerNode node) => throw new InvalidOperationException("Source palette/speedup commands never construct an actor.");
    }

    private sealed class Host : IMissionPaletteFadeHost, IMissionGameLevelHost, IMissionEndLevelNodeHost
    {
        public int GameLevel { get; set; } = -123;
        public int EndNode { get; private set; } = -234;
        public List<uint[]> Calls { get; } = [];
        public void SetEndLevelNode(ushort node) => EndNode = node;
        public void FadeUp(PaletteFadeColour colour) => Calls.Add([1, colour.Red, colour.Green, colour.Blue]);
        public void FadeDown() => Calls.Add([2, 0, 0, 0]);
    }
}
