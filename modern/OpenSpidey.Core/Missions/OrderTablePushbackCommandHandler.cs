using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Rendering;

namespace OpenSpidey.Core.Missions;

/// <summary>Actual166/169 signed render-global stores, independent of camera ownership.</summary>
public sealed class OrderTablePushbackCommandHandler : IMissionCommandHandler
{
    private readonly SourcePolygonDepthState state;
    public ushort Opcode { get; }
    public OrderTablePushbackCommandHandler(ushort opcode, SourcePolygonDepthState state)
    {
        if (opcode is not (166 or 169)) throw new ArgumentOutOfRangeException(nameof(opcode));
        Opcode = opcode; this.state = state ?? throw new ArgumentNullException(nameof(state));
    }
    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 2)
            throw new InvalidDataException($"Node{context.Node.Id} source OT command{Opcode} requires its exact signed word.");
        short value = BinaryPrimitives.ReadInt16LittleEndian(instruction.Arguments.Span);
        if (Opcode == 166) state.SetFirstPushback(value); else state.SetSecondPushback(value);
    }
    public static IEnumerable<IMissionCommandHandler> CreateHandlers(SourcePolygonDepthState state) =>
        new ushort[] { 166, 169 }.Select(opcode => new OrderTablePushbackCommandHandler(opcode, state));
}
