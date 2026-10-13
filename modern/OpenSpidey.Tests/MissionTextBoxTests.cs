using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.UI;

internal static class MissionTextBoxTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(MissionTextBoxTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.mission-textbox.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement fixture in document.RootElement.EnumerateArray())
        {
            long[] input = fixture.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            JsonElement expected = fixture.GetProperty("expected");
            var messages = new MissionMessages(new OriginalMissionMessageData("Checkpoint"u8.ToArray()));
            messages.ShowCheckpointMessage();
            SimpleMissionMessage text = messages.AddSimpleMessage("source"u8.ToArray(), unchecked((uint)input[8]), 0, 125, unchecked((int)input[9]));
            var host = new Host(messages, unchecked((int)input[10]));
            byte[] arguments = new byte[16];
            for (int word = 0; word < 8; word++) BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(word * 2), unchecked((ushort)input[word]));
            var context = new MissionCommandContext(new TriggerNode(0, 4, 0, ReadOnlyMemory<byte>.Empty), 0);
            var handlers = MissionTextBoxCommandHandler.CreateHandlers(host).ToDictionary(handler => handler.Opcode);
            handlers[208].Execute(context, new(208, 0, arguments));
            MissionTextBoxState state = host.SourceOrder.Single();
            MissionTextBoxDefinition definition = state.Definition;
            long[] actual = [definition.X, definition.Y, definition.Width, definition.Height, definition.Lifetime,
                definition.Red, definition.Green, definition.Blue];
            assert(actual.SequenceEqual(Numbers(expected.GetProperty("definition"))),
                $"Actual PC208 case{index} preserves signed placement/height, ignored width word, retained source font-width globals, wrapped absolute/width math, ushort lifetime and low-byte dimmed RGB; actual=[{string.Join(',', actual)}].");
            state.Age = unchecked((short)input[11]);
            JsonElement[] steps = expected.GetProperty("states").EnumerateArray().ToArray();
            for (int step = 0; step < steps.Length; step++)
            {
                if (step != 0) state.Move(unchecked((int)input[11 + step]));
                var quad = state.DisplayQuad();
                long[] snapshot = [state.Age, state.LastTimer, state.ElapsedTicks, state.Dead ? 1 : 0,
                    quad.Left, quad.Top, quad.Right, quad.Bottom, state.PackedColour];
                assert(snapshot.SequenceEqual(Numbers(steps[step])),
                    $"Actual CTextBox40D770/display case{index} step{step} keeps full signed timer delta, short age wrap, strict lifetime death, unsigned pop-in/out shifts, short quad stores and raw packed alpha160; actual=[{string.Join(',', snapshot)}], expected=[{string.Join(',', Numbers(steps[step]))}].");
            }
            state.Protected = true;
            host.CreateTextBox(new(1, 2, 3, 4, 5, definition.Red, definition.Green, definition.Blue));
            host.CreateTextBox(new(6, 7, 8, 9, 10, definition.Red, definition.Green, definition.Blue));
            handlers[219].Execute(context, new(219, 0, ReadOnlyMemory<byte>.Empty));
            assert(host.Destroyed.SequenceEqual(Numbers(expected.GetProperty("clear")).Select(value => (int)value)) &&
                expected.GetProperty("remaining").GetInt32() == 0 && expected.GetProperty("bitCount").GetInt32() == 0 &&
                host.SourceOrder.Count == 0 && ReferenceEquals(messages.SourceSimpleMessages.Single(), text) && messages.SourceMessages.Count == 1 &&
                messages.LastSimpleMessageX == unchecked((uint)input[8]) && messages.LastSimpleMessageWidth == unchecked((int)input[9]),
                "Actual219 clears every TextBox head including protected bits, retaining separate simple/checkpoint messages and process font-width globals.");
            index++;
        }
    }

    private static IEnumerable<long> Numbers(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64());
    private sealed class Host(MissionMessages messages, int timer) : IMissionTextBoxHost
    {
        private readonly Dictionary<MissionTextBoxState, int> identities = [];
        public List<MissionTextBoxState> SourceOrder { get; } = [];
        public List<int> Destroyed { get; } = [];
        public uint LastSimpleMessageX => messages.LastSimpleMessageX;
        public int LastSimpleMessageWidth => messages.LastSimpleMessageWidth;
        public void CreateTextBox(MissionTextBoxDefinition definition)
        {
            var box = new MissionTextBoxState(definition, timer);
            identities.Add(box, identities.Count); SourceOrder.Insert(0, box);
        }
        public void ClearTextBoxes()
        {
            foreach (MissionTextBoxState box in SourceOrder.ToArray()) { Destroyed.Add(identities[box]); SourceOrder.Remove(box); }
        }
    }
}
