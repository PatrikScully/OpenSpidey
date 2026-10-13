using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Missions;

/// <summary>Source215/216 write only the canonical current player's down/up bytes; no operands or immediate gain/audio update.</summary>
public sealed class FightMusicFadeCommandHandler : IMissionCommandHandler
{
    private readonly Func<PlayerCombatState> currentPlayer;
    public ushort Opcode { get; }
    public FightMusicFadeCommandHandler(ushort opcode, Func<PlayerCombatState> currentPlayer)
    {
        if (opcode is not (215 or 216)) throw new ArgumentOutOfRangeException(nameof(opcode));
        Opcode = opcode;
        this.currentPlayer = currentPlayer ?? throw new ArgumentNullException(nameof(currentPlayer));
    }
    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || !instruction.Arguments.IsEmpty)
            throw new InvalidDataException($"Node{context.Node.Id}: source{Opcode} has no fight-music operands.");
        PlayerCombatState player = currentPlayer() ?? throw new InvalidOperationException($"Source{Opcode} requires the actual current player.");
        if (Opcode == 215) { player.FightMusicFadeDown = 1; player.FightMusicFadeUp = 0; }
        else { player.FightMusicFadeUp = 1; player.FightMusicFadeDown = 0; }
    }
}
