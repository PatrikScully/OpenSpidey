using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Actual production enemy/combat/web composition in a separate original L1A1 native world.</summary>
internal static class EnemyRuntimeImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository,
        OriginalPlayerData original, Func<ImportedAnimatedModel> playerModelFactory,
        ReadOnlyMemory<byte> originalExecutable, PsxAssetRegions regions)
    {
        var viewport = new SubViewport { Name = "ProductionEnemyProof", OwnWorld3D = true, Size = new(1280, 720),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        var root = new Node3D(); viewport.AddChild(root);
        GodotEnemyRuntime? enemies = null;
        GodotPlayerWebRuntime? web = null;
        GodotPlayerCombatRuntime? combat = null;
        GodotActorRegistry? registry = null;
        GodotCombatSceneOwnership? sceneOwnership = null;
        int checks = 0;
        uint clock = 1000;
        try
        {
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A1_T.trg"));
            ImportedLevelWorld world = ImportedLevelWorld.Load(repository, "data/L1A1_G.psx"); root.AddChild(world);
            RestartPoint restart = new MissionCheckpoints(triggers).Select("Re_Restart01").Point;
            FixedVector3 initialPosition = new FixedVector3(restart.Position.X, restart.Position.Y, restart.Position.Z) << 12;
            var random = new GameRandom(0x12A6CC58);
            PlayerFollowCamera? sourceCamera = null;
            GodotSoundChannels sounds = owner.GetNode<GodotSoundChannels>("SoundChannels");
            GodotSpeechPlayer speech = owner.GetNode<GodotSpeechPlayer>("Speech");
            SoundListener Listener() => new((sourceCamera ?? throw new InvalidOperationException("Source camera is not yet constructed.")).SourcePosition >> 12);
            PsxAssetLibrary geometry = repository.RequireLoaded("data/L1A1_G.psx"), objects = repository.Load("data/L1A1_O.psx");
            var textures = repository.Load("data/L1A1_L.psx").Textures.Concat(geometry.Textures).Concat(objects.Textures).ToArray();
            registry = new GodotActorRegistry { Name = "ProductionSourceActors", Triggers = triggers,
                Difficulty = 1, SuspensionDistance = uint.MaxValue, RandomSource = random.Next,
                Environment = new(LevelId: 0x101), Sounds = sounds.Controller, SoundListener = Listener,
                VoicePlayback = (bank, track, priority) => speech.Play(bank, track, priority),
                ObjectLibrary = () => objects, ObjectTextures = () => textures, ClaimObjectModel = world.ClaimObjectModel,
                ShatterPlatform = (actor, seen, processed) => (combat ?? throw new InvalidOperationException("Production combat composition is incomplete.")).ShatterPlatform(actor, seen, processed) };
            root.AddChild(registry); registry.SetPhysicsProcess(false);
            var visibility = new GodotEnvironmentVisibility(() => world, repository);
            var visibilityLists = new GodotVisibilityBoxHost(registry, visibility, repository);
            MissionRuntime? mission = null;
            sceneOwnership = new(registry);
            enemies = GodotEnemyRuntime.Prepare(new()
            {
                Archive = archive, Repository = repository, OriginalExecutable = originalExecutable,
                Registry = registry, SceneOwnership = sceneOwnership, Sounds = sounds, Speech = speech,
                CurrentWorld = () => world, Regions = regions, EnvironmentVisibility = visibility, LightingState = new(),
                Clock = () => clock, AttackClock = () => unchecked((int)clock), TotalTime = () => unchecked((int)clock),
                TriggerCommandPoint = checksum => (mission ?? throw new InvalidOperationException("Original command-point owner is not ready.")).TriggerCommandPoint(checksum)
            });
            root.AddChild(enemies);
            var loader = new ModuleLoadCommandHandler(enemies.Modules);
            mission = new(triggers, registry, [loader]); registry.ReceivePulse = mission.ReceivePulse;
            Assert(registry.Player is null && !enemies.IsPlayerBound && ReferenceEquals(sceneOwnership.Actors.Registry, registry) &&
                enemies.Modules.Modules.SourceOrder.Count == 0,
                "Canonical real body identities and enemy module factories are prepared before CPlayer, without loading189 or fabricating a player.");
            bool earlyPlayerActionRejected = false;
            try { _ = enemies.Session.DomeCount; }
            catch (InvalidOperationException) { earlyPlayerActionRejected = true; }
            Assert(earlyPlayerActionRejected && enemies.NativeServices.RecoverActor(null) is null,
                "Player-dependent AI leaves are unavailable before genuine player binding, while null native player recovery retains its original absent-pointer meaning.");
            TriggerNode autoexec = triggers.Nodes[12];
            foreach (TriggerInstruction instruction in TriggerCommandReader.Read(autoexec).Where(instruction => instruction.Opcode == 189))
                loader.Execute(new(autoexec, 0), instruction);
            Assert(enemies.Modules.Modules.SourceOrder.Select(module => module.Name).Order().SequenceEqual(new[] { "blackcat", "thug" }),
                "Untouched L1A1 autoexec189 names initialize the genuine thug and Black Cat code modules.");
            ushort[] startup = [9, 10, 16, 19];
            foreach (ushort id in startup)
            {
                TriggerActorDefinition definition = TriggerActorDefinition.Read(triggers.Nodes[id]);
                Assert(definition.InitializeOnLoad && definition.ActorType == 312,
                    $"Node{id} retains the authored initial312 marker before central module construction.");
                registry.Spawn(triggers.Nodes[id]);
            }
            GodotThugActor[] thugs = startup.Select(id => registry.SourceActors(ActorUpdateGroup.Enemy)
                .OfType<GodotThugActor>().Single(actor => actor.SourceNodeId == id)).ToArray();
            var emptyOperands = new ActorScriptCursor(ReadOnlyMemory<byte>.Empty);
            Assert(registry.Player is null && !enemies.IsPlayerBound && registry.BaddyConstruction.State.Count == 4 &&
                thugs.All(actor => actor.SourceUpdates == 0 && actor.State.Health == 90 && actor.AttackDamage == 10 &&
                actor.RecoverPlayerBody() is null &&
                actor.Variables.Read(0x2132, emptyOperands) == 8191 && actor.Variables.Read(0x2133, emptyOperands) == 0 &&
                actor.Variables.Read(0x2150, emptyOperands) == 0 && actor.Variables.Read(0x2151, emptyOperands) == 0 &&
                actor.Variables.Read(0x2152, emptyOperands) == 0 && ReferenceEquals(sceneOwnership.Actors.RecoverNative(actor), actor)),
                "Untouched initial312 constructors parse health/damage/Stop with genuinely absent source player, exact no-player common variables and real shadow/directory identities before the first AI frame.");
            var prePlayerHandle = sceneOwnership.Actors.StoreHandle(thugs[0]);
            var prePlayerKickHandle = sceneOwnership.Directory.MakeHandle(thugs[0]);
            var prePlayerAppearances = thugs.Select(enemies.Visuals.AppearanceFor).ToArray();
            foreach (GodotThugActor thug in thugs) thug._Process(0);
            var player = GodotPlayer.Create(initialPosition, restart.Orientation.Y, playerModelFactory(),
                new(true, new(false, 10, true)), health: 100, random: random.Next);
            IReadOnlyList<ActorPartBox> boxes = PsxActorCollisionBoxes.Read(archive.Read("data/spidey.psx"));
            player.Combat.ConfigureCollisionBoxes(boxes);
            player.ConfigureHandVariants(original.FistVariants);
            root.AddChild(player); player.SetPhysicsProcess(false);
            player.Model.AdvanceAutomatically = false;
            var camera = new PlayerFollowCamera { Name = "ProductionEnemyCamera", Target = player, Current = true,
                SourceTotalTime = () => clock };
            root.AddChild(camera); camera.SetPhysicsProcess(false); camera.MakeCurrent();
            sourceCamera = camera;
            player.Combat.SourceTimer = () => clock;
            player.Combat.Sound = (sound, volume, _) => sounds.Controller.Play(sound, unchecked((short)volume));
            player.Combat.PositionSound = (sound, point, _) => sounds.Controller.PlayAt(sound, point, Listener());
            player.Combat.SoundStopped = handle => sounds.Controller.Stop(unchecked((uint)handle));
            player.Combat.XaStopped = speech.Controller.Stop;
            player.Combat.CameraBehind = camera.PutBehind;
            player.Combat.CameraField12C = camera.SetFocusHook;
            var flash = new GodotScreenFlash { Name = "ProductionEnemyHitFlash" }; root.AddChild(flash);
            player.Combat.FlashReset = flash.Reset;
            registry.Player = player;
            player.Inventory = new(new() { Webbing = 4096, Cartridges = 10 }, player, sounds.Controller,
                speech, Listener, () => registry.Difficulty, () => false, random.Next);
            combat = GodotPlayerCombatRuntime.Create(archive, repository, original, player, registry,
                () => world, camera, speech, () => clock, sceneOwnership);
            root.AddChild(combat); combat.SetPhysicsProcess(false);
            web = GodotPlayerWebRuntime.Create(archive, repository, player, combat, camera, () => clock,
                random.Next, (sound, position, _) => unchecked((int)sounds.Controller.PlayAt(sound, position, Listener())));
            root.AddChild(web);
            web.ConfigureDomes(registry, combat, repository, camera, () => clock, random.Next);
            enemies.BindPlayer(new()
            {
                Player = player, Camera = camera, Combat = combat, Web = web,
                HostageCamera = new GodotOptimumViewingCamera(triggers, combat.World, camera, visibilityLists)
            });
            Assert(enemies.IsPlayerBound && ReferenceEquals(combat.SceneOwnership, sceneOwnership) &&
                ReferenceEquals(enemies.Grenades.Registry, registry) && enemies.Session.DomeCount == web.Domes!.DomeCount &&
                ReferenceEquals(sceneOwnership.Actors.RecoverHandle(prePlayerHandle), thugs[0]) &&
                ReferenceEquals(sceneOwnership.Directory.Recover(ref prePlayerKickHandle), thugs[0]) &&
                prePlayerAppearances.SequenceEqual(thugs.Select(enemies.Visuals.AppearanceFor)),
                "Binding the actual post-census CPlayer/CCamera keeps the same pre-player native handles, directories and bit lists and connects real web/grenade owners.");
            bool rebindingRejected = false;
            try { enemies.BindPlayer(new() { Player = player, Camera = camera, Combat = combat, Web = web }); }
            catch (InvalidOperationException) { rebindingRejected = true; }
            Assert(rebindingRejected, "Source enemy player binding rejects a second initialization without replaying constructors or effects.");
            foreach (GodotThugActor thug in thugs)
                Assert(thug.Variables.Read(0x2150, emptyOperands) == unchecked((short)(player.Controller.Position.X >> 12)) &&
                    thug.Variables.Read(0x2151, emptyOperands) == unchecked((short)(player.Controller.Position.Y >> 12)) &&
                    thug.Variables.Read(0x2152, emptyOperands) == unchecked((short)(player.Controller.Position.Z >> 12)),
                    "The same source variable bank observes the real player only after the original player construction boundary.");
            player.ActorState.InvisibilityFlag = 1;
            Assert(registry.BaddyConstruction.State.Count == 4 && enemies.SourceThugs().SequenceEqual(thugs.Reverse()) &&
                combat.Actors.SourceBaddyBodies.Select(body => body.Identity).SequenceEqual(thugs.Reverse()) &&
                thugs.All(actor => actor.State.Health == 90 && actor.AttackDamage == 10 && actor.Model.ClipId == 0),
                "Actual module Spawn parses the four health90/damage10 scripts, shares constructor ordinals and preserves canonical newest-first BaddyList across production directories.");
            int[] firstWaypoints = [-1, -1, -1, -1];
            FixedVector3[] starts = thugs.Select(actor => actor.Position).ToArray();
            bool[] moved = new bool[thugs.Length];
            for (int update = 0; update < 180; update++)
            {
                await Step();
                for (int index = 0; index < thugs.Length; index++)
                {
                    GodotThugActor thug = thugs[index];
                    if (firstWaypoints[index] == -1 && thug.State.State != 0) firstWaypoints[index] = thug.Route.CurrentNode;
                    moved[index] |= thug.Position.X != starts[index].X || thug.Position.Z != starts[index].Z;
                    Assert(thug.FrameTiming.LastUpdateTick == clock && thug.FrameTiming.ElapsedTicks == 2 &&
                        thug.FrameTiming.AccumulatedTicks == (update + 1) * 2,
                        "Production actor timing follows the one shared source clock through native animation, physics and AI.");
                    thug.Model.VerifyCurrentPose();
                }
            }
            Assert(firstWaypoints.SequenceEqual(new[] { 7, 2, 15, 21 }) && moved.All(value => value),
                "Real production services execute every original initial patrol along its independently recorded first waypoint.");
            ActorVisibilityBoxImportChecks.Verify(registry, visibilityLists, thugs[0]);
            ActorLinkedBodyImportChecks.VerifyL1A2Constructor(owner, archive);
            ActorLinkedBodyImportChecks.VerifyProductionBodies(registry, thugs[0]);
            ActorLinkEventImportChecks.VerifyL1A2Platform(owner, archive);
            GodotThugActor target = thugs[0];
            short originalHealth = target.State.Health;
            target.State.Health = 50;
            Assert(web.Contacts.TrapWeb(target) && target.Messages.Head is { Code: 5, SenderHandle: null },
                "The production CBaddy trap leaf creates the original null-sender message5.");
            target.Messages.Head!.Flags |= 1; target.Messages.Cleanup();
            web.Contacts.AddTrap(target, 0);
            Assert(enemies.Session.RecoverWeb(target, false) is not null &&
                ReferenceEquals(enemies.Session.RecoverWeb(target, false), web.Contacts.RecoverContact(target, false)),
                "Genuine player web contact and enemy recovery share the same attached source trap identity and actual generation.");
            Node originalParent = target.GetParent();
            var reparent = new Node3D(); root.AddChild(reparent);
            originalParent.RemoveChild(target); reparent.AddChild(target);
            Assert(combat.Directory.Contains(target) && ReferenceEquals(combat.Actors.RecoverNative(target), target) &&
                ReferenceEquals(enemies.Session.RecoverWeb(target, false), web.Contacts.RecoverContact(target, false)) &&
                registry.BaddyConstruction.State.Count == 4,
                "Production reparent preserves the real body/count/directory and attached web contact without logical source deletion.");
            reparent.RemoveChild(target); originalParent.AddChild(target); reparent.Free();
            enemies.Session.RecoverWeb(target, false)!.Burst(); target.State.Health = originalHealth;
            enemies.Session.SetPlayerWebCombo(9, 7000);
            Assert(player.Combat.State.Field534 == 9 && player.Combat.State.Field52C == 7000 &&
                enemies.Session.PlayerWebComboValue == player.Combat.State.Field528,
                "Production web-combo actions write the real player528/52C/534 fields and retain their distinct original roles.");
            IProjectileBlastBody? head = enemies.Grenades.BlastHead(GrenadeDamageList.PrimaryTargets, GrenadeTargetList.Baddies);
            foreach (GodotThugActor thug in thugs.Reverse())
            {
                Assert(head is not null && head.Position == thug.Position,
                    "Production grenade blast uses live canonical BaddyList order and positions rather than copied private fixture bodies.");
                head = head!.NextBody;
            }
            Assert(head is null && enemies.Grenades.Configuration.ResolveRegion(enemies.Grenades.Configuration.ObjectFileRegion()).AssetPath.Equals(objects.AssetPath, StringComparison.OrdinalIgnoreCase),
                "Production grenade effects resolve the current original OBJ bank generation through the canonical source region allocator.");
            target.Neutralize(); target.Procedures.Execute(2); target.RunAnimation(0);
            player.ActorState.InvisibilityFlag = 0; player.Combat.HiddenParts = ~(1u << 5);
            var playerPose = new FixedJointTransform[18];
            for (int part = 0; part < playerPose.Length; part++) playerPose[part] = player.Model.GetSourceAnimationPartPose(part);
            ActorPartBox box = boxes[5];
            FixedVector3 ContactPoint(int x) => ActorHookPosition.Evaluate(player.Controller.Position,
                player.Controller.Position >> 12, player.Controller.Orientation.Basis, playerPose,
                new(checked((short)(x * 16)), checked((short)((box.UpperY + box.LowerY) * 16)),
                    checked((short)((box.UpperZ + box.LowerZ) * 16)), 5));
            PlaceHook(ContactPoint(box.UpperX * 2 + 100));
            target.StartAttackMonitor(0, 1, 100, 0); target.Procedures.Execute(2);
            Assert(player.Combat.State.Health == 100,
                "The genuine production attack monitor records its first posed hook without creating an invented immediate hit.");
            PlaceHook(ContactPoint(box.LowerX * 2 - 100)); target.Procedures.Execute(2);
            Assert(player.Combat.State.Health == 90 && player.Model.ClipId == 0xAA,
                "Real production enemy monitor, posed player box, impact/SFX/flash and Hit deliver authored damage10 and source clipAA.");
            registry.Spawn(triggers.Nodes[100]);
            GodotBlackCatActor cat = registry.SourceActors(ActorUpdateGroup.Enemy).OfType<GodotBlackCatActor>().Single();
            await Step();
            Assert(ReferenceEquals(combat.Actors.RecoverNative(cat), cat) && registry.BaddyConstruction.State.Count == 5 &&
                cat.FrameTiming.ElapsedTicks == 2 && (cat.State.BodyFlags & 4) == 0 && cat.Model.SourcePartCount >= 18,
                "The actual loaded Black Cat module constructs319 into the same production source count, directory, clock and original eighteen-part model.");
            enemies.Modules.Modules.Unload("blackcat");
            Assert(!GodotObject.IsInstanceValid(cat) && registry.BaddyConstruction.State.Count == 4,
                "Production module clear immediately destroys319 and releases its genuine constructor lease.");
            enemies.Modules.Modules.Unload("thug");
            Assert(thugs.All(actor => !GodotObject.IsInstanceValid(actor)) && registry.BaddyConstruction.State.Count == 0 &&
                enemies.SourceThugs().Count == 0 && combat.Directory.SourceBodies.Count == 0 && enemies.Modules.Modules.SourceOrder.Count == 0,
                "Production module clear retires all actual312 identities, shared body views, attached web contacts and base counts exactly once.");
            enemies.Modules.LoadCodeModule("thug");
            registry.Spawn(triggers.Nodes[9]);
            GodotThugActor cleanupThug = enemies.SourceThugs().Single();
            Assert(enemies.Modules.Modules.IsLoaded("thug") && registry.BaddyConstruction.State.Count == 1,
                "Source162 proof must allocate a genuine reloaded module and actual312 before the fixed unload-all prepass.");
            enemies.Modules.UnloadAllCodeModules();
            Assert(!GodotObject.IsInstanceValid(cleanupThug) && registry.BaddyConstruction.State.Count == 0 &&
                enemies.Modules.Modules.SourceOrder.Count == 0 && combat.Directory.SourceBodies.Count == 0,
                "Actual Reloc_UnloadAll destroys the selected active baddy family before module clear/unlink and releases its real directory/count owners.");
            GD.Print($"Passed {checks} native production enemy checks: genuine pre-player189/initial312 construction and once-only actual player binding, shared clocks/directories, real patrol/model/culling/web/grenade services and319 lifetime.");

            void PlaceHook(FixedVector3 position)
            {
                target.Position = default; target.Actor.Body.Angles = default; target.BuildTransform();
                target.Position = position - target.GetActorHookPosition(new(0, 0, 0, 0)); target.BuildTransform();
            }

            async Task Step()
            {
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                clock = unchecked(clock + 2);
                foreach (ActorUpdateGroup group in Enum.GetValues<ActorUpdateGroup>()) registry.SourceList(group).Update();
                registry.SuspendedSourceList.Update();
                combat.Particles.Move(clock); combat.Particles.RemoveDead();
                enemies.SynchronizeSourceLighting();
                camera._PhysicsProcess(1.0 / 60); camera._PhysicsProcess(1.0 / 60);
            }
        }
        finally
        {
            // Destroy bodies while all real directories, web contacts and native visual owners still exist.
            if (registry is not null && GodotObject.IsInstanceValid(registry))
                foreach (GodotScriptActor actor in Enum.GetValues<ActorUpdateGroup>().SelectMany(registry.SourceActors).ToArray())
                    if (registry.Owns(actor)) registry.DestroyImmediately(actor);
            if (enemies is not null && GodotObject.IsInstanceValid(enemies)) enemies.Free();
            if (web is not null && GodotObject.IsInstanceValid(web)) web.Free();
            if (combat is not null && GodotObject.IsInstanceValid(combat)) combat.Free();
            viewport.Free();
            sceneOwnership?.Dispose();
        }
        void Assert(bool condition, string evidence)
        { if (!condition) throw new InvalidDataException($"Native production enemy: {evidence}"); checks++; }
    }

}
