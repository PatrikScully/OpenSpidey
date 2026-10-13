using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Missions;

/// <summary>Original11 node links and133 absolutely aligned whole-unit boxes.</summary>
public sealed class MissionActorDeathCommandHandler : IMissionCommandHandler
{
    private readonly TriggerFile triggers;
    private readonly IMissionActorDeathHost host;
    public ushort Opcode { get; }
    public MissionActorDeathCommandHandler(ushort opcode, TriggerFile triggers, IMissionActorDeathHost host)
    {
        if (opcode is not (11 or 133)) throw new ArgumentOutOfRangeException(nameof(opcode));
        Opcode = opcode; this.triggers = triggers ?? throw new ArgumentNullException(nameof(triggers));
        this.host = host ?? throw new ArgumentNullException(nameof(host));
    }
    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode) throw new ArgumentException("Source death command does not match its handler.", nameof(instruction));
        if (Opcode == 11)
        {
            if (instruction.Arguments.Length != 0) throw new InvalidDataException("Source SendKill has no operands.");
            MissionActorDeath.SendKill(triggers, context.Node, host); return;
        }
        ReadOnlySpan<byte> bytes = instruction.Arguments.Span;
        int cursor = 0;
        while (true)
        {
            if (cursor > bytes.Length - 2) throw new InvalidDataException("Source133 boxes have no terminating255 word.");
            if (BinaryPrimitives.ReadUInt16LittleEndian(bytes[cursor..]) == 255)
            {
                if (cursor + 2 != bytes.Length) throw new InvalidDataException("Source133 has data after its sentinel.");
                return;
            }
            cursor = checked(cursor + ((4 - ((instruction.SourceOffset + 2 + cursor) & 3)) & 3));
            if (cursor > bytes.Length - 24) throw new InvalidDataException("Source133 aligned box extends beyond its record.");
            MissionActorDeath.KillEverythingInBox(ReadPosition(bytes[cursor..]), ReadPosition(bytes[(cursor + 12)..]), host);
            cursor += 24;
        }
    }
    private static FixedVector3 ReadPosition(ReadOnlySpan<byte> bytes) => new(
        unchecked(BinaryPrimitives.ReadInt32LittleEndian(bytes) << 12), unchecked(BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]) << 12),
        unchecked(BinaryPrimitives.ReadInt32LittleEndian(bytes[8..]) << 12));
    public static IEnumerable<IMissionCommandHandler> CreateHandlers(TriggerFile triggers, IMissionActorDeathHost host) =>
        new ushort[] { 11, 133 }.Select(opcode => new MissionActorDeathCommandHandler(opcode, triggers, host));
}
