using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>Original130 (0x4E23B0): CCamera::SetCamAngle(signed heading, unsigned duration) on the camera head, if any.</summary>
public sealed class CameraAngleCommandHandler(Action<short, ushort> setCameraAngle, Func<bool> cameraPresent) : IMissionCommandHandler
{
    private readonly Action<short, ushort> setCameraAngle = setCameraAngle ?? throw new ArgumentNullException(nameof(setCameraAngle));
    private readonly Func<bool> cameraPresent = cameraPresent ?? throw new ArgumentNullException(nameof(cameraPresent));
    public ushort Opcode => (ushort)MissionOpcode.SetCameraAngle;

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 4)
            throw new InvalidDataException($"Node {context.Node.Id}: source130 requires its original heading and duration words.");
        if (!cameraPresent()) return;
        ReadOnlySpan<byte> words = instruction.Arguments.Span;
        setCameraAngle(BinaryPrimitives.ReadInt16LittleEndian(words), BinaryPrimitives.ReadUInt16LittleEndian(words[2..]));
    }
}
