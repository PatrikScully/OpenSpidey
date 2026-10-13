using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Cameras;

namespace OpenSpidey.Core.Missions;

public sealed class CameraCommandHandler(ushort opcode, ScriptedCameraRig camera, Func<CameraPlayerState> player,
    Func<bool>? cameraPresent = null, Func<bool>? playerPresent = null) : IMissionCommandHandler
{
    public ushort Opcode => opcode;
    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span[offset..]);
        switch ((MissionOpcode)Opcode)
        {
            case MissionOpcode.SetCameraMode:
                if (cameraPresent?.Invoke() != false) camera.SetMode(Word(0));
                break;
            case MissionOpcode.SetCameraZoom:
                if (cameraPresent?.Invoke() != false) camera.SetZoom(Word(0), Word(2));
                break;
            case MissionOpcode.SetCameraAngleLock:
                if (playerPresent?.Invoke() != false) camera.SetAngleLock(Word(0));
                break;
            case MissionOpcode.SetWideScreenValue: camera.SetWideScreenScriptValue(Word(0)); break;
            case MissionOpcode.SetPlayerCameraValue:
                if (playerPresent?.Invoke() == false || cameraPresent?.Invoke() == false) break;
                camera.SetPlayerValue((CameraSurface)Word(0), (CameraParameter)Word(2),
                    unchecked((short)Word(4)), Word(6), Word(8) != 0, player());
                break;
            default: throw new InvalidOperationException($"Camera command {Opcode} has not been ported.");
        }
    }
    public static IEnumerable<IMissionCommandHandler> CreateHandlers(ScriptedCameraRig camera, Func<CameraPlayerState> player,
        Func<bool>? cameraPresent = null, Func<bool>? playerPresent = null) =>
        new MissionOpcode[] { MissionOpcode.SetCameraMode, MissionOpcode.SetCameraZoom, MissionOpcode.SetPlayerCameraValue,
            MissionOpcode.SetCameraAngleLock, MissionOpcode.SetWideScreenValue }
        .Select(opcode => new CameraCommandHandler((ushort)opcode, camera, player, cameraPresent, playerPresent));
}
