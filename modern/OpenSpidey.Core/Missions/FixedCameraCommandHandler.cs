using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Missions;

/// <summary>Chooses the first linked type-twelve camera node, preserving source link order.</summary>
public sealed class FixedCameraCommandHandler(TriggerFile file, ScriptedCameraRig camera,
    Func<FixedVector3> currentPosition, MissionOpcode opcode = MissionOpcode.SetFixedCameraPosition,
    Func<FixedQuaternion>? currentRotation = null, Func<bool>? cameraPresent = null) : IMissionCommandHandler
{
    public ushort Opcode => (ushort)opcode;

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        // Source185/186 consumes its duration and returns before reading links or native pose when the camera head is null.
        if (cameraPresent?.Invoke() == false) return;
        ushort duration = BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span);
        foreach (ushort id in MissionRuntime.ReadLinks(context.Node))
        {
            if (id >= file.Nodes.Count || file.Nodes[id].Id != id)
                throw new InvalidDataException($"Node {context.Node.Id}: fixed camera references missing node {id}.");
            TriggerNode linked = file.Nodes[id];
            if (linked.TypeCode != 12) continue;
            TriggerPlacement placement = TriggerPlacement.Read(linked);
            TriggerPosition position = placement.Position;
            FixedVector3 target = new FixedVector3(position.X, position.Y, position.Z) << 12;
            if (opcode == MissionOpcode.SetFixedCameraPosition) camera.SetFixedPosition(currentPosition(), target, duration);
            else if (opcode == MissionOpcode.SetFixedCameraPositionAndRotation)
            {
                if (currentRotation is null) throw new InvalidOperationException("Quaternion camera command needs the current view rotation.");
                if (linked.Data.Length - placement.AfterPositionOffset < 8)
                    throw new InvalidDataException($"Camera node {linked.Id}: quaternion extends beyond its record.");
                ReadOnlySpan<byte> bytes = linked.Data.Span[placement.AfterPositionOffset..];
                var rotation = new FixedQuaternion(BinaryPrimitives.ReadInt16LittleEndian(bytes),
                    BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]),
                    BinaryPrimitives.ReadInt16LittleEndian(bytes[6..]));
                camera.SetFixedPositionAndRotation(currentPosition(), target, currentRotation(), rotation, duration);
            }
            else throw new InvalidOperationException($"Unsupported fixed-camera opcode {opcode}.");
            return;
        }
        // The original consumes the duration but leaves the camera unchanged when no camera link exists.
    }

    public static IEnumerable<IMissionCommandHandler> CreateHandlers(TriggerFile file, ScriptedCameraRig camera,
        Func<FixedVector3> currentPosition, Func<FixedQuaternion> currentRotation, Func<bool>? cameraPresent = null) =>
        new MissionOpcode[] { MissionOpcode.SetFixedCameraPosition, MissionOpcode.SetFixedCameraPositionAndRotation }
            .Select(opcode => new FixedCameraCommandHandler(file, camera, currentPosition, opcode, currentRotation, cameraPresent));
}
