using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Game.Actors;

/// <summary>The inherited source shake command consumes operands against a real present-or-absent scene camera owner.</summary>
internal static class GodotActorCameraShakeCommands
{
    public static bool TryExecute(GodotActorRegistry registry, ushort opcode, ActorScriptCursor operands,
        Func<ushort, ActorScriptCursor, short> readVariable, FixedVector3 position)
    {
        if (opcode != 0x4298) return false;
        return ActorCameraShakeCommand.TryExecute(opcode, operands, readVariable, position, registry.CameraShake ??
            throw new InvalidOperationException("Source4298 requires the genuine current-camera presence/effect owner."));
    }
}
