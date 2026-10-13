using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;

namespace OpenSpidey.Game.Actors;

/// <summary>Routes platform contacts to their genuine metadata and distinct damage override.</summary>
public sealed class GodotActorSessionPlatforms : IGodotActorSessionPlatformHost
{
    public ActorCollisionBody Snapshot(GodotPlatformActor actor) => actor.CollisionSnapshot;
    public bool Hit(GodotPlatformActor actor, ActorHit hit) => actor.Hit(hit);
}
