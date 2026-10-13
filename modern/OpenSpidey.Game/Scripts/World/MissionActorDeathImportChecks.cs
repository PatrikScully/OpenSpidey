using System.Buffers.Binary;
using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.UI;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Actual L1A4 source347/platform54 and67/control261 deaths, with real pre-player object composition and OBJ lifetime.</summary>
internal static class MissionActorDeathImportChecks
{
    private sealed class RejectedOwnership : Exception { }
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, GodotSoundChannels sounds, Func<SoundListener> listener)
    {
        var viewport = new SubViewport { Size = new(320, 240), OwnWorld3D = true }; owner.AddChild(viewport);
        GodotCombatSceneOwnership? ownership = null;
        try
        {
            var root = new Node3D { Name = "OriginalMissionActorDeath" }; viewport.AddChild(root);
            var repository = new PsxLibraryRepository(archive);
            ImportedLevelWorld world = ImportedLevelWorld.Load(repository, "data/L1A4_G.psx"); root.AddChild(world);
            byte[] triggerBytes = archive.Read("data/L1A4_T.trg"); TriggerFile triggers = TriggerFile.Read(triggerBytes);
            PsxAssetLibrary objects = repository.RequireLoaded("L1A4_O");
            bool rejectOwnership = false;
            var registry = new GodotActorRegistry { Triggers = triggers, ObjectLibrary = () => objects,
                ObjectTextures = () => repository.RequireLoaded("L1A4_L").Textures.Concat(objects.Textures).ToArray(),
                ClaimObjectModel = (path, index) => { if (rejectOwnership) throw new RejectedOwnership(); world.ClaimObjectModel(path, index); },
                Sounds = sounds.Controller, SoundListener = listener, Environment = new(LevelId: 0x104), ProcessMode = Node.ProcessModeEnum.Disabled };
            root.AddChild(registry); ownership = new(registry);
            uint clock = 100;
            registry.ControlServices = new(new CompassState(), sounds.Controller, listener, () => clock / 2);
            GodotLevelObjectRuntime? objectsRuntime = null;
            var visibility = new GodotEnvironmentVisibility(() => world, repository, (source, index) =>
                objectsRuntime?.PreparedEnvironment?.Items.FirstOrDefault(item => item.SourceAsset == source.AssetPath && item.SourceInstanceIndex == index));
            var countdown = new MissionCountdown();
            objectsRuntime = GodotLevelObjectRuntime.Prepare(new()
            {
                Archive = archive, Repository = repository, CurrentWorld = () => world, Registry = registry, SceneOwnership = ownership,
                Sounds = sounds, Listener = listener, Countdown = countdown, TriggerData = triggerBytes, Clock = () => clock, Visibility = visibility
            }); root.AddChild(objectsRuntime);
            var death = new GodotMissionActorDeath(registry, visibility);
            var mission = new MissionRuntime(triggers, registry, MissionActorDeathCommandHandler.CreateHandlers(triggers, death));
            registry.ReceivePulse = mission.ReceivePulse;
            registry.Spawn(triggers.Nodes[54]);
            var platform = registry.SourceList(ActorUpdateGroup.Environment).SourceOrder.OfType<GodotPlatformActor>().Single();
            registry._PhysicsProcess(1.0 / 60); registry._PhysicsProcess(1.0 / 60);
            Assert(platform.ModelChecksum == 0x18591BAB && platform.SourceMesh?.Mesh is ArrayMesh { } initial && initial.GetSurfaceCount() > 0,
                "Actual initial platform54 program must select its genuine212F OBJ checksum before waiting for a signal.");
            var original = platform.CaptureOwnedResources();
            int originalIndex = platform.ModelIndex;
            TriggerActorDefinition other = TriggerActorDefinition.Read(triggers.Nodes[45]);
            var sourceOperands = new ActorScriptCursor(other.BehaviorData, triggers.Nodes[45].SourceOffset + other.ObjectDataOffset + 18);
            Assert(sourceOperands.ReadWord() == 0x212F, "Real platform45 must retain its original model-selection command.");
            int replacementIndex = objects.FindModel(sourceOperands.ReadAlignedDword());
            Assert(replacementIndex != originalIndex, "The original45/54 OBJ records must provide distinct genuine model replacements.");
            Select(platform, replacementIndex);
            Assert(original.Materials.IsDisposed && original.Resources.All(resource => !GodotObject.IsInstanceValid(resource)) &&
                platform.ModelIndex == replacementIndex && platform.SourceMesh?.Mesh is ArrayMesh { } replacement && replacement.GetSurfaceCount() > 0,
                "A real platform model replacement must retire its previous private GPU/physics resources and install the actual other source model.");
            var stable = platform.CaptureOwnedResources();
            double beforeFailure = Performance.GetMonitor(Performance.Monitor.ObjectResourceCount);
            rejectOwnership = true; bool rejected = false;
            try { Select(platform, originalIndex); } catch (RejectedOwnership) { rejected = true; }
            rejectOwnership = false;
            Assert(rejected && platform.ModelIndex == replacementIndex && stable.Resources.All(GodotObject.IsInstanceValid) &&
                Performance.GetMonitor(Performance.Monitor.ObjectResourceCount) == beforeFailure,
                "Rejected required OBJ transfer must release the complete temporary replacement while preserving the previous platform owner.");
            var failed = GodotPlatformActor.Create(triggers.Nodes[54], registry);
            double beforeInitialization = Performance.GetMonitor(Performance.Monitor.ObjectResourceCount);
            rejectOwnership = true; rejected = false;
            try { Select(failed, originalIndex); } catch (RejectedOwnership) { rejected = true; }
            rejectOwnership = false;
            Assert(rejected && failed.SourceMesh is null && Performance.GetMonitor(Performance.Monitor.ObjectResourceCount) == beforeInitialization,
                "A platform's failed first model initialization must release every allocated mesh/material/texture/shape before registry adoption.");
            failed.Free(); Assert(failed.ResourcesDisposed, "Failed initial platform owner must execute its genuine native Predelete.");
            var former = new Node3D(); root.AddChild(former); platform.Reparent(former); platform.Reparent(registry); former.Free();
            Assert(registry.Owns(platform) && !platform.ResourcesDisposed && !stable.Materials.IsDisposed && stable.Resources.All(GodotObject.IsInstanceValid),
                "Actual platform resource ownership must survive reparent and the former container's destruction.");
            ushort flags = platform.SourceVisibilityFlags; int pending = mission.PendingCount;
            mission.ReceivePulse(347); mission.ExecutePending();
            Assert((platform.BodyFlags & 0x40) != 0 && platform.Lifecycle.Phase == ActorLifePhase.Dead &&
                platform.SourceVisibilityFlags == flags && mission.PendingCount == pending && registry.Owns(platform) && !platform.ResourcesDisposed,
                "Authored34711 must call inherited CBody::Die on54, retain its drawable/flags, avoid script-completion55 pulses and defer destruction.");
            int updates = platform.SourceUpdates;
            registry._PhysicsProcess(1.0 / 60); registry._PhysicsProcess(1.0 / 60);
            Assert(platform.Lifecycle.Phase == ActorLifePhase.PendingDeletion && registry.Owns(platform) && platform.SourceUpdates == updates,
                "The first genuine source zombie update must retain54 and run no platform AI.");
            registry._PhysicsProcess(1.0 / 60); registry._PhysicsProcess(1.0 / 60);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(!registry.Owns(platform) && platform.ResourcesDisposed && stable.Materials.IsDisposed &&
                stable.Resources.All(resource => !GodotObject.IsInstanceValid(resource)),
                "The next original deletion update must retire54 and every private platform GPU/physics resource.");

            registry.Spawn(triggers.Nodes[261]);
            var control = registry.SourceList(ActorUpdateGroup.Control).SourceOrder.OfType<GodotControlActor>().Single();
            TriggerInstruction box = TriggerCommandReader.Read(triggers.Nodes[67]).Single(instruction => instruction.Opcode == 133);
            new MissionActorDeathCommandHandler(133, triggers, death).Execute(new(triggers.Nodes[67], 0), box);
            Assert((control.BodyFlags & 0x40) != 0 && control.Lifecycle.Phase == ActorLifePhase.Dead && registry.Owns(control) &&
                !control.ScriptState.DeathPulseSent, "Actual source67 box must select real261 inside its unchanged bounds and call base Die without script-completion pulses.");
            registry.DestroyImmediately(control);
            registry.Spawn(triggers.Nodes[261]);
            control = registry.SourceList(ActorUpdateGroup.Control).SourceOrder.OfType<GodotControlActor>().Single();
            registry.SuspendActiveActors(261);
            new MissionActorDeathCommandHandler(133, triggers, death).Execute(new(triggers.Nodes[67], 0), box);
            Assert(!GodotObject.IsInstanceValid(control) && registry.SuspendedSourceList.Count == 0 &&
                registry.SourceList(ActorUpdateGroup.Control).Count == 0,
                "The same authored133 box must use the immediate deleting destructor for the real suspended261 owner.");
            registry.Spawn(triggers.Nodes[14]);
            var bomb = registry.SourceList(ActorUpdateGroup.Environment).SourceOrder.OfType<GodotManipulationActor>().Single();
            countdown.Running = 1; countdown.DisplayEnabled = 1; countdown.RemainingTicks = 300;
            death.Die(bomb);
            Assert(bomb.LevelOneBomb is not null && (bomb.State.BodyFlags & 0x40) == 0 && registry.Owns(bomb) &&
                countdown.Running == 0 && countdown.DisplayEnabled == 0 &&
                registry.SourceList(ActorUpdateGroup.Control).SourceOrder.OfType<GodotControlActor>().Select(actor => actor.SourceNodeId).Order().SequenceEqual(new ushort[] { 65, 70 }),
                "The original600 virtual Die must pulse actual65/70 links and stop the scene timer while keeping its source401 body alive.");
            registry.DestroyImmediately(bomb);
            foreach (GodotControlActor linked in registry.SourceList(ActorUpdateGroup.Control).SourceOrder.OfType<GodotControlActor>().ToArray())
                registry.DestroyImmediately(linked);
            Assert(registry.LiveCount == 0, "Private mission death proof must retire every actual source actor.");
            GD.Print("Verified source347/54 base death and deferred resource destruction,67 active/suspended261 distinction, real600 virtual timer/link override, actual OBJ replacement/failure/reparent ownership.");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(viewport)) viewport.Free();
            ownership?.Dispose();
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
    private static void Select(GodotPlatformActor actor, int index)
    { byte[] bytes = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)index)); actor.WriteVariable(0x2124, new(bytes)); }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
