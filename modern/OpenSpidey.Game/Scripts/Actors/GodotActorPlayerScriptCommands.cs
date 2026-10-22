using OpenSpidey.Core.Actors.Scripting;

namespace OpenSpidey.Game.Actors;

internal static class GodotActorPlayerScriptCommands
{
    public static bool TryExecute(GodotActorRegistry registry, ushort opcode, ActorScriptCursor operands)
    {
        if (opcode == 0x42B9)
            return ActorCinematicScreenCommand.TryExecute(opcode, operands, registry.CinematicScreenHost ??
                throw new InvalidOperationException("The source cinematic screen globals have no process owner."));
        if (opcode is not (0x42BA or 0x470D or 0x470E)) return false;
        return ActorPlayerScriptCommands.TryExecute(opcode, operands, registry.PlayerScriptHost ??
            throw new InvalidOperationException("The source script-only player commands have no actual CPlayer service."));
    }
}
