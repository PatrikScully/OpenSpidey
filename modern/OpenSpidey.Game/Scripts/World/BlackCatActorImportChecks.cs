using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>Genuine source L1A1 Black Cat records, real model/ground/script output and native constructor lifetime.</summary>
internal static class BlackCatActorImportChecks
{
    public static async Task VerifyAsync(Node3D owner, GodotActorRegistry registry, GodotBlackCatConfiguration configuration,
        GodotThugConfiguration thugs, Action advanceSourceClock)
    {
        if (registry.Environment.LevelId != 0x101 || registry.Player is null)
            throw new InvalidOperationException("Black Cat native proof requires the actual L1A1 world and scene player.");
        var fixture = new Node3D { Name = "OriginalBlackCatProof", TopLevel = true };
        owner.AddChild(fixture);
        fixture.GlobalTransform = Transform3D.Identity;
        int initialCount = registry.BaddyConstruction.State.Count;
        var actors = new List<GodotBlackCatActor>();
        int assertions = 0;
        try
        {
            foreach (ushort nodeId in new ushort[] { 100, 113, 121, 138 })
            {
                TriggerNode node = registry.Triggers.Nodes[nodeId];
                TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
                Assert(definition.ActorType == 319 && definition.Markers.SequenceEqual(new byte[] { 0, 2, 4 }) &&
                    !definition.InitializeOnLoad && !definition.StartsSuspended && definition.Links.Count == 0,
                    $"Untouched L1A1 node{nodeId} is a pulse-created active source319 actor with marker4.");
                int ordinal = registry.BaddyConstruction.State.Count;
                GodotBlackCatActor actor = registry.BaddyConstruction.Create(319, () => GodotBlackCatActor.Create(node, registry, configuration));
                actors.Add(actor); fixture.AddChild(actor); actor.Begin();
                Assert(actor.State.SourceConstructionOrdinal == ordinal && actor.State.ActorPhase == unchecked((byte)ordinal) &&
                    actor.State.ModelName == "blackcat" && actor.State.Stage == 1 && actor.State.Substate == 0 &&
                    actor.State.SurfaceClearance == 100 && actor.State.MinorRadius == 0 && actor.State.BodyFlags == 0x214 &&
                    actor.ItemFlags == 0x482 && actor.Model.ClipId == 12 && !actor.Model.AdvanceAutomatically &&
                    actor.Model.SourcePartCount >= 18 && actor.CinematicScript.Cursor == 0 && !actor.CinematicScript.Running,
                    $"Node{nodeId} imports the genuine constructor, base count, initial clip12 and cinematic stream without ordinary4100 parsing.");
                Assert(!actor.Hit(new ActorHit { Damage = ushort.MaxValue }) && actor.State.BodyFlags == 0x214,
                    "The original Black Cat Hit virtual always returns0 and cannot damage or mutate its body.");
                actor.SynchronizeSourceLighting(); actor.Lighting.VerifyUploads();
                Assert(actor.SourceData.Light.AmbientRed == 1760 && actor.SourceData.Light.AmbientGreen == 1600 &&
                    actor.SourceData.Light.AmbientBlue == 1600 && actor.Lighting.LitPartCount == 9,
                    "Actual Black Cat SLight data lights exactly the nine source parts with vertex-plus-face normal coverage and preserves baked colours on the remaining parts.");
                ushort originalFlags = actor.ItemFlags;
                actor.ItemFlags &= 0xFF7F; actor.SynchronizeSourceLighting(); actor.Lighting.VerifyUploads();
                Assert(actor.Lighting.LitPartCount == 0,
                    "Removing source item80 restores the actual imported baked surface RGB/alpha without changing model geometry or pose.");
                actor.ItemFlags = originalFlags; actor.SynchronizeSourceLighting(); actor.Lighting.VerifyUploads();
            }
            Assert(registry.BaddyConstruction.State.Count == initialCount + actors.Count,
                "All four genuine Black Cat native bodies share the existing scene CBaddy count.");
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ActorVisibilityImportChecks.VerifyAsync(owner, actors[0]);
            FixedVector3 explosion = new(12345, -54321, 67890);
            actors[0].ReceiveMessage(10, null, explosion);
            Assert(actors[0].Messages.Head is { Code: 10, SenderHandle: null } pending && pending.Vector0 == explosion,
                "Real manipulation explosion message10 retains its source null sender and one vector payload in Black Cat's actual CMessage queue.");
            advanceSourceClock();
            actors[0].Advance();
            actors[0].SynchronizeSourceLighting();
            Assert(actors[0].Messages.Head is null && !actors[0].State.IsDead && (actors[0].State.BodyFlags & 4) == 0,
                "Source EveryFrame clears the constructor timing flag before Black Cat physics/AI, which deletes all pending messages without an invented explosion reaction.");
            Assert(actors[0].FrameTiming.ElapsedTicks == 2 && actors[0].FrameTiming.AccumulatedTicks == 2 &&
                actors[0].FrameTiming.LastUpdateTick == configuration.Clock() &&
                actors[0].FrameTiming.BeforeUpdateAnimation.Animation == 12 && actors[0].FrameTiming.BeforeUpdateAnimation.Frame == 0,
                "The genuine EveryFrame owner records the first source clock, initial clip12/frame0 snapshot and accumulated2 before cinematic AI.");
            bool[] landed = new bool[actors.Count], scripted = new bool[actors.Count], moved = new bool[actors.Count];
            FixedVector3[] initialPositions = actors.Select(actor => actor.SourcePosition).ToArray();
            for (int update = 0; update < 2400 && actors.Any(actor => !actor.State.IsDead); update++)
            {
                advanceSourceClock();
                for (int index = actors.Count - 1; index >= 0; index--)
                {
                    GodotBlackCatActor actor = actors[index];
                    if (actor.State.IsDead) continue;
                    actor.Advance();
                    Assert(actor.FrameTiming.LastUpdateTick == configuration.Clock() && actor.FrameTiming.ElapsedTicks == 2,
                        "Every ordered cinematic body observes the same source iteration clock; no per-body synthetic delta is substituted.");
                    actor.SynchronizeSourceLighting();
                    landed[index] |= actor.State.Stage == 1 && actor.State.Substate == 2 || actor.State.Stage == 2;
                    scripted[index] |= actor.State.Stage == 2;
                    moved[index] |= actor.SourcePosition.X != initialPositions[index].X || actor.SourcePosition.Z != initialPositions[index].Z;
                    if (update % 50 == 0) actor.Lighting.VerifyUploads();
                }
                if (update % 12 == 0) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            Assert(landed.All(value => value) && scripted.All(value => value) && moved.All(value => value) && actors.All(actor => actor.State.IsDead),
                "Every untouched authored Black Cat lands against actual imported geometry, runs its complete cinematic program, moves to its real waypoint and reaches source Die0.");
            foreach (GodotBlackCatActor actor in actors)
            {
                Assert(actor.World.GroundQueries > 0 && actor.Shadow is { IsAlive: true } shadow && shadow.SourceCorners.Count == 4 &&
                    shadow.SourceCorners.All(corner => corner.Y == actor.State.GroundY << 12) && actor.State.DeathPulseSent &&
                    (actor.ItemFlags & 1) != 0 && actor.Lifecycle.Phase == ActorLifePhase.Dead,
                    "The real protected footprint uses four limb hooks at the source signed ground register and survives until actual body deletion.");
            }
            GodotBlackCatActor first = actors[0];
            GodotCinematicFootprintShadowReference shadowReference = new(first.Shadow!, first.Shadow!.SourceCorners.ToArray());
            fixture.RemoveChild(first); fixture.AddChild(first);
            Assert(registry.BaddyConstruction.State.Count == initialCount + actors.Count && shadowReference.Shadow.IsAlive &&
                shadowReference.Shadow.SourceCorners.SequenceEqual(shadowReference.Corners),
                "Reparenting a source cinematic actor destroys neither its constructor lease nor protected native footprint geometry.");
            foreach (GodotBlackCatActor actor in actors) actor.Free();
            Assert(registry.BaddyConstruction.State.Count == initialCount && actors.All(actor => !GodotObject.IsInstanceValid(actor)),
                "Actual native destruction releases each Black Cat base count once and retires its source command/shadow ownership.");
            var moduleRegistry = new GodotActorRegistry
            {
                Triggers = registry.Triggers, Player = registry.Player, Environment = registry.Environment,
                BaddyConstructionState = registry.BaddyConstructionState, Sounds = registry.Sounds,
                SoundListener = registry.SoundListener, VoicePlayback = registry.VoicePlayback, ReceivePulse = registry.ReceivePulse
            };
            fixture.AddChild(moduleRegistry); moduleRegistry.SetPhysicsProcess(false);
            var modules = new GodotActorCodeModules(moduleRegistry, thugs, configuration);
            modules.RegisterFactories();
            bool rejectedUnloaded = false;
            try { moduleRegistry.Spawn(registry.Triggers.Nodes[100]); }
            catch (InvalidOperationException) { rejectedUnloaded = true; }
            Assert(rejectedUnloaded && registry.BaddyConstruction.State.Count == initialCount && moduleRegistry.LiveCount == 0,
                "An unloaded source blackcat module rejects factory calls and unwinds the base count without adopting a body.");
            modules.LoadCodeModule("blackcat"); modules.LoadCodeModule("blackcat");
            moduleRegistry.Spawn(registry.Triggers.Nodes[100]);
            GodotScriptActor created = moduleRegistry.SourceActors(ActorUpdateGroup.Enemy).Single();
            Assert(created is GodotBlackCatActor && modules.Modules.SourceOrder.Count == 1 &&
                registry.BaddyConstruction.State.Count == initialCount + 1 && moduleRegistry.LiveCount == 1,
                "Genuine module189 activation supplies BlackCat userfunction0 to central Spawn with one constructor lease.");
            moduleRegistry.SignalActiveActors(100);
            var createdCat = (GodotBlackCatActor)created;
            Assert(createdCat.ConsumeAnimationSignal(OpenSpidey.Core.Actors.Cinematics.CinematicActorSignalSource.InputFlags) &&
                !createdCat.ConsumeAnimationSignal(OpenSpidey.Core.Actors.Cinematics.CinematicActorSignalSource.InputFlags) &&
                createdCat.State.BodyFlags == 0x214 && createdCat.ScriptState.PendingSignals == 0,
                "Canonical mission signals supply source input44 bit1 exactly once; Black Cat's wait15 leaves body46 flags untouched.");
            BlackCatDirectoryLifetimeImportChecks.Verify(fixture, moduleRegistry, modules, configuration, createdCat, initialCount);
            Assert(!GodotObject.IsInstanceValid(created) && moduleRegistry.LiveCount == 0 && moduleRegistry.DeletedCount == 2 &&
                registry.BaddyConstruction.State.Count == initialCount && modules.Modules.SourceOrder.Count == 0,
                "Each real BlackCat module clear immediately removes and frees its active319 body before unlinking its loaded module.");
            GD.Print($"Passed {assertions} native Black Cat checks: all four untouched L1A1 cinematic actors, original landing/model/VM/footprint and CBaddy lifetime.");
        }
        finally
        {
            foreach (GodotBlackCatActor actor in actors) if (GodotObject.IsInstanceValid(actor)) actor.Free();
            fixture.Free();
        }
        void Assert(bool condition, string evidence)
        { if (!condition) throw new InvalidDataException($"Native Black Cat: {evidence}"); assertions++; }
    }
    private sealed record GodotCinematicFootprintShadowReference(OpenSpidey.Game.Effects.GodotCinematicFootprintShadow Shadow,
        IReadOnlyList<FixedVector3> Corners);
}
