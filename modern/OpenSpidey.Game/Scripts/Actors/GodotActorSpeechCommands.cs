using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Game.Actors;

/// <summary>Actor XA commands use the scene's one original speech channel.</summary>
internal static class GodotActorSpeechCommands
{
    public static bool TryExecute(GodotActorRegistry registry, ushort opcode, ActorScriptCursor operands,
        ActorScriptState actor, ActorScriptEnvironment environment, FixedVector3 position) =>
        ActorSpeechCommands.TryExecute(opcode, operands, actor, environment, position, registry);
}
