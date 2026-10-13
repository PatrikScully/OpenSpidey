using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.UI;

namespace OpenSpidey.Core.Missions;

public interface IMissionTextBoxHost
{
    uint LastSimpleMessageX { get; }
    int LastSimpleMessageWidth { get; }
    void CreateTextBox(MissionTextBoxDefinition definition);
    void ClearTextBoxes();
}

/// <summary>Original208 rectangle derives its width from the last genuine simple-message/font measurement;219 clears only boxes.</summary>
public sealed class MissionTextBoxCommandHandler : IMissionCommandHandler
{
    private readonly IMissionTextBoxHost host;
    public ushort Opcode { get; }

    public MissionTextBoxCommandHandler(ushort opcode, IMissionTextBoxHost host)
    {
        if (opcode is not (208 or 219)) throw new ArgumentOutOfRangeException(nameof(opcode));
        Opcode = opcode;
        this.host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public static IEnumerable<IMissionCommandHandler> CreateHandlers(IMissionTextBoxHost host) =>
        new ushort[] { 208, 219 }.Select(opcode => new MissionTextBoxCommandHandler(opcode, host));

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != (Opcode == 208 ? 16 : 0))
            throw new InvalidDataException($"Node {context.Node.Id}: original{Opcode} has invalid text-box operands.");
        if (Opcode == 219) { host.ClearTextBoxes(); return; }
        ReadOnlySpan<byte> data = instruction.Arguments.Span;
        int x = BinaryPrimitives.ReadInt16LittleEndian(data);
        int offset = unchecked((int)host.LastSimpleMessageX - x);
        if (offset < 0) offset = unchecked(-offset);
        host.CreateTextBox(new(unchecked(x - 2), BinaryPrimitives.ReadInt16LittleEndian(data[2..]),
            unchecked(host.LastSimpleMessageWidth + offset * 2 - 2), BinaryPrimitives.ReadInt16LittleEndian(data[6..]) - 2,
            unchecked((ushort)BinaryPrimitives.ReadInt16LittleEndian(data[8..])), Dim(data[10]), Dim(data[12]), Dim(data[14])));
        // Operand2 is unused in the original executable; its authored width is never substituted for the measured font width.
    }

    private static byte Dim(byte channel) => unchecked((byte)(int)(channel * (double)0.4f));
}
