using System.Text;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>Ports SetRestart and its silent variant; persistence/UI are supplied by the game adapter.</summary>
public sealed class SetRestartCommandHandler : IMissionCommandHandler
{
    private readonly MissionCheckpoints checkpoints;
    private readonly Action<CheckpointSelection> onSelected;
    public ushort Opcode { get; }

    public SetRestartCommandHandler(MissionOpcode opcode, MissionCheckpoints checkpoints,
        Action<CheckpointSelection> onSelected)
    {
        if (opcode is not (MissionOpcode.SetRestart or MissionOpcode.SetRestartSilently))
            throw new ArgumentOutOfRangeException(nameof(opcode));
        Opcode = (ushort)opcode;
        this.checkpoints = checkpoints;
        this.onSelected = onSelected;
    }

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        int length = instruction.Arguments.Span.IndexOf((byte)0);
        if (length < 0)
            throw new InvalidDataException($"Node {context.Node.Id}: Unterminated restart command name.");
        string name = Encoding.Latin1.GetString(instruction.Arguments.Span[..length]);
        CheckpointSelection selection = checkpoints.Select(name, Opcode == (ushort)MissionOpcode.SetRestartSilently);
        onSelected(selection);
    }
}
