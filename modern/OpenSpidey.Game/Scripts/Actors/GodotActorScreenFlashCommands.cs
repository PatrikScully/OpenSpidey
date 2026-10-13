using OpenSpidey.Core.Actors.Scripting;

namespace OpenSpidey.Game.Actors;

/// <summary>The inherited CBaddy flash command uses the actual scene's one global flash owner.</summary>
internal static class GodotActorScreenFlashCommands
{
    public static bool TryExecute(GodotActorRegistry registry, ushort opcode, ActorScriptCursor operands)
    {
        if (opcode != 0x429C) return false;
        return ActorScreenFlashCommand.TryExecute(opcode, operands, registry.ScreenFlash ??
            throw new InvalidOperationException("Source429C requires the actual scene screen-flash owner."));
    }
}
