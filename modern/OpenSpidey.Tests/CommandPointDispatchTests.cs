using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;

internal static class CommandPointDispatchTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(CommandPointDispatchTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.command-point.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            uint[] checksums = test.GetProperty("checksums").EnumerateArray().Select(value => value.GetUInt32()).ToArray();
            var runtime = new MissionRuntime(new(checksums.Select((checksum, index) => Point((ushort)index, checksum)).ToArray()),
                new NoActors(), []);
            var pending = new List<ushort>();
            var completed = new List<ushort>();
            runtime.Trace += trace => { if (trace.Kind == MissionTraceKind.Queued) pending.Add(trace.NodeId); };
            runtime.CommandListCompleted += context => completed.Add(context.Node.Id);
            JsonElement[] expected = test.GetProperty("states").EnumerateArray().ToArray();
            int step = 0;
            foreach (JsonElement action in test.GetProperty("actions").EnumerateArray())
            {
                int kind = action[0].GetInt32();
                int result = -1;
                switch (kind)
                {
                    case 0: runtime.BeginContactFrame(); break;
                    case 1: result = runtime.TriggerCommandPoint(action[1].GetUInt32()) is { } triggered ? triggered.NodeId : -1; break;
                    case 2: runtime.EndContactFrame(); break;
                    case 3: runtime.ExecutePending(); pending.Clear(); break;
                    default: throw new InvalidDataException("Unknown original command-point oracle action.");
                }
                JsonElement state = expected[step++];
                assert(result == state.GetProperty("result").GetInt32(),
                    "Direct command checksum dispatch returns the first unexecuted source hash-chain match.");
                JsonElement[] flagRows = state.GetProperty("flags").EnumerateArray().ToArray();
                for (ushort index = 0; index < checksums.Length; index++)
                {
                    CommandPointState point = runtime.CommandPoints[index];
                    assert(point.ContactThisFrame == (flagRows[index][0].GetInt32() != 0) &&
                        point.ExecutedForContact == (flagRows[index][1].GetInt32() != 0),
                        "Visited-only source collision flags and uncollided rearming match actual PC dispatch.");
                    assert(point.PulsesReceived == 0, "Direct crate command triggers preserve the separate source pulse counter.");
                }
                assert(pending.SequenceEqual(state.GetProperty("pending").EnumerateArray().Select(value => value.GetUInt16())) &&
                    runtime.PendingCount == pending.Count && completed.SequenceEqual(state.GetProperty("completed").EnumerateArray().Select(value => value.GetUInt16())),
                    "Direct command-point queues and actual execution preserve the original pending-list order.");
            }
        }
    }

    private static TriggerNode Point(ushort id, uint checksum)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)6); writer.Write((ushort)0); writer.Write(checksum); writer.Write((ushort)65535);
        return new(id, 6, 0, stream.ToArray());
    }

    private sealed class NoActors : IMissionActorFactory
    {
        public void Spawn(TriggerNode node) => throw new InvalidOperationException("Command-point dispatch does not construct fixture actors.");
    }
}
