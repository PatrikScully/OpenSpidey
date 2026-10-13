using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Missions;

/// <summary>Source183 consumes three words and updates the actual player's initial lookaround pitch globals; a missing player leaves them unchanged.</summary>
public sealed class PlayerLookaroundCameraValueCommandHandler(Func<PlayerLookaroundState> currentPlayer,
    Func<bool>? playerPresent = null) : IMissionCommandHandler
{
    private readonly Func<PlayerLookaroundState> currentPlayer = currentPlayer ?? throw new ArgumentNullException(nameof(currentPlayer));
    public ushort Opcode => 183;

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 6)
            throw new InvalidDataException($"Node{context.Node.Id} source183 requires its three original words.");
        if (playerPresent?.Invoke() == false) return;
        ReadOnlySpan<byte> words = instruction.Arguments.Span;
        currentPlayer().SetInitialCameraPitch(BinaryPrimitives.ReadUInt16LittleEndian(words),
            BinaryPrimitives.ReadUInt16LittleEndian(words[2..]), BinaryPrimitives.ReadInt16LittleEndian(words[4..]));
    }
}
