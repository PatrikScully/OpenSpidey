using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

public interface IMissionCheckpointMessages
{
    void DeleteAllMessages();
    void ShowCheckpointMessage();
}

/// <summary>The source save game's restart-name field; checkpoint selection does not write disk or snapshot player health.</summary>
public sealed class MissionCheckpointSave
{
    public string RestartPointName { get; private set; }

    public MissionCheckpointSave(string restartPointName = "")
    {
        Validate(restartPointName);
        RestartPointName = restartPointName;
    }

    internal void WriteRestartName(string name)
    {
        Validate(name);
        RestartPointName = name;
    }

    private static void Validate(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length >= 50 || name.Any(character => character == 0 || character > 255))
            throw new InvalidDataException("A checkpoint save name must fit the original fifty-byte Latin1 field including its terminator.");
    }
}

/// <summary>Owns mission checkpoints and original140/176 notification/save-name effects across scene integration.</summary>
public sealed class MissionCheckpointSession
{
    private readonly IMissionCheckpointMessages messages;
    public MissionCheckpoints Checkpoints { get; }
    public MissionCheckpointSave Save { get; }
    public RestartPoint? Current => Checkpoints.Current;

    public MissionCheckpointSession(TriggerFile file, MissionCheckpointSave save, IMissionCheckpointMessages messages)
    {
        ArgumentNullException.ThrowIfNull(file);
        Checkpoints = new(file);
        Save = save ?? throw new ArgumentNullException(nameof(save));
        this.messages = messages ?? throw new ArgumentNullException(nameof(messages));
    }

    public CheckpointSelection Select(string name, bool silent = false)
    {
        CheckpointSelection selection = Checkpoints.Select(name, silent);
        ApplySelection(selection);
        return selection;
    }

    public IEnumerable<IMissionCommandHandler> CreateHandlers() =>
        new[] { MissionOpcode.SetRestart, MissionOpcode.SetRestartSilently }.Select(opcode =>
            new SetRestartCommandHandler(opcode, Checkpoints, ApplySelection));

    /// <summary>Front_LoadGame copies the final selected node name after executing its restart, even for the initial death node.</summary>
    public void SaveCurrentRestart()
    {
        RestartPoint current = Current ?? throw new InvalidOperationException("No authored restart has been selected.");
        Save.WriteRestartName(current.Name);
    }

    private void ApplySelection(CheckpointSelection selection)
    {
        if (selection.ShowMessage)
        {
            messages.DeleteAllMessages();
            messages.ShowCheckpointMessage();
        }
        if (selection.SaveRequired)
            // Front_SaveGameState copies the selected node's original name, preserving its case.
            Save.WriteRestartName(selection.Point.Name);
    }
}
