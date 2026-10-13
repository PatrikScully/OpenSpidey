using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Game.Actors;

/// <summary>Inherited source laser/explosion commands share the scene's actual Glow/Bullet effect owners.</summary>
internal static class GodotActorVisualEffectCommands
{
    public static bool TryExecute(GodotActorRegistry registry, ushort opcode, ActorScriptCursor operands, FixedVector3 position)
    {
        if (opcode is not (0x42A6 or 0x42A7 or 0x42A8)) return false;
        return ActorVisualEffectCommands.TryExecute(opcode, operands, position, registry.ActorVisualEffects ??
            throw new InvalidOperationException("Original actor visual effects require the actual source Glow/Bullet owners."));
    }
}
