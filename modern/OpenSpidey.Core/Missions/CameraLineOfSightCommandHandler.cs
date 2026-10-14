using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Cameras;

namespace OpenSpidey.Core.Missions;

/// <summary>Original217 (0x4E0372) writes camera0xF9 = word != 0 through the camera head without a null check.</summary>
public sealed class CameraLineOfSightCommandHandler(Func<CameraModeState?> camera) : IMissionCommandHandler
{
    private readonly Func<CameraModeState?> camera = camera ?? throw new ArgumentNullException(nameof(camera));
    public ushort Opcode => (ushort)MissionOpcode.AllowCameraLineOfSightCheck;

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 2)
            throw new InvalidDataException($"Node {context.Node.Id}: source217 requires its original one-word operand.");
        CameraModeState state = camera() ?? throw new InvalidOperationException(
            $"Node {context.Node.Id}: source217 dereferences the camera head; no live camera exists.");
        state.ItemHidingEnabled = (byte)(BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span) != 0 ? 1 : 0);
    }
}
