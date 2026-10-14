using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Gameplay;

/// <summary>FindBaddyOfType (0x403F90) over the canonical BaddyList order.</summary>
internal sealed class GodotMissionBaddies(GodotActorRegistry registry) : IMissionBaddyLookup
{
    public object? FindBaddyOfType(ushort type)
    {
        SourceActorList baddies = registry.SourceList(ActorUpdateGroup.Enemy);
        for (object? body = baddies.Head; body is not null; body = baddies.ReadNext(body))
        {
            GodotScriptActor actor = body as GodotScriptActor ?? throw new NotSupportedException(
                "Source FindBaddyOfType requires canonical CBody owners in BaddyList.");
            if (unchecked((ushort)actor.SourceActorType) == type) return actor;
        }
        return null;
    }
}
