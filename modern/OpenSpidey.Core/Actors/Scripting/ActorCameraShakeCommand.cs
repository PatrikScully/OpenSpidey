using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scripting;

public interface IActorCameraShakeHost
{
    bool CameraPresent { get; }
    void ShakeCamera(FixedVector3 position, int sourceMode);
}

/// <summary>Inherited CBaddy4298 resolves its operand even without a camera, then maps the source request to EShakeType.</summary>
public static class ActorCameraShakeCommand
{
    public static bool TryExecute(ushort opcode, ActorScriptCursor operands,
        Func<ushort, ActorScriptCursor, short> readVariable, FixedVector3 position, IActorCameraShakeHost host)
    {
        if (opcode != 0x4298) return false;
        ArgumentNullException.ThrowIfNull(operands); ArgumentNullException.ThrowIfNull(readVariable); ArgumentNullException.ThrowIfNull(host);
        ushort value = operands.ReadWord();
        if ((value & 0x2000) != 0) value = unchecked((ushort)readVariable(value, operands));
        if (host.CameraPresent) host.ShakeCamera(position, value switch { 0 => 2, 1 => 1, 2 => 0, _ => 3 });
        return true;
    }
}
