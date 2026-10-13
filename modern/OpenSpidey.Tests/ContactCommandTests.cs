using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;

internal static class ContactCommandTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(ContactCommandTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.contact-command.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.GetProperty("boundaries").EnumerateArray())
        {
            int[] input = Read(test.GetProperty("input"));
            JsonElement expected = test.GetProperty("expected");
            string context = $"Original static-contact dispatch [{string.Join(',', input)}]";
            ushort[] links = Enumerable.Range(1, input[3]).Select(value => (ushort)value).ToArray();
            TriggerNode source = MakeNode(0, (ushort)input[0], 100, links,
                [(ushort)134, (ushort)input[1], (ushort)3, ushort.MaxValue]);
            var file = new TriggerFile(new[] { source }.Concat(Enumerable.Range(1, 3)
                .Select(index => MakeNode((ushort)index, 6, (uint)(100 + index), [], [ushort.MaxValue]))).ToArray());
            var runtime = new MissionRuntime(file, new NoActorFactory(), []);
            for (ushort target = 1; target < 4; target++)
                for (int pulse = 0; pulse < input[2]; pulse++) runtime.ReceivePulse(target);
            runtime.ExecutePending();
            var queued = new List<ushort>();
            var commands = new List<MissionTrace>();
            runtime.Trace += trace =>
            {
                if (trace.Kind == MissionTraceKind.Queued && trace.NodeId != 0) queued.Add(trace.NodeId);
                if (trace.Kind == MissionTraceKind.CommandStarted && trace.NodeId == 0) commands.Add(trace);
            };
            runtime.Queue(0);
            int executed = runtime.ExecutePending();
            assert(runtime.CommandPoints[0].RemainingPulses == expected.GetProperty("remaining").GetInt32() &&
                runtime.CommandPoints[0].InitialPulsesSet == (input[0] == 6),
                context + " binds the source NumPulses policy only for kind6.");
            assert(Enumerable.Range(1, 3).Select(index => (int)runtime.CommandPoints[(ushort)index].PulsesReceived)
                .SequenceEqual(Read(expected.GetProperty("pulses"))) && queued.Select(value => (int)value)
                .SequenceEqual(Read(expected.GetProperty("queued"))) && executed == 1 + queued.Count,
                context + " sends genuine links in original order, wraps target byte counters and executes appended events in the same pass.");
            assert(new[] { (int)OriginalContactCommandProgram.SendPulseOpcode, OriginalContactCommandProgram.Terminator }
                .SequenceEqual(Read(expected.GetProperty("sharedWords"))),
                context + " matches the actual executable words at5580C0.");
            if (input[0] is 2 or 9)
                assert(commands.Count == 1 && commands[0].Opcode == 3 &&
                    commands[0].SourceOffset == OriginalContactCommandProgram.SourceAddress &&
                    commands[0].SourceKind == TriggerInstructionSourceKind.OriginalExecutable,
                    context + " retains the static executable program's provenance through source execution diagnostics.");
        }

        var banks = new HashSet<string>();
        int authored = 0;
        foreach (JsonElement item in document.RootElement.GetProperty("authored").EnumerateArray())
        {
            string bank = item.GetProperty("bank").GetString()!; banks.Add(bank);
            var node = new TriggerNode(item.GetProperty("node").GetUInt16(), item.GetProperty("kind").GetUInt16(),
                item.GetProperty("offset").GetInt32(), Convert.FromHexString(item.GetProperty("data").GetString()!));
            IReadOnlyList<TriggerInstruction> instructions = TriggerCommandReader.Read(node);
            assert(TriggerEnvironmentReference.ReadChecksum(node) == item.GetProperty("checksum").GetUInt32() &&
                MissionRuntime.ReadLinks(node).Select(value => (int)value).SequenceEqual(Read(item.GetProperty("links"))),
                $"Actual {bank} contact{node.Id} retains its source link header and absolutely aligned ENV checksum.");
            assert(ReferenceEquals(instructions, OriginalContactCommandProgram.Instructions) && instructions.Count == 1 &&
                instructions[0].Opcode == 3 && instructions[0].Arguments.IsEmpty &&
                instructions[0].SourceOffset == OriginalContactCommandProgram.SourceAddress &&
                instructions[0].SourceKind == TriggerInstructionSourceKind.OriginalExecutable,
                $"Actual {bank} contact{node.Id} owns the shared executable program instead of interpreting bytes after its checksum or silently returning an empty program.");
            authored++;
        }
        assert(authored == 101 && banks.SetEquals(new[] { "data/l2a1_t.trg", "data/l2a2_t.trg", "data/l4a1_t.trg", "data/lda2_t.trg", "data/lda3_t.trg" }),
            "The full source archive contributes all101 static ENV contacts from the five actual banks.");
        var malformed = new TriggerNode(0, 9, 0, new byte[] { 9, 0, 255, 255 });
        try { TriggerCommandReader.Read(malformed); throw new Exception("Static ENV contact accepted a truncated header."); }
        catch (InvalidDataException) { assert(true, "Static executable program selection still validates its real source-node link/checksum header."); }
    }

    private static int[] Read(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt32()).ToArray();

    private static TriggerNode MakeNode(ushort id, ushort kind, uint checksum, ushort[] links, ushort[] commands)
    {
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output);
        writer.Write(kind); writer.Write((ushort)links.Length);
        foreach (ushort link in links) writer.Write(link);
        while (output.Position % 4 != 0) writer.Write((byte)0);
        writer.Write(checksum);
        if (kind == 6) foreach (ushort command in commands) writer.Write(command);
        return new(id, kind, 0, output.ToArray());
    }

    private sealed class NoActorFactory : IMissionActorFactory
    {
        public void Spawn(TriggerNode node) => throw new InvalidOperationException("This PC boundary contains only actual command-point targets.");
    }
}
