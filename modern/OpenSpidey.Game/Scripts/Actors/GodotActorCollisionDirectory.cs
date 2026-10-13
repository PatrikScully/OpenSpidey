using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Actors;

/// <summary>Live full BaddyList snapshots, distinct from the thug-only dialogue/coordinator view.</summary>
public static class GodotActorCollisionDirectory
{
    /// <summary>Directory list order already reflects registry suspension, resume and removal notifications.</summary>
    public static IReadOnlyList<ActorCollisionBody> SourceBaddyBodies(GodotPlayerMeleeDirectory directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        return directory.SourceBodies.Select(body=>Snapshot(body.Native)).ToArray();
    }
    public static ActorCollisionBody Snapshot(GodotScriptActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if(!GodotObject.IsInstanceValid(actor) || actor.IsQueuedForDeletion())
            throw new InvalidOperationException("Source collision list contains a deleted native actor.");
        return actor switch
        {
            GodotThugActor thug=>new(thug,thug.Type,thug.Position,unchecked((ushort)thug.State.MinorRadius),thug.State.BodyFlags,thug.State.Health),
            GodotHostageActor hostage=>new(hostage,hostage.Type,hostage.Position,hostage.State.MinorRadius,hostage.BodyFlags,hostage.State.Health),
            GodotBlackCatActor cat=>new(cat,319,cat.SourcePosition,cat.State.MinorRadius,cat.State.BodyFlags,0),
            // CManipObChunk has zero radius; preserve it even when the global ignore-radius query is active.
            GodotManipulationDebris debris=>new(debris,401,debris.Body.Position,debris.MinorRadius,debris.BodyFlags,debris.Variables.Health),
            _=>throw new NotSupportedException($"Source collision body family {actor.GetType().Name} has no proven metadata adapter.")
        };
    }
}
