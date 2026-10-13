using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Audio;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.World;

/// <summary>Init_KillAll over genuine L1A3 actors, native bullet models, protected player/dome companions and the actual retry UI.</summary>
internal static class LevelSceneObjectImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository,
        OriginalPlayerData original, Func<GodotPlayer> playerFactory, ReadOnlyMemory<byte> originalExecutable,
        PsxAssetRegions regions)
    {
        var viewport = new SubViewport { Name = "OriginalSceneDestructorProof", OwnWorld3D = true,
            Size = new(640, 480), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        Node3D? proofRoot = null;
        bool observing = false;
        int checks = 0;
        try
        {
            var root = new Node3D(); proofRoot = root; viewport.AddChild(root);
            byte[] triggerData = archive.Read("data/L1A3_T.trg");
            TriggerFile triggers = TriggerFile.Read(triggerData);
            ImportedLevelWorld world = ImportedLevelWorld.Load(repository, "data/L1A3_G.psx"); root.AddChild(world);
            PsxAssetLibrary geometry = repository.RequireLoaded("data/L1A3_G.psx"), objects = repository.Load("data/L1A3_O.psx");
            var textures = repository.Load("data/L1A3_L.psx").Textures.Concat(geometry.Textures).Concat(objects.Textures).ToArray();
            GodotPlayer player = playerFactory(); root.AddChild(player); player.SetPhysicsProcess(false);
            RestartPoint restart = triggers.Nodes.Where(node => node.TypeCode == (ushort)TriggerNodeKind.Restart)
                .Select(RestartPoint.Read).First();
            player.Controller.TeleportFromScript(new FixedVector3(restart.Position.X, restart.Position.Y, restart.Position.Z) << 12);
            player.RefreshCombatTransform(); player.Model.AdvanceAutomatically = false;
            uint clock = 1000;
            var camera = new PlayerFollowCamera { Target = player, Current = true, SourceTotalTime = () => clock };
            root.AddChild(camera); camera.SetPhysicsProcess(false); camera.MakeCurrent();
            var sounds = new GodotSoundChannels { Archive = archive, LevelGroup = SoundCatalog.Find("L1A3") }; root.AddChild(sounds);
            sounds.Controller.PlayerBank = SoundDefinitionBank.Read(archive.Read("data/spidey.sfx"));
            sounds.Controller.LevelBank = SoundDefinitionBank.Read(archive.Read("data/l1a3.sfx"), levelSpecific: true);
            var speech = new GodotSpeechPlayer { CacheDirectory = owner.GetNode<GodotSpeechPlayer>("Speech").CacheDirectory };
            root.AddChild(speech); speech.SetPhysicsProcess(false);
            SoundListener Listener() => new(camera.SourcePosition >> 12);
            var random = new GameRandom(0x4F6F00);
            var registry = new GodotActorRegistry { Triggers = triggers, Player = player, Difficulty = 2,
                SuspensionDistance = uint.MaxValue, Environment = new(LevelId: 0x103), RandomSource = random.Next,
                Sounds = sounds.Controller, SoundListener = Listener,
                ObjectLibrary = () => objects, ObjectTextures = () => textures, ClaimObjectModel = world.ClaimObjectModel,
                VoicePlayback = (bank, track, priority) => speech.Play(bank, track, priority) };
            root.AddChild(registry); registry.SetPhysicsProcess(false);
            player.Combat.SourceTimer = () => clock;
            player.Combat.Sound = (sound, volume, _) => sounds.Controller.Play(sound, unchecked((short)volume));
            player.Combat.PositionSound = (sound, position, _) => sounds.Controller.PlayAt(sound, position, Listener());
            player.Combat.SoundStopped = handle => sounds.Controller.Stop(unchecked((uint)handle));
            player.Combat.XaStopped = speech.Controller.Stop;
            player.Combat.CameraBehind = camera.PutBehind; player.Combat.CameraField12C = camera.SetFocusHook;
            player.Combat.CameraFlag = () => camera.TerrainObstructed;
            player.Inventory = new(new() { Webbing = 4096, Cartridges = 10 }, player, sounds.Controller, speech,
                Listener, () => registry.Difficulty, () => false, random.Next);
            var armorBefore = (player.Combat.State.ArmorActive, player.Combat.State.ArmorTexturesActive,
                player.Combat.State.SuitArmorEnabled, player.Combat.State.ArmorAmount);
            foreach (var flags in new[] { (Damage: true, Textures: false, Suit: (byte)0),
                (Damage: false, Textures: true, Suit: (byte)128), (Damage: true, Textures: true, Suit: (byte)0) })
            {
                player.Combat.State.ArmorActive = flags.Damage;
                player.Combat.State.ArmorTexturesActive = flags.Textures;
                player.Combat.State.SuitArmorEnabled = flags.Suit;
                player.Combat.State.ArmorAmount = 37;
                Assert(player.ReadCarriedSaveState() == new PlayerCarriedSaveState(4096, 10, 37,
                    flags.Textures ? (byte)1 : (byte)0, flags.Suit),
                    "The actual player destructor capture uses global armor-set and independent player57C, never damage flag5E9 for save7A.");
            }
            (player.Combat.State.ArmorActive, player.Combat.State.ArmorTexturesActive,
                player.Combat.State.SuitArmorEnabled, player.Combat.State.ArmorAmount) = armorBefore;
            var combat = GodotPlayerCombatRuntime.Create(archive, repository, original, player, registry, () => world, camera, speech, () => clock);
            root.AddChild(combat); combat.SetPhysicsProcess(false);
            var web = GodotPlayerWebRuntime.Create(archive, repository, player, combat, camera, () => clock, random.Next,
                (sound, position, _) => unchecked((int)sounds.Controller.PlayAt(sound, position, Listener())));
            root.AddChild(web); web.ConfigureDomes(registry, combat, repository, camera, () => clock, random.Next);
            var target = new GodotScreenTarget { Camera = camera }; root.AddChild(target);
            var lookaround = new GodotPlayerLookaroundRuntime(player, web, combat, camera, target, new()
            {
                PracticeMode = () => false, InvertPitch = () => false, SwingWebPresent = () => web.Swing?.SwingPresent == true,
                BeginZip = player.Controller.BeginLookaroundZip, BeginSwing = _ => player.Controller.ApplyCombatState()
            });
            player.Combat.LookaroundExited = lookaround.Camera.ExitLookaroundMode;
            web.ConfigureSwing(camera, combat, lookaround.Targets); player.Controller.Web = web; player.Controller.Lookaround = lookaround;
            GodotLevelObjectRuntime? objectRuntime = null;
            var visibility = new GodotEnvironmentVisibility(() => world, repository, (source, index) =>
                objectRuntime?.Environment.Items.FirstOrDefault(item => item.SourceAsset.Equals(source.AssetPath,
                    StringComparison.OrdinalIgnoreCase) && item.SourceInstanceIndex == index));
            objectRuntime = GodotLevelObjectRuntime.Create(archive, repository, world, registry, combat, sounds,
                camera, new MissionCountdown(), triggerData, () => clock, visibility);
            root.AddChild(objectRuntime);
            var visibilityLists = new GodotVisibilityBoxHost(registry, visibility, repository);
            MissionRuntime? mission = null;
            var enemies = GodotEnemyRuntime.Create(new()
            {
                Archive = archive, Repository = repository, OriginalExecutable = originalExecutable, Regions = regions,
                Registry = registry, Combat = combat, Web = web, Sounds = sounds, Speech = speech, Camera = camera,
                CurrentWorld = () => world, EnvironmentVisibility = visibility, LightingState = new(),
                Clock = () => clock, AttackClock = () => unchecked((int)clock), TotalTime = () => unchecked((int)clock),
                TriggerCommandPoint = checksum => (mission ?? throw new InvalidOperationException("Native source command-point owner is not ready.")).TriggerCommandPoint(checksum),
                HostageCamera = new GodotOptimumViewingCamera(triggers, combat.World, camera, visibilityLists)
            });
            root.AddChild(enemies);
            mission = new(triggers, registry, [new ModuleLoadCommandHandler(enemies.Modules)]); registry.ReceivePulse = mission.ReceivePulse;
            enemies.Modules.LoadCodeModule("thug");
            GodotThugActor activeEnemy = Spawn<GodotThugActor>(8), suspendedEnemy = Spawn<GodotThugActor>(10);
            GodotControlActor control = Spawn<GodotControlActor>(45);
            GodotManipulationActor item = Spawn<GodotManipulationActor>(193), orphan = Spawn<GodotManipulationActor>(218);
            GodotSwitchActor vault = Spawn<GodotSwitchActor>(176);
            registry.SuspendActiveActors(suspendedEnemy.SourceNodeId);
            Assert(registry.BaddyConstructionState.Count > 0 && registry.SuspendedSourceList.Count == 1 &&
                registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder.Contains(activeEnemy) &&
                registry.SourceList(ActorUpdateGroup.Control).SourceOrder.Contains(control) &&
                registry.SourceList(ActorUpdateGroup.Control).SourceOrder.Contains(vault) &&
                registry.SourceList(ActorUpdateGroup.Environment).SourceOrder.Contains(item),
                "Untouched L1A3 definitions must construct active/suspended enemies, source203 control,401 OBJ and407 vault in their canonical source families.");
            var messages = GodotMissionMessages.Create(OriginalMissionMessageData.Read(originalExecutable),
                OriginalBitmapFontReader.Read(archive.Read("data/sp_fnt00.fnt"), "sp_fnt00"));
            root.AddChild(messages); messages.ShowCheckpointMessage();
            messages.ShowSimpleMessage(OriginalMissionMessageData.Read(originalExecutable).CheckpointText, 256, 30, 125);
            var lifecycle = new GodotLevelLifecycle { ProcessMode = Node.ProcessModeEnum.Always }; root.AddChild(lifecycle);
            lifecycle.RequestExit((int)LevelExitReason.RetryRequested);
            await Frame(); await Frame(); await Physics(); await Physics();
            Button retry = Descendants(lifecycle).OfType<Button>().Single(button => button.Text == "Restart checkpoint");
            Assert(!retry.Disabled && messages.Messages.SourceMessages.Count == 1 && messages.Messages.SourceSimpleMessages.Count == 1,
                "The native retry UI and both real source message lists must exist before source scene destruction.");

            GodotPlayerSwing swing = web.Swing!;
            GodotActorWorldHit contact = FindActualContact(item, combat.World);
            var sourceNormal = GodotImpactWebNativeHost.Normal(contact);
            FixedVector3 normal = new(sourceNormal.X, sourceNormal.Y, sourceNormal.Z);
            int length = FixedVectorMath.OriginalDistance(player.Controller.Position, contact.Position);
            Assert(length > 0, "The source Swinger constructor must use a nonzero distance to a real imported OBJ face.");
            PlayerDistanceAttackPreparationImportChecks.Verify(player, camera, web, combat, repository,
                sounds.Controller, item, contact.Position, normal);
            WebSwingAnchor anchor = swing.CreateSwing(contact.Position, length,
                PlayerSwingBasis.Calculate(player.Controller.Position, contact.Position, normal, default).Matrix, normal);
            swing.State.Current = anchor;
            var currentWeb = new WebStrandOwner(web.Actions);
            currentWeb.Fire(player.Melee!.GetPlayerHookPosition(0), contact.Position, null, false, default, camera.SourcePosition >> 12, random.Next);
            var currentLine = currentWeb.Strand!;
            web.Actions.State.CurrentStrand = currentWeb;
            web.Contacts.AddTrap(activeEnemy, 0);
            FireEmber ember = web.Embers.Emit(item.Body.Position, 256, random.Next);
            player.Melee.PlaySingleAnimation(0x11C, 0, -1); player.Model.SetSourceAnimationFrame(4);
            player.Controller.CaptureCombatState(2);
            PlayerKickHandle domeHandle = web.Domes!.Spawn(0);
            registry.SourceList(ActorUpdateGroup.Miscellaneous).Update();
            WebDome dome = web.Domes.Recover(ref domeHandle)!;
            Assert(dome.Pieces.All(piece => piece is { BodyFlags: var flags } && (flags & 0x20) != 0),
                "Actual CDome AI must create its five protected36 model companions before Init_KillAll.");
            GodotWebDomeModel[] pieceModels = dome.Pieces.Select(piece => web.Domes.ModelFor(piece!)).ToArray();
            Assert(web.Domes.Burst(ref domeHandle), "The real dome burst must create its source ring, shock and fragment owners.");
            objectRuntime.Explosions.Emit(item.Body.Position, random.Next);
            // Source fire-kick context creates its actual ten rocks, two rings and six line particles.
            combat.Impacts.Emit(new(1, 1, 100), item.Body.Position, 0);
            objectRuntime.Effects.SpawnChunkSmoke(new(item.Body.Position, item.Body.Position, 0));
            bool shatterEmitted = combat.ItemShatter.Shatter(item, item.SourceVisibilityBank!, item.ModelIndex,
                item.Body.Position, item.Body.Angles, false, true, textures);
            Assert(combat.Rocks.Count == 10 && combat.Impacts.Lines.Count == 6 && objectRuntime.Effects.Smoke.Count == 1 &&
                shatterEmitted && combat.ItemShatter.ChunkCount + combat.ItemShatter.GlassCount > 0,
                "Original fire-kick, chunk-smoke and the actual401 model shatter must populate their real Flat/GLine/Chunk/Glass owners before lifecycle verification.");
            GodotExplosionModel[] bullets = objectRuntime.Explosions.SourceModelOrder.ToArray();
            Assert(bullets.Length == 9 && objectRuntime.Explosions.Count == 9 && combat.Particles.Count > 0 && web.Domes.ShockCount > 0,
                "Actual source constructors must populate BulletList and real line/quad/flat/nonrendered bit owners before cleanup.");
            await PlayerScriptDeathImportChecks.VerifyAsync(root, player, registry, web, combat);

            GodotPlayerMelee melee = player.Melee!;
            var scriptSelection = player.Animation.SourceScriptsSelected;
            var temporaryParent = new Node3D(); root.AddChild(temporaryParent);
            root.RemoveChild(player); temporaryParent.AddChild(player);
            Assert(ReferenceEquals(player.Melee, melee) && ReferenceEquals(player.Controller.Melee, melee) &&
                ReferenceEquals(player.Animation.SourceScriptsSelected, scriptSelection) && !anchor.IsDisposed &&
                web.Actions.State.CurrentStrand == currentWeb,
                "A live player reparent must preserve source melee, animation-sound selection, current CWeb and protected Swinger ownership.");
            temporaryParent.RemoveChild(player); root.AddChild(player); temporaryParent.Free();

            var partialRegistry = new GodotActorRegistry { Name = "PartialStartupSourceActors", Triggers = triggers,
                Environment = registry.Environment, Player = player,
                ControlServices = new(new OpenSpidey.Core.UI.CompassState(), sounds.Controller, Listener, () => clock),
                Sounds = sounds.Controller, SoundListener = Listener, VoicePlayback = registry.VoicePlayback,
                RandomSource = random.Next };
            root.AddChild(partialRegistry); partialRegistry.SetPhysicsProcess(false);
            partialRegistry.Spawn(triggers.Nodes[45]);
            GodotControlActor partialSuspended = partialRegistry.SourceActors(ActorUpdateGroup.Control).OfType<GodotControlActor>().Single();
            partialRegistry.SuspendActiveActors(45);
            partialRegistry.Spawn(triggers.Nodes[45]);
            GodotControlActor partialActive = partialRegistry.SourceActors(ActorUpdateGroup.Control).OfType<GodotControlActor>().Single();
            var partialDeletes = new List<(object Identity, bool Native)>();
            partialRegistry.ActorRemoved += actor => partialDeletes.Add((actor, false));
            foreach (GodotControlActor actor in new[] { partialActive, partialSuspended })
                actor.AddChild(new GodotNativeDestructionObserver { Destroyed = () => partialDeletes.Add((actor, true)) });
            Assert(partialRegistry.LiveCount == 2 && partialRegistry.BaddyConstructionState.Count == 2 &&
                partialRegistry.BaddyConstruction.OwnedCount == 2 && partialRegistry.SuspendedSourceList.Count == 1,
                "The partial-startup lifetime proof must allocate genuine active and suspended source203 bodies and both actual constructor leases.");
            partialActive.Free();
            Assert(partialRegistry.LiveCount == 1 && partialRegistry.BaddyConstructionState.Count == 1 &&
                partialRegistry.BaddyConstruction.OwnedCount == 1 && partialRegistry.SourceList(ActorUpdateGroup.Control).Count == 0 &&
                partialRegistry.SuspendedSourceList.SourceOrder.Single() == partialSuspended && partialRegistry.DeletedCount == 1,
                "Direct native body destruction must retire its canonical source identity, emit ActorRemoved and release its one actual count lease.");
            GodotScriptActor? failedActor = null;
            Action<GodotScriptActor> failStartup = actor =>
            {
                failedActor = actor;
                actor.AddChild(new GodotNativeDestructionObserver { Destroyed = () => partialDeletes.Add((actor, true)) });
                throw new InvalidOperationException("Original203 startup proof failure");
            };
            partialRegistry.Spawned += failStartup;
            try
            {
                partialRegistry.Spawn(triggers.Nodes[45]);
                throw new InvalidDataException("The real constructor-unwind proof did not reach its source startup failure.");
            }
            catch (InvalidOperationException exception) when (exception.Message == "Original203 startup proof failure") { }
            finally { partialRegistry.Spawned -= failStartup; }
            Assert(failedActor is not null && !GodotObject.IsInstanceValid(failedActor) && partialRegistry.LiveCount == 1 &&
                partialRegistry.BaddyConstructionState.Count == 1 && partialRegistry.BaddyConstruction.OwnedCount == 1 &&
                partialRegistry.SourceList(ActorUpdateGroup.Control).Count == 0 && partialRegistry.SuspendedSourceList.Count == 1,
                "A real203 allocation followed by startup failure must immediately run its native destructor and release its head/count ownership while existing shared services remain alive.");
            partialRegistry.Free();
            Assert(!GodotObject.IsInstanceValid(partialSuspended) && partialRegistry.LiveCount == 0 &&
                partialRegistry.BaddyConstructionState.Count == 0 && partialRegistry.BaddyConstruction.OwnedCount == 0 &&
                partialRegistry.SuspendedSourceList.Count == 0 && partialRegistry.DeletedCount == 2,
                $"Actual registry Predelete must destroy its remaining suspended source203 before disposing leases: " +
                $"body-valid={GodotObject.IsInstanceValid(partialSuspended)}, actors={partialRegistry.LiveCount}, " +
                $"leases={partialRegistry.BaddyConstructionState.Count}/{partialRegistry.BaddyConstruction.OwnedCount}, " +
                $"suspended-head={partialRegistry.SuspendedSourceList.Count}, deletes={partialRegistry.DeletedCount}.");
            (object Identity, bool Native)[] expectedPartialDeletes =
            // Direct Free deletes observer children in reverse creation order. Source-initiated
            // deletion first retires the actor, then invokes its actual native destructor.
            [(partialActive, true), (partialActive, false), (failedActor!, false), (failedActor!, true),
                (partialSuspended, false), (partialSuspended, true)];
            Assert(partialDeletes.Count == expectedPartialDeletes.Length && partialDeletes.Zip(expectedPartialDeletes,
                (actual, expected) => ReferenceEquals(actual.Identity, expected.Identity) && actual.Native == expected.Native).All(equal => equal),
                "Original203 partial-startup removal/native destruction order: " + string.Join(", ", partialDeletes.Select(entry =>
                    $"{(ReferenceEquals(entry.Identity, partialActive) ? "active" : ReferenceEquals(entry.Identity, failedActor) ? "failed" : "suspended")}/{(entry.Native ? "native" : "removed")}")));

            orphan.Lifecycle.Kill();
            registry.SourceList(ActorUpdateGroup.Environment).Update(); registry.SourceList(ActorUpdateGroup.Environment).Update();
            Assert(!registry.Owns(orphan) && GodotObject.IsInstanceValid(orphan) && orphan.IsQueuedForDeletion(),
                "Ordinary source40/80 AI deletion must leave one genuine detached native orphan pending Godot's queued destructor.");
            bool stopped = false; GodotPlayer? currentPlayer = player;
            var phases = new List<LevelSceneObjectPhase>();
            var nativeDeletes = new List<(object Identity, LevelSceneObjectPhase Phase)>();
            var sceneObjects = new GodotLevelSceneObjects(new()
            {
                Player = () => currentPlayer, Camera = camera, Registry = registry, Web = web, Combat = combat,
                Objects = objectRuntime, Enemies = enemies, Speech = speech, Messages = messages,
                StopProcessing = () => { stopped = true; root.SetProcess(false); root.SetPhysicsProcess(false); },
                ClearPlayer = () => currentPlayer = null
            });
            sceneObjects.PhaseStarted += phases.Add;
            Observe(player, LevelSceneObjectPhase.Player, () => Assert(anchor.IsDisposed && anchor.Strand.IsDisposed &&
                currentLine.IsDisposed && web.Strands.Owners.Count == 0,
                "Actual player native destruction must follow its protected Swinger, inner line, knot and CWeb destructors."));
            Observe(camera, LevelSceneObjectPhase.Camera);
            Observe(activeEnemy, LevelSceneObjectPhase.Enemy); Observe(control, LevelSceneObjectPhase.Control);
            Observe(vault, LevelSceneObjectPhase.Control); Observe(item, LevelSceneObjectPhase.Environment);
            Observe(orphan, LevelSceneObjectPhase.Environment); Observe(suspendedEnemy, LevelSceneObjectPhase.Suspended);
            foreach (GodotExplosionModel bullet in bullets) Observe(bullet, LevelSceneObjectPhase.Bullet);
            foreach (GodotWebDomeModel model in pieceModels) Observe(model, LevelSceneObjectPhase.Miscellaneous);
            object[] expectedNativeDeleteOrder =
                [player, camera, activeEnemy, vault, control, item, orphan, ..bullets, suspendedEnemy, ..pieceModels];
            var sourceHeads = Enum.GetValues<ActorUpdateGroup>().ToDictionary(group => group,
                group => registry.SourceList(group).SourceOrder.ToArray());
            object[] suspendedHead = registry.SuspendedSourceList.SourceOrder.ToArray();
            int allocatedActors = registry.LiveCount, allocatedLeases = registry.BaddyConstructionState.Count;
            var particleHeads = Enum.GetValues<SourceParticleList>().ToDictionary(list => list,
                list => combat.Particles.Identities(list).ToArray());
            int allocatedBits = combat.Particles.Count, glyphs = messages.Text.GlyphDrawCount;
            var effectCounts = (combat.Rocks.Count, combat.Impacts.Lines.Count, objectRuntime.Effects.Smoke.Count,
                combat.ItemShatter.ChunkCount, combat.ItemShatter.GlassCount);
            uint trapSound = sounds.Controller.PlayAt(33, player.Controller.Position, Listener());
            Assert(trapSound is not 0 and not uint.MaxValue && sounds.Controller.PlayerBank!.Resolve(33) is { Looped: true },
                "The cleanup proof must allocate the actual source33 trapping loop from the original player SFX bank.");
            web.Actions.State.TrapSoundHandle = unchecked((int)trapSound);
            int trapChannel = Enumerable.Range(1, 31).Single(channel => (trapSound & (1u << channel)) != 0);
            AudioStreamPlayer trapVoice = sounds.PlayerForChannel(trapChannel)!;
            ulong trapStream = trapVoice.Stream.GetInstanceId();
            viewport.RemoveChild(root);
            Assert(!root.IsInsideTree() && registry.LiveCount == allocatedActors &&
                registry.BaddyConstructionState.Count == allocatedLeases &&
                Enum.GetValues<ActorUpdateGroup>().All(group => registry.SourceList(group).SourceOrder.SequenceEqual(sourceHeads[group])) &&
                registry.SuspendedSourceList.SourceOrder.SequenceEqual(suspendedHead) && registry.Owns(activeEnemy) && registry.Owns(suspendedEnemy) &&
                combat.Directory.Contains(vault) && combat.Directory.SourceControls.Contains(vault) &&
                !anchor.IsDisposed && !currentLine.IsDisposed && web.Actions.State.CurrentStrand == currentWeb,
                "Whole-scene tree exit before Predelete must preserve every actual source head, active/suspended constructor lease,407 directory identity, CWeb and protected Swinger.");
            Assert(combat.Particles.Count == allocatedBits && Enum.GetValues<SourceParticleList>()
                .All(list => combat.Particles.Identities(list).SequenceEqual(particleHeads[list])) &&
                effectCounts == (combat.Rocks.Count, combat.Impacts.Lines.Count, objectRuntime.Effects.Smoke.Count,
                    combat.ItemShatter.ChunkCount, combat.ItemShatter.GlassCount) &&
                glyphs > 0 && messages.Text.GlyphDrawCount == glyphs && !messages.Text.ResourcesDisposed &&
                sounds.Controller.ReadChannel(trapChannel).Allocated && sounds.PlayerForChannel(trapChannel) == trapVoice &&
                trapVoice.Stream?.GetInstanceId() == trapStream,
                "Whole-scene exit must retain actual particle list identities, renderer owners, original message glyphs and source33 audio resources until their real teardown phases.");
            observing = true;
            sceneObjects.KillAllSceneObjects(LevelExitReason.RetryRequested);
            Assert(sceneObjects.IsDestroyed && currentPlayer is null && phases.SequenceEqual(Enum.GetValues<LevelSceneObjectPhase>()) &&
                registry.LiveCount == 0 && registry.BaddyConstructionState.Count == 0 && registry.SuspendedSourceList.Count == 0 &&
                Enum.GetValues<ActorUpdateGroup>().All(group => registry.SourceList(group).Count == 0) && combat.Particles.Count == 0 &&
                dome.IsDisposed && pieceModels.All(model => !GodotObject.IsInstanceValid(model)) &&
                bullets.All(model => !GodotObject.IsInstanceValid(model)) && !GodotObject.IsInstanceValid(orphan) &&
                !GodotObject.IsInstanceValid(web) && !GodotObject.IsInstanceValid(combat) && !GodotObject.IsInstanceValid(enemies) &&
                !GodotObject.IsInstanceValid(objectRuntime),
                "Init_KillAll must run all thirteen source phases and real destructors, clear constructor leases, queued orphan, protected pieces and scheduler registrations.");
            Assert(!sounds.Controller.ReadChannel(trapChannel).Allocated && sounds.PlayerForChannel(trapChannel) is null &&
                messages.Text.GlyphDrawCount == 0 && !messages.Text.ResourcesDisposed,
                "Player-phase destruction must stop the actual33 voice, and the final Messages phase must clear real glyph submissions before the retained font owner is destroyed.");
            Assert(nativeDeletes.Count == expectedNativeDeleteOrder.Length && nativeDeletes.Select(entry => entry.Identity)
                .Zip(expectedNativeDeleteOrder, ReferenceEquals).All(equal => equal) && messages.Messages.SourceMessages.Count == 0 &&
                messages.Messages.SourceSimpleMessages.Count == 0 && GodotObject.IsInstanceValid(retry) && !retry.Disabled &&
                GodotObject.IsInstanceValid(lifecycle) && lifecycle.ExitReason == LevelExitReason.RetryRequested &&
                GodotObject.IsInstanceValid(world) && world.GetChildren().Count > 0 && GodotObject.IsInstanceValid(speech) &&
                archive.Read("data/L1A3_T.trg").AsSpan().SequenceEqual(triggerData),
                "Actual source teardown must clear both message lists while retaining the working retry controls, speech cache, static world and original archive.");
            int deletes = sceneObjects.Deletions.Count, observedDeletes = nativeDeletes.Count;
            sceneObjects.KillAllSceneObjects(LevelExitReason.PlayerDied);
            Assert(sceneObjects.Deletions.Count == deletes && nativeDeletes.Count == observedDeletes &&
                sceneObjects.ExitReason == LevelExitReason.RetryRequested,
                "Repeated native teardown must preserve the first source reason and never run a deleting destructor twice.");
            observing = false;
            viewport.AddChild(root);
            await Frame();
            Assert(GodotObject.IsInstanceValid(retry) && !retry.Disabled && lifecycle.NavigationTask is null,
                "Deferred native cleanup must preserve the actual retry button until explicit scene navigation.");
            GD.Print($"Passed {checks} native Init_KillAll checks: genuine L1A3 ctor/list order, whole-scene tree exit, partial-startup native deletion, live player reparent, protected Swinger/dome, queued orphan, native bullets, all bit owners/messages and retained retry/world/archive.");

            T Spawn<T>(ushort id) where T : GodotScriptActor
            {
                registry.Spawn(triggers.Nodes[id]);
                return Enum.GetValues<ActorUpdateGroup>().SelectMany(registry.SourceActors).OfType<T>()
                    .Single(actor => actor.SourceNodeId == id);
            }
            void Observe(Node identity, LevelSceneObjectPhase expected, Action? beforeRecord = null)
            {
                identity.AddChild(new GodotNativeDestructionObserver { Destroyed = () =>
                {
                    if (!observing) return;
                    Assert(stopped && sceneObjects.CurrentPhase == expected,
                        $"The actual {identity.GetType().Name} native destructor must run in source phase {expected} after processing stops.");
                    beforeRecord?.Invoke(); nativeDeletes.Add((identity, expected));
                } });
            }
        }
        finally
        {
            observing = false;
            if (proofRoot is not null && GodotObject.IsInstanceValid(proofRoot))
            {
                SceneTree tree = owner.GetTree();
                GodotAudioShutdown.Resource[] resources = await GodotAudioShutdown.RetirePlaybackAsync(proofRoot, tree);
                proofRoot.Free();
                await GodotAudioShutdown.VerifyReleasedAsync(resources, tree);
            }
            viewport.Free();
        }
        void Assert(bool condition, string evidence) { checks++; if (!condition) throw new InvalidDataException(evidence); }
        async Task Frame() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        async Task Physics() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private static GodotActorWorldHit FindActualContact(GodotManipulationActor item, GodotActorWorldQuery world)
    {
        foreach (ImportedCollisionBody body in item.Colliders)
            foreach (var face in body.SourceTriangles)
            {
                FixedVector3 center = PsxCoordinates.OriginalWorldPosition(body.ToGlobal(PsxCoordinates.WorldPosition((face.A + face.B + face.C) / 3)));
                FixedVector3 normal = PsxCoordinates.RotateOriginalNormal(face.FixedNormal, body.SourceRotation);
                if (world.Trace(center + normal * 128, item.Body.Position) is { } hit &&
                    item.Colliders.Any(collider => ReferenceEquals(collider, hit.Collider))) return hit;
            }
        throw new InvalidDataException("The native destructor proof requires a genuine collision contact on its original401 OBJ.");
    }
    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child)) yield return descendant;
        }
    }
}
