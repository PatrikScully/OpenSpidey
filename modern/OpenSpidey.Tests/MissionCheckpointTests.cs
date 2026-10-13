using System.Text;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;

internal static class MissionCheckpointTests
{
    public static void Run(Action<bool, string> assert)
    {
        var file = new TriggerFile([Autoexec(), Restart(1, "Re_Start"), Restart(2, "Checkpoint"),
            Restart(3, "Re_Start_Death"), Restart(4, "RE_START"), Restart(5, "CAF\u00c9")]);
        var checkpoints = new MissionCheckpoints(file);
        CheckpointSelection initial = checkpoints.Select("re_start_death");
        assert(!initial.SaveRequired && !initial.ShowMessage && !checkpoints.HasNonDeathRestart,
            "Initial death restart selection neither saves nor sets the non-death flag.");
        CheckpointSelection normal = checkpoints.Select("RE_START");
        assert(normal.Point.NodeId == 1 && normal.SaveRequired && normal.ShowMessage && checkpoints.HasNonDeathRestart,
            "ASCII case-insensitive selection uses the first matching restart and requests a checkpoint save.");
        CheckpointSelection repeated = checkpoints.Select("Re_Start");
        assert(!repeated.SaveRequired && !repeated.ShowMessage, "Selecting the same checkpoint does not save or show a message.");
        CheckpointSelection silent = checkpoints.Select("checkpoint", silent: true);
        assert(silent.SaveRequired && !silent.ShowMessage, "Silent checkpoint changes still save.");
        checkpoints.Select("re_start_death");
        assert(checkpoints.HasNonDeathRestart, "Death selection preserves the recovered non-death flag latch.");
        assert(Rejects(() => checkpoints.Select("caf\u00e9")), "Only ASCII uppercase letters fold in original restart names.");
        assert(checkpoints.Current is null, "Missing restart names clear the selection before the mission faults.");

        checkpoints = new MissionCheckpoints(file);
        var selections = new List<CheckpointSelection>();
        var runtime = new MissionRuntime(file, new MissionRuntimeTests.ActorFactory(),
            [new SetRestartCommandHandler(MissionOpcode.SetRestart, checkpoints, selections.Add),
             new SetRestartCommandHandler(MissionOpcode.SetRestartSilently, checkpoints, selections.Add)]);
        runtime.QueueAutoexec();
        assert(runtime.ExecutePending() == 1 && selections.Count == 4, "Original checkpoint commands execute through the mission interpreter.");
        assert(selections.Select(selection => (selection.SaveRequired, selection.ShowMessage)).SequenceEqual(
            new[] { (false, false), (true, true), (true, false), (false, false) }),
            "Checkpoint adapters receive original initial/change/silent/repeated save and notification semantics.");
        assert(checkpoints.Current?.NodeId == 3, "Mission command execution selects the final restart point.");
        var handler = new SetRestartCommandHandler(MissionOpcode.SetRestart, checkpoints, selections.Add);
        assert(Rejects(() => handler.Execute(new(file.Nodes[0], 0), new(140, 2, Encoding.Latin1.GetBytes("missing terminator")))),
            "Malformed checkpoint command strings are rejected before changing state.");
    }

    private static bool Rejects(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return true; }
        return false;
    }

    private static TriggerNode Restart(ushort id, string name)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true);
        writer.Write((ushort)8);
        writer.Write((ushort)0);
        writer.Write(new byte[18]);
        WriteString(writer, name);
        writer.Write(ushort.MaxValue);
        return new(id, 8, 0, stream.ToArray());
    }

    private static TriggerNode Autoexec()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true);
        writer.Write((ushort)4);
        foreach ((ushort opcode, string name) in new (ushort, string)[]
            { (140, "re_start"), (140, "Checkpoint"), (176, "Re_Start_Death"), (140, "RE_START_DEATH") })
        {
            writer.Write(opcode);
            WriteString(writer, name);
        }
        writer.Write(ushort.MaxValue);
        return new(0, 4, 0, stream.ToArray());
    }

    private static void WriteString(BinaryWriter writer, string name)
    {
        writer.Write(Encoding.Latin1.GetBytes(name));
        writer.Write((byte)0);
        if (writer.BaseStream.Position % 2 != 0) writer.Write((byte)0);
    }
}
