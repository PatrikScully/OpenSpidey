using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Npcs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>Genuine L6A4 Mary Jane, L6A3 lizard and L8A5 submariner records through module189 factories and real models.</summary>
internal static class IdleNpcImportChecks
{
    private sealed record Sample(string Triggers, int LevelId, ushort Node, short Type, int SecondClip);

    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository, ReadOnlyMemory<byte> originalImage,
        GodotThugConfiguration thugs, GodotBlackCatConfiguration blackCat)
    {
        var fixture = new Node3D { Name = "OriginalIdleNpcProof", TopLevel = true };
        owner.AddChild(fixture);
        int assertions = 0;
        uint clock = 0x100;
        bool fastForward = false;
        var resources = new GodotIdleNpcResources(repository, originalImage);
        var configuration = new GodotIdleNpcConfiguration
        {
            ModelFactory = resources.CreateModel, Light = resources.ReadLight, LightingState = new SourceVertexLightingState(),
            Clock = () => clock, FastForwardRequested = () => fastForward, IsSourceCulled = _ => false
        };
        try
        {
            foreach (Sample sample in new Sample[] { new("data/L6A4_T.trg", 0x604, 142, 303, 1), new("data/l6a3_t.trg", 0x603, 273, 326, 5),
                new("data/l8a5_t.trg", 0x805, 51, 719, -1) })
            {
                TriggerFile triggers = TriggerFile.Read(archive.Read(sample.Triggers));
                var pulses = new List<ushort>();
                var registry = new GodotActorRegistry
                {
                    Name = $"IdleNpcRegistry{sample.Type}", Triggers = triggers, Environment = new(LevelId: sample.LevelId),
                    ReceivePulse = pulses.Add
                };
                fixture.AddChild(registry); registry.SetPhysicsProcess(false);
                var modules = new GodotActorCodeModules(registry, thugs, blackCat, idleNpcs: configuration);
                modules.RegisterFactories();
                TriggerNode node = triggers.Nodes[sample.Node];
                TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
                IdleNpcKind kind = IdleNpcState.KindOf(sample.Type);
                Assert(definition.ActorType == sample.Type && definition.ClearsBodyFlag2 && !definition.StartsSuspended && definition.Links.Count == 0,
                    $"Untouched {sample.Triggers} node{sample.Node} is an active marker4 source{sample.Type} actor without death links.");
                bool rejected = false;
                try { registry.Spawn(node); }
                catch (InvalidOperationException) { rejected = true; }
                Assert(rejected && registry.LiveCount == 0 && registry.BaddyConstructionState.Count == 0,
                    $"An unloaded {IdleNpcState.ModuleNameOf(kind)} module rejects its factory and unwinds the CBaddy count.");
                modules.LoadCodeModule(IdleNpcState.ModuleNameOf(kind));
                registry.Spawn(node);
                var actor = (GodotIdleNpcActor)registry.SourceActors(ActorUpdateGroup.Enemy).Single();
                Assert(actor.State.Kind == kind && actor.SourceActorType == actor.State.BodyType && actor.State.BodyFlags == 0x214 &&
                    actor.ItemFlags == 0x482 && actor.Model.ClipId == 0 && !actor.Model.AdvanceAutomatically && actor.MinorRadius == 0 &&
                    registry.BaddyConstructionState.Count == 1 && actor.State.SourceConstructionOrdinal == 0 && actor.TransformBuilds == 2,
                    $"{kind} imports the genuine constructor: clip0 cycle, item flags 0x480, CBaddy count and body type{actor.State.BodyType}.");
                Assert(!actor.Hit(new ActorHit { Damage = ushort.MaxValue }) && actor.State.BodyFlags == 0x214,
                    "The shared slot3 Hit leaf returns0 without mutating the body.");
                actor.SynchronizeSourceLighting(); actor.Lighting.VerifyUploads();
                Assert(actor.Lighting.LitPartCount > 0, $"Actual {kind} SLight data lights the genuine model parts.");
                for (int frame = 0; frame < 30; frame++)
                {
                    clock += 2; actor.Advance(); actor.SynchronizeSourceLighting();
                }
                Assert(actor.SourceUpdates == 30 && actor.Model.ClipId == 0 && !actor.State.IsDead && actor.TransformBuilds == 32,
                    $"{kind} runs bounded source frames, rebuilding its transform each AI frame without changing clip.");
                actor.ReceiveMessage(10, null);
                registry.SignalActiveActors(sample.Node);
                clock += 2; actor.Advance();
                if (sample.SecondClip >= 0)
                    Assert(actor.Model.ClipId == sample.SecondClip && actor.InputFlags == 0 && actor.Messages.Head is null,
                        $"The canonical trigger signal toggles {kind} to clip{sample.SecondClip} once and deletes its CMessage list.");
                else
                    Assert(actor.Model.ClipId == 0 && actor.InputFlags == 1 && actor.Messages.Head is not null,
                        "CSubmariner's AI ignores trigger signals and messages.");
                if (sample.SecondClip >= 0)
                {
                    registry.SignalActiveActors(sample.Node);
                    clock += 2; actor.Advance();
                    Assert(actor.Model.ClipId == 0, $"A second signal returns {kind} to clip0.");
                }
                modules.Modules.Unload(IdleNpcState.ModuleNameOf(kind));
                Assert(GodotObject.IsInstanceValid(actor) && registry.Owns(actor) && modules.Modules.SourceOrder.Count == 0,
                    "The shared module clear 0x4138D0 deletes only type319 and leaves this body allocated.");
                fastForward = true;
                clock += 2; actor.Advance();
                if (kind == IdleNpcKind.Submariner)
                    Assert(actor.State.IsDead && (actor.ItemFlags & 1) != 0 && actor.Lifecycle.Phase == ActorLifePhase.Dead && pulses.Count == 0,
                        "The fast-forward byte makes CSubmariner::AI run CBaddy::Die(0) with no authored links to pulse.");
                else
                    Assert(!actor.State.IsDead, $"{kind} AI never tests the fast-forward byte after construction.");
                fastForward = false;
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                Node3D destination = new() { Name = "IdleNpcReparent" }; fixture.AddChild(destination);
                registry.RemoveChild(actor); destination.AddChild(actor);
                Assert(registry.BaddyConstructionState.Count == 1 && GodotObject.IsInstanceValid(actor.Model),
                    "Reparenting keeps the CBaddy lease and model.");
                destination.RemoveChild(actor); registry.AddChild(actor);
                registry.DestroyImmediately(actor);
                Assert(!GodotObject.IsInstanceValid(actor) && registry.LiveCount == 0 && registry.BaddyConstructionState.Count == 0,
                    "Actual native destruction releases the CBaddy count once.");
                fastForward = kind != IdleNpcKind.Lizard;
                modules.LoadCodeModule(IdleNpcState.ModuleNameOf(kind));
                registry.Spawn(node);
                var aborted = (GodotIdleNpcActor)registry.SourceActors(ActorUpdateGroup.Enemy).Single();
                Assert(aborted.State.IsDead == (kind != IdleNpcKind.Lizard),
                    $"{kind} constructor applies the original abort-byte Die(0) rule.");
                fastForward = false;
                modules.UnloadAllCodeModules();
                registry.Free();
            }
            GD.Print($"Passed {assertions} native idle NPC checks: L6A4 Mary Jane, L6A3 lizard and L8A5 submariner constructors, AI frames, signals and module lifetime.");
        }
        finally
        {
            fixture.Free();
        }
        void Assert(bool condition, string evidence)
        { if (!condition) throw new InvalidDataException($"Native idle NPC: {evidence}"); assertions++; }
    }
}
