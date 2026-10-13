using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;

internal static class MissionRuntimeTests
{
    public static void Run(Action<bool, string> assert)
    {
        var recorder = new SoundRecorder();
        var factory = new ActorFactory();
        var source = Point(0, 100, [1], [134, 2, 3, 65535]);
        var destination = Point(1, 101, [], [105, 7, 65535]);
        var runtime = new MissionRuntime(new([source, destination]), factory, [recorder]);
        runtime.ReceivePulse(0); runtime.ReceivePulse(0); runtime.ReceivePulse(0);
        assert(runtime.ExecutePending() == 5, "Appended pulses execute in the same pass.");
        assert(recorder.Calls.SequenceEqual(new[] { (1, 7), (1, 7) }), "Initial pulse limit is set once and decremented.");
        assert(runtime.CommandPoints[0].RemainingPulses == 0, "Exhausted source pulse count.");

        recorder.Calls.Clear();
        runtime = new(new([Point(0, 99, [], [105, 10, 65535]), Point(1, 99, [], [105, 11, 65535])]), factory, [recorder]);
        runtime.BeginContactFrame(); runtime.NotifyContact(99); runtime.NotifyContact(99); runtime.EndContactFrame();
        runtime.ExecutePending();
        assert(recorder.Calls.SequenceEqual(new[] { (1, 11), (0, 10) }), "Duplicate checksums fire in original reverse-creation order, once per contact.");
        runtime.BeginContactFrame(); runtime.NotifyContact(99); runtime.EndContactFrame();
        assert(runtime.PendingCount == 0, "Continuous contact does not execute again.");
        runtime.BeginContactFrame(); runtime.EndContactFrame();
        runtime.BeginContactFrame(); runtime.NotifyContact(99); runtime.EndContactFrame();
        assert(runtime.PendingCount == 2, "Contact points rearm after leaving the collider.");

        recorder.Calls.Clear();
        runtime = new(new([Point(0, 1, [], [148, 2, 105, 42, 149, 65535])]), factory, [recorder]);
        runtime.ReceivePulse(0); runtime.ExecutePending();
        assert(recorder.Calls.Count == 0, "False pulse condition skips the body.");
        runtime.ReceivePulse(0); runtime.ExecutePending();
        assert(recorder.Calls.SequenceEqual(new[] { (0, 42) }), "Matching pulse condition executes the body.");

        runtime = new(new([Point(0, 1, [0], [134, 65535, 3, 65535])]), factory, []);
        runtime.ReceivePulse(0);
        assert(runtime.ExecutePending(3) == 3 && runtime.PendingCount == 1, "Cyclic events yield after the frame budget.");
        assert(runtime.CommandPoints[0].RemainingPulses == 65535, "Unlimited pulse count is not decremented.");

        runtime = new(new([Point(0, 1, [], [65535])]), factory, []);
        for (int index = 0; index < 256; index++) runtime.ReceivePulse(0);
        assert(runtime.PendingCount == 256 && runtime.CommandPoints[0].PulsesReceived == 0,
            "The queue grows beyond the original 16 slots and the original byte pulse counter wraps.");
        assert(runtime.ExecutePending() == 256, "Every queued event executes without overwriting another object.");

        recorder.Calls.Clear();
        runtime = new(new([Point(0, 99, [], [105, 10, 65535])]), factory, [recorder]);
        runtime.ReceivePulse(0);
        runtime.BeginContactFrame(); runtime.TriggerCommandPoint(99); runtime.EndContactFrame();
        runtime.ClearPendingEvents();
        assert(runtime.PendingCount == 0 && runtime.ExecutePending() == 0 && recorder.Calls.Count == 0 &&
            runtime.CommandPoints[0].PulsesReceived == 1 && runtime.CommandPoints[0].ExecutedForContact,
            "Trig_ZeroPendingList discards queued effects while retaining received pulses and continuous-contact suppression.");
        runtime.TriggerCommandPoint(99);
        assert(runtime.PendingCount == 0, "Clearing queued points does not rearm an already contacted source point.");
        runtime.BeginContactFrame(); runtime.EndContactFrame();
        runtime.TriggerCommandPoint(99);
        assert(runtime.PendingCount == 1, "The normal no-contact phase still rearms a point whose earlier queued effects were cleared.");

        var blocking = new BlockingCommand();
        recorder.Calls.Clear();
        runtime = new(new([Point(0, 1, [], [148, 1, 105, 1, 190, 11, 105, 2, 149, 65535]),
            Point(1, 2, [], [105, 3, 65535])]), factory, [recorder, blocking]);
        runtime.ReceivePulse(0);
        runtime.ReceivePulse(1);
        assert(runtime.ExecutePending() == 0 && runtime.IsBlocked && runtime.PendingCount == 2,
            "A cinema suspends its partially executed command point and subsequent queued events.");
        assert(recorder.Calls.SequenceEqual(new[] { (0, 1) }) && blocking.Calls == 1,
            "Commands after cinema do not run behind playback.");
        runtime.ExecutePending();
        assert(blocking.Calls == 1 && recorder.Calls.Count == 1, "Blocked frame polling does not replay movie or earlier commands.");
        blocking.IsBlocking = false;
        assert(runtime.ExecutePending() == 2 && !runtime.IsBlocked && runtime.PendingCount == 0 &&
            recorder.Calls.SequenceEqual(new[] { (0, 1), (0, 2), (1, 3) }),
            "Movie completion resumes inside its conditional and preserves event order.");

        recorder.Calls.Clear();
        runtime = new(new([Point(0, 1, [], [105, 1, 190, 11, 105, 2, 65535]),
            Point(1, 2, [], [105, 3, 65535])]), factory, [recorder, blocking]);
        runtime.Queue(0); runtime.Queue(1); runtime.ExecutePending();
        runtime.ClearPendingEvents();
        assert(runtime.IsBlocked && runtime.PendingCount == 1 && recorder.Calls.SequenceEqual(new[] { (0, 1) }),
            "Pending-list clearing preserves the currently executing cinema continuation and discards only subsequent queued points.");
        blocking.IsBlocking = false;
        assert(runtime.ExecutePending() == 1 && recorder.Calls.SequenceEqual(new[] { (0, 1), (0, 2) }),
            "The cleared queue cannot replay a later point when the genuine current command list resumes.");

        runtime = new(new([Point(0, 1, [], [105, 1, 65535])]), factory, []);
        runtime.ReceivePulse(0);
        try { runtime.ExecutePending(); throw new Exception("Missing command handler was ignored."); }
        catch (InvalidOperationException exception)
        {
            assert(exception.Message.Contains("Node 0, command 105"), "Missing gameplay handlers report the source node and opcode.");
        }
        try { runtime.Queue(0); throw new Exception("Faulted mission was replayed."); }
        catch (InvalidOperationException exception)
        {
            assert(exception.Message.Contains("reload"), "A partly executed program cannot replay its side effects.");
        }

        TriggerNode truncated = new(0, 6, 0, new byte[] { 6, 0, 255, 255 });
        try { TriggerCommandReader.Read(truncated); throw new Exception("Truncated links were accepted."); }
        catch (InvalidDataException) { assert(true, "Truncated command-point links are rejected."); }

        TriggerNode unsupported = Point(0, 1, [], [999, 65535]);
        try { TriggerCommandReader.Read(unsupported); throw new Exception("Unknown opcode was ignored."); }
        catch (InvalidDataException exception)
        {
            assert(exception.Message.Contains("Unknown command 999"), "Unknown command layouts fail before execution.");
        }
    }

    private static TriggerNode Point(ushort id, uint checksum, ushort[] links, ushort[] commands)
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        writer.Write((ushort)6); writer.Write((ushort)links.Length);
        foreach (ushort link in links) writer.Write(link);
        while (output.Position % 4 != 0) writer.Write((byte)0);
        writer.Write(checksum);
        foreach (ushort command in commands) writer.Write(command);
        return new(id, 6, 0, output.ToArray());
    }

    private sealed class SoundRecorder : IMissionCommandHandler
    {
        public ushort Opcode => 105;
        public List<(int Node, int Sound)> Calls { get; } = [];
        public void Execute(MissionCommandContext context, TriggerInstruction instruction) =>
            Calls.Add((context.Node.Id, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span)));
    }

    internal sealed class ActorFactory : IMissionActorFactory
    {
        public void Spawn(TriggerNode node) => throw new InvalidOperationException("Actor spawning is not part of this fixture.");
    }

    private sealed class BlockingCommand : IMissionBlockingCommandHandler
    {
        public ushort Opcode => 190;
        public bool IsBlocking { get; set; }
        public int Calls { get; private set; }
        public void Execute(MissionCommandContext context, TriggerInstruction instruction) { Calls++; IsBlocking = true; }
    }
}
