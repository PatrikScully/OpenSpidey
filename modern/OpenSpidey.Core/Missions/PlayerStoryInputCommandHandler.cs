using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Player.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>Actual222 normalizes the allow byte;223 writes the complete unsigned word to player34C.</summary>
public sealed class PlayerStoryInputCommandHandler : IMissionCommandHandler
{
    private readonly PlayerCutsceneSkipState state;
    private readonly Action<ushort> setFootstepBank;
    public ushort Opcode { get; }

    private PlayerStoryInputCommandHandler(ushort opcode, PlayerCutsceneSkipState state, Action<ushort> setFootstepBank)
    {
        Opcode = opcode; this.state = state; this.setFootstepBank = setFootstepBank;
    }
    public static IReadOnlyList<IMissionCommandHandler> CreateHandlers(PlayerCutsceneSkipState state,
        Action<ushort> setFootstepBank)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(setFootstepBank);
        return [new PlayerStoryInputCommandHandler(222, state, setFootstepBank),
            new PlayerStoryInputCommandHandler(223, state, setFootstepBank)];
    }
    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 2)
            throw new InvalidDataException($"Node{context.Node.Id} source{Opcode} requires one unsigned word.");
        ushort value = BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span);
        if (Opcode == 222) state.SetAllowed(value);
        else setFootstepBank(value);
    }
}
