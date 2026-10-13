using System.Buffers.Binary;

namespace OpenSpidey.Assets.Scripting;

public enum PlayerScriptOpcode : short
{
    Teleport = 1, WalkToTarget = 2, HoldInput = 3, PlayVoice = 4, MoveToTarget = 5,
    HoldAnimation = 6, StandAfterDelay = 7, HoldActionValue = 8, Wait = 9,
    WaitForAnimationSignal = 15, SetCheckpoint = 16, HoldInteraction = 17,
    SetScriptFlag = 18, Stop = 255
}

public readonly record struct PlayerScriptInstruction(short DueTime, PlayerScriptOpcode Opcode,
    short First, short Second, short Third, int SourceOffset);

/// <summary>Bounded decoding of the signed-word player input stream embedded in mission command 199.</summary>
public sealed class PlayerScriptProgram
{
    public IReadOnlyList<PlayerScriptInstruction> Instructions { get; }

    private PlayerScriptProgram(List<PlayerScriptInstruction> instructions) => Instructions = instructions.AsReadOnly();

    public static PlayerScriptProgram Read(ReadOnlySpan<byte> data, int sourceOffset = 0)
    {
        if (data.Length % 2 != 0) throw new InvalidDataException("Player script has a partial word.");
        var instructions = new List<PlayerScriptInstruction>();
        int cursor = 0;
        while (true)
        {
            int start = cursor;
            short due = Word(data, ref cursor, sourceOffset);
            var opcode = (PlayerScriptOpcode)Word(data, ref cursor, sourceOffset);
            int count = OperandCount(opcode, checked(sourceOffset + start));
            short first = count > 0 ? Word(data, ref cursor, sourceOffset) : (short)0;
            short second = count > 1 ? Word(data, ref cursor, sourceOffset) : (short)0;
            short third = count > 2 ? Word(data, ref cursor, sourceOffset) : (short)0;
            instructions.Add(new(due, opcode, first, second, third, checked(sourceOffset + start)));
            if (opcode != PlayerScriptOpcode.Stop) continue;
            if (cursor != data.Length)
                throw new InvalidDataException($"Player script at 0x{sourceOffset + cursor:X} has data after Stop.");
            return new(instructions);
        }
    }

    private static int OperandCount(PlayerScriptOpcode opcode, int offset) => opcode switch
    {
        PlayerScriptOpcode.Teleport or PlayerScriptOpcode.WalkToTarget or PlayerScriptOpcode.StandAfterDelay or
            PlayerScriptOpcode.Wait or PlayerScriptOpcode.WaitForAnimationSignal or PlayerScriptOpcode.SetCheckpoint or
            PlayerScriptOpcode.SetScriptFlag => 1,
        PlayerScriptOpcode.HoldInput or PlayerScriptOpcode.MoveToTarget or PlayerScriptOpcode.HoldAnimation or
            PlayerScriptOpcode.HoldActionValue => 2,
        PlayerScriptOpcode.PlayVoice => 3,
        PlayerScriptOpcode.HoldInteraction or PlayerScriptOpcode.Stop => 0,
        _ => throw new InvalidDataException($"Player script at 0x{offset:X}: unknown opcode {(short)opcode}.")
    };

    private static short Word(ReadOnlySpan<byte> data, ref int cursor, int offset)
    {
        if (data.Length - cursor < 2)
            throw new InvalidDataException($"Player script at 0x{offset + cursor:X} ends before its instruction or Stop.");
        short word = BinaryPrimitives.ReadInt16LittleEndian(data[cursor..]);
        cursor += 2;
        return word;
    }
}
