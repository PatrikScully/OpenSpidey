using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.UI;

internal static class MissionTextTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(MissionTextTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.mission-text.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        var data = new OriginalMissionMessageData("Checkpoint"u8.ToArray())
        {
            Substitutions = document.RootElement.GetProperty("substitutions").EnumerateArray().Select(value =>
                new OriginalMessageSubstitution(value.GetProperty("pattern").GetString()!, value.GetProperty("format").GetString()!,
                    value.GetProperty("first").GetUInt16(), value.GetProperty("second").GetUInt16())).ToArray()
        };
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var names = new ControlNames { Mode = test.GetProperty("mode").GetInt32() };
            var host = new MessageHost(data); host.Messages.ShowCheckpointMessage();
            var handlers = MissionTextCommandHandler.CreateHandlers(host, data, names).ToDictionary(handler => handler.Opcode);
            byte[] text = Encoding.Latin1.GetBytes(test.GetProperty("text").GetString()!);
            int words = (text.Length + 2) / 2;
            byte[] arguments = new byte[words * 2 + 6]; text.CopyTo(arguments, 0);
            BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(words * 2), test.GetProperty("x").GetUInt16());
            BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(words * 2 + 2), test.GetProperty("y").GetUInt16());
            BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(words * 2 + 4), test.GetProperty("duration").GetUInt16());
            var context = new MissionCommandContext(new TriggerNode(0, 4, 0, ReadOnlyMemory<byte>.Empty), 0);
            var instruction = new TriggerInstruction(198, 0, arguments);
            handlers[198].Execute(context, instruction);
            SimpleMissionMessage first = host.Messages.SourceSimpleMessages[0];
            JsonElement expected = test.GetProperty("expected");
            assert(TextBytes(first.Text).SequenceEqual(expected.GetProperty("text").EnumerateArray().Select(value => value.GetByte())) &&
                first.X == expected.GetProperty("x").GetUInt32() && first.Y == expected.GetProperty("y").GetUInt32() &&
                first.Duration == expected.GetProperty("duration").GetUInt32(),
                "Actual PC198 preserves aligned Latin1 strings, exact whole-message wildcard matching, source keyboard substitutions and unsigned placement/lifetime words.");
            assert(names.Requests.SequenceEqual(expected.GetProperty("actions").EnumerateArray().Select(value => value.GetUInt16())),
                "Tutorial substitutions request the original first/second action masks in source order, including zero/unbound actions.");
            names.Mode = 2;
            handlers[198].Execute(context, instruction);
            SimpleMissionMessage second = host.Messages.SourceSimpleMessages[0];
            assert(TextBytes(second.Text).SequenceEqual(expected.GetProperty("second").EnumerateArray().Select(value => value.GetByte())) &&
                TextBytes(first.Text).SequenceEqual(expected.GetProperty("firstAfterRepeat").EnumerateArray().Select(value => value.GetByte())),
                "Original per-pattern256-byte slots are shared by already-live messages; subsequent substitutions retain actual PC text aliasing.");
            handlers[218].Execute(context, new(218, 0, ReadOnlyMemory<byte>.Empty));
            assert(expected.GetProperty("clears").GetInt32() == 1 && host.Messages.SourceSimpleMessages.Count == 0 && host.Messages.SourceMessages.Count == 1,
                "Source218 clears genuine simple-message ownership while preserving separate checkpoint SMessage records.");
        }
        var substitutions = new TutorialMessageSubstitution(data, new ControlNames { Mode = 3 });
        bool rejected = false;
        try { substitutions.Substitute("AB to Punch and CD to Kick"u8.ToArray()); }
        catch (InvalidDataException) { rejected = true; }
        assert(rejected, "Modern tutorial parsing bounds the original unsafe one-printf-argument/two-placeholder branch instead of reading an absent native string pointer.");
    }

    private static byte[] TextBytes(ReadOnlyMemory<byte> memory)
    {
        int terminator = memory.Span.IndexOf((byte)0);
        return (terminator < 0 ? memory : memory[..terminator]).ToArray();
    }

    private sealed class MessageHost(OriginalMissionMessageData data) : IMissionTextMessageHost
    {
        public MissionMessages Messages { get; } = new(data);
        public SimpleMissionMessage ShowSimpleMessage(ReadOnlyMemory<byte> text, uint x, uint y, uint duration) =>
            Messages.AddSimpleMessage(text, x, y, duration, 51);
        public void ClearSimpleMessages() => Messages.ClearSimpleMessages();
    }

    private sealed class ControlNames : IMissionControlNames
    {
        public int Mode { get; set; }
        public List<ushort> Requests { get; } = [];
        public MissionControlName Read(ushort sourceAction)
        {
            Requests.Add(sourceAction);
            bool bound = sourceAction != 0 && Mode != 0 && !(Mode == 2 && sourceAction == 64) && !(Mode == 3 && sourceAction == 32);
            string name = bound ? sourceAction switch
            { 16 => "Space", 32 => "K", 64 => "J", 128 => "L", 256 => "T", 512 => "E", 1024 => "S",
                _ => throw new InvalidDataException("Fixture requested an unknown original input mask.") } : "NoKey";
            return new(bound, name);
        }
    }
}
