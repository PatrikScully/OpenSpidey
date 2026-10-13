using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Missions;

/// <summary>Source152 KillBruce checks current player presence, then requests the distinct scripted death path.</summary>
public sealed class PlayerScriptDeathCommandHandler(Func<IPlayerScriptDeathHost> currentPlayer,
    Func<bool>? playerPresent = null) : IMissionCommandHandler
{
    private readonly Func<IPlayerScriptDeathHost> currentPlayer = currentPlayer ?? throw new ArgumentNullException(nameof(currentPlayer));
    public ushort Opcode => 152;
    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || !instruction.Arguments.IsEmpty)
            throw new InvalidDataException($"Node{context.Node.Id}: source152 has no operands.");
        if (playerPresent?.Invoke() == false) return;
        PlayerScriptDeathRequest.Request(currentPlayer());
    }
}
