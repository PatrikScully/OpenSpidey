using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>The existing bounded NPC contact checks do not execute platform damage or recoil.</summary>
internal sealed class ActorSessionCheckPlatforms : IGodotActorSessionPlatformHost
{
    public ActorCollisionBody Snapshot(GodotPlatformActor actor) =>
        throw new NotSupportedException("NPC contact verification unexpectedly requested an unconfigured original platform metadata leaf.");
    public bool Hit(GodotPlatformActor actor, ActorHit hit) =>
        throw new NotSupportedException("NPC contact verification unexpectedly requested an unconfigured original CPlatform::Hit leaf.");
}
