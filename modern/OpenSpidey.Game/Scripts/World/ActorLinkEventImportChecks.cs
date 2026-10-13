using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Actual L1A2 platform100 model/contact script, genuine pulse-created203 bodies and queued101 signals.</summary>
internal static class ActorLinkEventImportChecks
{
    public static void VerifyL1A2Platform(Node3D owner, PkrArchive archive)
    {
        var viewport = new SubViewport { Name = "L1A2PlatformLinkProof", OwnWorld3D = true, Size = new(640, 480) };
        owner.AddChild(viewport); var root = new Node3D(); viewport.AddChild(root);
        GodotActorRegistry? registry = null;
        try
        {
            var libraries = new PsxLibraryRepository(archive);
            ImportedLevelWorld world = ImportedLevelWorld.Load(libraries, "data/L1A2_G.psx"); root.AddChild(world);
            PsxAssetLibrary objects = libraries.RequireLoaded("data/L1A2_O.psx");
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A2_T.trg"));
            registry = new GodotActorRegistry { Name = "ActualPlatform100Actors", Triggers = triggers,
                Environment = new(LevelId: 0x102), ObjectLibrary = () => objects,
                ObjectTextures = () => libraries.Loaded.Values.SelectMany(library => library.Textures).ToArray(),
                ClaimObjectModel = world.ClaimObjectModel };
            root.AddChild(registry); registry.SetPhysicsProcess(false);
            var mission = new MissionRuntime(triggers, registry,
                [new ActorSignalCommandHandler(MissionOpcode.SendSignal, triggers, registry)]);
            registry.ReceivePulse = mission.ReceivePulse;
            var queued = new List<ushort>();
            mission.Trace += trace => { if (trace.Kind == MissionTraceKind.Queued) queued.Add(trace.NodeId); };
            registry.Spawn(triggers.Nodes[100]); registry.Spawn(triggers.Nodes[102]);
            GodotPlatformActor platform = registry.SourceActors(ActorUpdateGroup.Environment).OfType<GodotPlatformActor>().Single();
            GodotControlActor receiver = registry.SourceActors(ActorUpdateGroup.Control).OfType<GodotControlActor>().Single();
            Assert(platform.Definition.NodeId == 100 && platform.Definition.Links.SequenceEqual(new ushort[] { 101, 178 }) &&
                receiver.SourceNodeId == 102 && registry.Player is null && registry.BaddyConstruction.State.Count == 2,
                "The genuine no-player source subset retains platform100 links101/178 and the actual initial waiting control102, with distinct original constructor leases.");
            registry.SourceList(ActorUpdateGroup.Environment).Update();
            MeshInstance3D mesh = platform.GetChildren().OfType<MeshInstance3D>().Single();
            Transform3D initial = platform.Transform;
            PsxModel sourceModel = objects.Scene.Models[platform.ModelIndex];
            Assert(platform.ModelChecksum == 0xD7833D12 && ReferenceEquals(platform.SourceModelLibrary, objects) &&
                sourceModel.Vertices.Count == 112 && sourceModel.Faces.Count == 196 &&
                !sourceModel.Faces.Any(face => PsxRenderPolicy.IsFaceVisible(face.Flags)) && mesh.Mesh is null &&
                platform.ScriptState.Active && mission.PendingCount == 0 && registry.SourceActors(ActorUpdateGroup.Control).Count == 1 &&
                ReferenceEquals(platform.SupportMotion, platform.Motion.Body),
                "First actual ENV AI selects original OBJ model7 (112vertices/196 nonrendered faces), retains its native model/collision/body owners without inventing a visible mesh, then4300 waits for contact.");
            // NotifyTrodUpon's real source entry owns field334; no script callback or mission event is replaced.
            platform.NotifyTrodden();
            registry.SourceList(ActorUpdateGroup.Environment).Update();
            GodotControlActor[] spawned = registry.SourceActors(ActorUpdateGroup.Control).OfType<GodotControlActor>()
                .Where(actor => actor.SourceNodeId == 178).ToArray();
            Assert(queued.SequenceEqual(new ushort[] { 101, 101 }) && mission.PendingCount == 2 && mission.CommandPoints[101].PulsesReceived == 2 &&
                spawned.Length == 2 && spawned.All(actor => actor.ScriptState.WaitTicks == 64 && actor.Body.Recorded.ElapsedTicks == 128 && actor.SourceUpdates == 0) &&
                registry.BaddyConstruction.State.Count == 4,
                "Authored42B2(212A own-node) and the independent4205 death pulse each queue101 and construct real178; source deliberately retains both events and both constructors.");
            Assert(platform.Lifecycle.Phase == ActorLifePhase.Dead && platform.ScriptState.DeathPulseSent && !platform.ScriptState.Active &&
                GodotObject.IsInstanceValid(mesh) && ReferenceEquals(mesh, platform.GetChildren().OfType<MeshInstance3D>().Single()) && platform.Transform == initial,
                "Source4205 retires then continues to Stop; the same native model remains allocated until ordinary source deletion without changing its geometry/transform.");
            byte[] ownNode = [0x2A, 0x21];
            var signalOperand = new ActorScriptCursor(ownNode);
            Assert(platform.ExecuteCommand(0x42B1, signalOperand, registry.Environment) && signalOperand.Position == 2 &&
                spawned.All(actor => actor.ScriptState.PendingSignals == 1) && receiver.ScriptState.PendingSignals == 0 && mission.PendingCount == 2,
                "The real inherited42B1 own-node getter signals both canonical active178 controls while ignoring command-point101 and preserving the independent pending pulse queue.");
            Assert(mission.ExecutePending() == 2 && receiver.ScriptState.PendingSignals == 1 && mission.PendingCount == 0,
                "The real queued101 command lists execute source10 twice and set the actual control102 input bit rather than recursively inventing a script event.");
            registry.SourceList(ActorUpdateGroup.Control).Update();
            Assert(receiver.ScriptState.SignalSnapshot == 1 && receiver.ScriptState.PendingSignals == 0 &&
                receiver.ScriptState.PulsesReceived == 1 && !receiver.ScriptState.Active && registry.Player is null &&
                spawned.All(actor => actor.ScriptState.WaitTicks == 62 && actor.SourceUpdates == 1 && actor.ScriptState.PulsesReceived == 1),
                "The next genuine Control phase consumes one shared signal bit, releases102’s4281, and advances both real178 timers once in captured source order.");
            registry.SuspendActiveActors(178);
            Assert(registry.SuspendedSourceList.Count == 2 && registry.BaddyConstruction.State.Count == 4 &&
                platform.ExecuteCommand(0x42B1, new ActorScriptCursor(ownNode), registry.Environment) &&
                spawned.All(actor => actor.ScriptState.PendingSignals == 0),
                "Source42B1 searches only active heads: actual suspended178 bodies retain their allocated leases and receive no invented signal.");
        }
        finally
        {
            if (registry is not null && GodotObject.IsInstanceValid(registry))
                foreach (GodotScriptActor actor in Enum.GetValues<ActorUpdateGroup>().SelectMany(registry.SourceActors).ToArray())
                    if (registry.Owns(actor)) registry.DestroyImmediately(actor);
            viewport.Free();
        }
        GD.Print("Verified actual L1A2 platform100: original OBJ/canonical body,4300 contact,42B2 own-node links, independent4205 duplicate pulses, genuine178 constructors, queued101/source10 and live102 signal consumption.");
    }
    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native L1A2 linked events: {evidence}"); }
}
