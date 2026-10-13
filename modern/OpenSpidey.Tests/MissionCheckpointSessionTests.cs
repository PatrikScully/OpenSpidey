using System.Text;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;

internal static class MissionCheckpointSessionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(MissionCheckpointSessionTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.mission-checkpoint.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        string[] names = document.RootElement.GetProperty("names").EnumerateArray().Select(value => value.GetString()!).ToArray();
        var nodes = new List<TriggerNode> { new(0, 4, 0, new byte[] { 4, 0, 255, 255 }) };
        nodes.AddRange(names.Select((name, index) => Restart((ushort)(index + 1), name)));
        var file = new TriggerFile(nodes.AsReadOnly());
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var messages = new Messages();
            var save = new MissionCheckpointSave("initial");
            var session = new MissionCheckpointSession(file, save, messages);
            messages.Session = session;
            var handlers = session.CreateHandlers().ToDictionary(handler => handler.Opcode);
            JsonElement[] expected = test.GetProperty("expected").EnumerateArray().ToArray();
            JsonElement[] commands = test.GetProperty("commands").EnumerateArray().ToArray();
            for (int step = 0; step < commands.Length; step++)
            {
                ushort opcode = commands[step][0].GetUInt16();
                string commandName = commands[step][1].GetString()!;
                string beforeSave = save.RestartPointName;
                messages.Events.Clear(); messages.SaveNames.Clear(); messages.SelectedNodes.Clear();
                handlers[opcode].Execute(new(nodes[0], 0), new(opcode, 0, Encoding.Latin1.GetBytes(commandName + '\0')));
                string saved = Encoding.Latin1.GetString(expected[step][2].EnumerateArray().Select(value => value.GetByte()).ToArray());
                int[] events = expected[step][3].EnumerateArray().Select(value => value.GetInt32()).ToArray();
                assert(session.Current?.NodeId == expected[step][0].GetUInt16() &&
                    session.Checkpoints.HasNonDeathRestart == (expected[step][1].GetInt32() != 0) && save.RestartPointName == saved,
                    "Checkpoint140/176 preserves actual PC first-duplicate/ASCII-only selection, original node-name save copy, repeated selection and non-death flag latch.");
                assert(messages.Events.SequenceEqual(events),
                    "Changed audible checkpoints clear then show the source message; initial, repeated and silent selections follow actual PC gates.");
                assert(messages.SaveNames.All(name => name == beforeSave) &&
                    messages.SelectedNodes.All(node => node == session.Current!.NodeId),
                    "Checkpoint notification sees the new current source node and the previous saved name; Front_SaveGameState updates the name afterward.");
            }
            var nextSession = new MissionCheckpointSession(file, save, new Messages());
            assert(ReferenceEquals(nextSession.Save, save) && nextSession.Save.RestartPointName == session.Save.RestartPointName && nextSession.Current is null,
                "Application-owned restart save state survives a new mission session while its source node selection starts uninitialized.");
        }
        bool rejected = false;
        try { _ = new MissionCheckpointSave(new string('x', 50)); }
        catch (InvalidDataException) { rejected = true; }
        assert(rejected, "Modern checkpoint save ownership bounds the original fifty-byte name field before an overwrite.");
        var startupMessages = new Messages();
        var startupSave = new MissionCheckpointSave();
        var startupSession = new MissionCheckpointSession(file, startupSave, startupMessages);
        startupMessages.Session = startupSession;
        startupSession.Select(names[0]);
        assert(startupSave.RestartPointName.Length == 0 && startupMessages.Events.Count == 0,
            "Initial source autoexec selection preserves an empty save name and emits no checkpoint notification.");
        startupSession.SaveCurrentRestart();
        assert(startupSave.RestartPointName == startupSession.Current!.Name && startupMessages.Events.Count == 0,
            "Front_LoadGame copies the authored initial restart name after execution without showing a changed-checkpoint message.");
    }

    private static TriggerNode Restart(ushort id, string name)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true);
        writer.Write((ushort)8); writer.Write((ushort)0); writer.Write(new byte[18]);
        writer.Write(Encoding.Latin1.GetBytes(name)); writer.Write((byte)0);
        if (stream.Position % 2 != 0) writer.Write((byte)0);
        writer.Write(ushort.MaxValue);
        return new(id, 8, 0, stream.ToArray());
    }

    private sealed class Messages : IMissionCheckpointMessages
    {
        public MissionCheckpointSession Session { get; set; } = null!;
        public List<int> Events { get; } = [];
        public List<string> SaveNames { get; } = [];
        public List<ushort> SelectedNodes { get; } = [];
        public void DeleteAllMessages() => Record(1);
        public void ShowCheckpointMessage() => Record(2);
        private void Record(int operation)
        {
            Events.Add(operation); SaveNames.Add(Session.Save.RestartPointName); SelectedNodes.Add(Session.Current!.NodeId);
        }
    }
}
