using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Game.Actors;

internal static class GodotActorSparkCommands
{
    public static bool TryExecute(GodotActorRegistry registry, ushort opcode, ActorScriptCursor operands,
        FixedVector3 position, ActorAngles angles)
    {
        if (opcode != 0x4292) return false;
        return ActorSparkCommand.TryExecute(opcode, operands, position, angles, registry.Sparks ??
            throw new InvalidOperationException("Source4292 requires the actual shared spark and PixelList owners."));
    }
}
