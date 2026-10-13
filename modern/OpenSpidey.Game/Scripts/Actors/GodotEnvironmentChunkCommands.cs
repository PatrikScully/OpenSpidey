using OpenSpidey.Core.Actors.Scripting;

namespace OpenSpidey.Game.Actors;

/// <summary>Required inherited CBaddy42B5 boundary; unrelated commands never resolve the scene chunk service.</summary>
internal static class GodotEnvironmentChunkCommands
{
    public static bool TryExecute(GodotActorRegistry registry, ushort opcode, ActorScriptCursor operands)
    {
        if (opcode != 0x42B5) return false;
        return ActorEnvironmentChunkCommand.TryExecute(opcode, operands, registry.EnvironmentChunks ??
            throw new InvalidOperationException("Source42B5 requires the actual scene ENV chunk producer."));
    }
}
