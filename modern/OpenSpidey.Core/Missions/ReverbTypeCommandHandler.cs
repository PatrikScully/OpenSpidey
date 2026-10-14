using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>Source reverb byte0x61919E. Its only PC reader, SFX_Unpause, passes it to the no-op mixer shim0x505B90.</summary>
public sealed class SourceReverbState
{
    public bool Enabled { get; private set; }
    /// <summary>SFX_SetReverbType (0x472470) keeps only the nonzero test of its byte.</summary>
    public void SetType(byte type) => Enabled = type != 0;
    /// <summary>SFX_ReverbOff (0x472480), run by level teardown.</summary>
    public void Off() => Enabled = false;
}

/// <summary>Original157 (0x4E13DF) reads only the low byte of its word operand.</summary>
public sealed class ReverbTypeCommandHandler(SourceReverbState state) : IMissionCommandHandler
{
    private readonly SourceReverbState state = state ?? throw new ArgumentNullException(nameof(state));
    public ushort Opcode => (ushort)MissionOpcode.SetReverbType;

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 2)
            throw new InvalidDataException($"Node {context.Node.Id}: source157 requires its original one-word operand.");
        state.SetType(instruction.Arguments.Span[0]);
    }
}
