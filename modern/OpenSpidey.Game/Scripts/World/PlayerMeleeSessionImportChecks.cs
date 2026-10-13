using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Assets.Models;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal static class PlayerMeleeSessionImportChecks
{
    public static async Task VerifyAsync(Node3D owner, GodotActorRegistry sourceRegistry, ImportedLevelWorld world, PsxLibraryRepository repository,
        GodotPlayer player, FixedVector3 verifiedRoofPosition)
    {
        var actors = new GodotActorRegistry { Name = "MeleeEnvironmentVerification", Triggers = sourceRegistry.Triggers,
            Player = player, Difficulty = sourceRegistry.Difficulty, ObjectLibrary = sourceRegistry.ObjectLibrary,
            ObjectTextures = sourceRegistry.ObjectTextures, ClaimObjectModel = sourceRegistry.ClaimObjectModel,
            Environment = sourceRegistry.Environment, Sounds = sourceRegistry.Sounds, SoundListener = sourceRegistry.SoundListener,
            SuspensionDistance = uint.MaxValue };
        owner.AddChild(actors);
        actors.SetPhysicsProcess(false);
        try
        {
            using var session = new GodotPlayerMeleeSession(actors, player, () => world);
            bool originalLookaround = player.Combat.State.LookaroundActive;
            try
            {
                player.Combat.State.LookaroundActive = true;
                if (session.SurfaceTransition != 1) throw new InvalidDataException("Melee lookaround gate lost source player ownership.");
                player.Combat.State.LookaroundActive = false;
                if (session.SurfaceTransition != 0 || session.Difficulty != actors.Difficulty)
                    throw new InvalidDataException("Melee lookaround/difficulty snapshot is not the actual session state.");
            }
            finally { player.Combat.State.LookaroundActive = originalLookaround; }

            GodotPlatformActor? captured = null;
            actors.Spawned += actor => captured = actor as GodotPlatformActor;
            actors.Spawn(actors.Triggers.Nodes[170]);
            GodotPlatformActor first = captured ?? throw new InvalidDataException("The genuine pressure platform did not spawn.");
            actors.Spawn(actors.Triggers.Nodes[55]);
            GodotPlatformActor second = captured ?? throw new InvalidDataException("The genuine moving mechanism did not spawn.");
            if (session.SourceEnvironmentalOrder.Count != 2 ||
                !ReferenceEquals(session.SourceEnvironmentalOrder[0].Identity, second) ||
                !ReferenceEquals(session.SourceEnvironmentalOrder[1].Identity, first))
                throw new InvalidDataException("Melee environmental order differs from source list prepend order.");
            var handle = session.MakeEnvironmentalHandle(first.KickObject);
            uint validation = handle.Validation;
            GodotScriptActor[] sourceOrder = actors.SourceActors(ActorUpdateGroup.Environment).ToArray();
            var reparent = new Node3D { Name = "EnvironmentalReparentProof" };
            owner.AddChild(reparent);
            actors.RemoveChild(first);
            reparent.AddChild(first);
            if (!ReferenceEquals(session.RecoverEnvironmentalHandle(ref handle), first) || handle.Validation != validation ||
                !actors.SourceActors(ActorUpdateGroup.Environment).SequenceEqual(sourceOrder))
                throw new InvalidDataException("Reparenting a live environmental body must preserve its handle and original source-list identity.");
            reparent.RemoveChild(first);
            actors.AddChild(first);
            reparent.Free();
            actors.SuspendActiveActors(first.Definition.NodeId);
            if (session.SourceEnvironmentalOrder.Count != 1 || !ReferenceEquals(session.RecoverEnvironmentalHandle(ref handle), first))
                throw new InvalidDataException("Suspension must remove an environment item from selection while preserving its live handle.");
            actors._PhysicsProcess(1.0 / 60);
            actors._PhysicsProcess(1.0 / 60);
            if (session.SourceEnvironmentalOrder.Count != 2 || !ReferenceEquals(session.SourceEnvironmentalOrder[0].Identity, first))
                throw new InvalidDataException("Nearby environment resume did not prepend to the source list.");

            first.Advance();
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            ImportedCollisionBody[] bodies = first.GetNode<Node3D>("WorldCollision").GetChildren().OfType<ImportedCollisionBody>().ToArray();
            if (bodies.Length == 0) throw new InvalidDataException("The original pressure mechanism has no native collision ownership.");
            foreach (ImportedCollisionBody body in bodies)
            {
                var hit = new GodotActorWorldHit(body, body.SourceTriangles[0], first.Motion.Position,
                    body.SupportId, body.SourceAsset, body.InstanceIndex, body.ModelIndex, body.ModelChecksum);
                if (!ReferenceEquals(session.ResolveWorldContact(hit), first))
                    throw new InvalidDataException("A mechanism face lost its actual source actor identity.");
            }

            // Earlier mechanism checks legitimately claim models from the main world. Read a fresh,
            // isolated original world to verify static item identities without changing those owners.
            var viewport = new SubViewport { Size = new(640, 480), OwnWorld3D = true };
            owner.AddChild(viewport);
            try
            {
                string geometry = world.Sources.Single(source => source.AssetPath.EndsWith("_G.psx", StringComparison.OrdinalIgnoreCase)).AssetPath;
                ImportedLevelWorld originalWorld = ImportedLevelWorld.Load(repository, geometry);
                viewport.AddChild(originalWorld);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                using var staticSession = new GodotPlayerMeleeSession(actors, player, () => originalWorld);
                var worldQuery = new GodotActorWorldQuery(originalWorld, actors.Triggers);
                FixedVector3 start = verifiedRoofPosition with { Y = unchecked(verifiedRoofPosition.Y - (128 << 12)) };
                FixedVector3 end = verifiedRoofPosition with { Y = unchecked(verifiedRoofPosition.Y + (5000 << 12)) };
                GodotActorWorldHit roof = worldQuery.Trace(start, end)
                    ?? throw new InvalidDataException("The genuine first roof did not produce a native targeting ray contact.");
                object identity = staticSession.ResolveWorldContact(roof);
                GodotActorWorldHit repeated = worldQuery.Trace(start, end)!;
                if (identity is not GodotPlayerMeleeSession.WorldItem item || item.SourceAsset != roof.SourceAsset ||
                    item.InstanceIndex != roof.InstanceIndex || !ReferenceEquals(identity, staticSession.ResolveWorldContact(repeated)))
                    throw new InvalidDataException("Repeated source roof queries did not retain canonical original item identity.");
            }
            finally { viewport.Free(); }

            first.Lifecycle.Kill();
            for (int tick = 0; tick < 4; tick++) actors._PhysicsProcess(1.0 / 60);
            if (session.RecoverEnvironmentalHandle(ref handle) is not null || handle.Target is not null || handle.Validation != validation)
                throw new InvalidDataException("Deleted environment recovery must clear only the captured pointer and retain its validation word.");
            GD.Print("Verified live melee environment ownership: original list order, suspension/resume, weak handle deletion, mechanism faces, static roof identity and player lookaround gate.");
        }
        finally { actors.Free(); }
    }
}
