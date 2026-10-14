using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;
using OpenSpidey.Game.Actors;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Player.Scripting;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Assets.Audio;
using OpenSpidey.Core.Audio;
using OpenSpidey.Game.UI;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Cinematics;

namespace OpenSpidey.Game.Gameplay;

/// <summary>Loads a real original level and player. Mission execution is connected as its actor systems are ported.</summary>
public partial class LevelSession : Node3D
{
    private const int InitialDifficulty = 2;
    private GodotPlayer? player;
    private Label status = null!;
    private RestartPoint restart = null!;
    private bool smoke;
    private bool checkpointSmoke;
    private bool transitionSmoke;
    private bool saveSmoke;
    private bool teardownSmoke;
    private GodotMissionLevelTransitions? levelTransitions;
    private GodotLevelSceneObjects? sceneObjects;
    private int ticks;
    private FixedVector3 initialPosition;
    private bool verifyFirstRoof;
    private PlayerEffectTextures? effectTextures;
    private PkrArchive? archive;
    private GodotMissionLibraries libraries = null!;
    private ControlActorImportChecks? controlActorChecks;
    private GodotScreenFlash flash = null!;
    private IReadOnlyList<ActorPartBox> playerCollisionBoxes = [];
    private Func<ImportedAnimatedModel> playerModelFactory = null!;
    private GodotPlayerCombatRuntime? combatRuntime;
    private GodotPlayerWebRuntime? webRuntime;
    private GodotLevelObjectRuntime? objectRuntime;
    private GodotEnemyRuntime? enemyRuntime;
    private PsxAssetRegions? assetRegions;
    private readonly SourceVertexLightingState actorLighting = new();
    private OriginalPlayerData originalPlayerData = null!;
    private GodotRenderEnvironment? renderEnvironment;
    private GodotEnvironmentVisibility environmentVisibility = null!;
    private MissionCheckpointSession checkpoints = null!;
    private GodotMissionMessages messages = null!;
    private ReadOnlyMemory<byte> originalExecutable;

    public override void _Ready()
    {
        string[] arguments = OS.GetCmdlineUserArgs();
        string[] componentChecks = ["--smoke-player", "--smoke-checkpoint", "--smoke-mission-transition",
            "--smoke-save", "--smoke-scene-teardown"];
        if (arguments.Contains("--smoke-mission-startup") || !arguments.Any(componentChecks.Contains))
        {
            AddChild(new StoryLevelSession { Name = "StoryMission" });
            SetProcess(false); SetPhysicsProcess(false);
            return;
        }
        // NPC culling runs first; the shared colour scratch then follows source actor order.
        ProcessPriority = 100;
        smoke = OS.GetCmdlineUserArgs().Contains("--smoke-player");
        checkpointSmoke = OS.GetCmdlineUserArgs().Contains("--smoke-checkpoint");
        transitionSmoke = OS.GetCmdlineUserArgs().Contains("--smoke-mission-transition");
        saveSmoke = OS.GetCmdlineUserArgs().Contains("--smoke-save");
        teardownSmoke = OS.GetCmdlineUserArgs().Contains("--smoke-scene-teardown");
        var overlay = new CanvasLayer();
        status = new Label { Position = new(20, 20) };
        overlay.AddChild(status);
        AddChild(overlay);
        try
        {
            if (!System.Environment.Is64BitProcess) throw new PlatformNotSupportedException("A 64-bit runtime is required.");
            string directory = System.Environment.GetEnvironmentVariable("OPENSPIDEY_ASSETS")
                ?? throw new InvalidOperationException("Supply your original game folder with --assets.");
            GameSessionState application = GetTree().Root.GetNode<GameSessionState>("GameSession");
            string asset = application.SceneAsset ?? System.Environment.GetEnvironmentVariable("OPENSPIDEY_SCENE") ?? "data/L1A1_G.psx";
            if (!asset.EndsWith("_G.psx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The level session requires a _G.psx geometry file.");
            if ((checkpointSmoke || transitionSmoke) && !asset.Equals("data/L1A1_G.psx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The checkpoint reload smoke uses the authored L1A1 checkpoints.");
            archive = new PkrArchive(System.IO.Path.Combine(directory, "data.pkr"));
            originalExecutable = System.IO.File.ReadAllBytes(System.IO.Path.Combine(directory, "SpideyPC.exe"));
            originalPlayerData = OriginalPlayerData.Read(originalExecutable.Span);
            OriginalLevelCatalog levelCatalog = CampaignCompletionImportChecks.Verify(originalExecutable);
            MissionCheckpointSave savedCheckpoint = application.OpenLevel(asset,
                System.Environment.GetEnvironmentVariable("OPENSPIDEY_RESTART") ?? "Re_Restart01");
            byte[] triggerData = archive.Read("data/" + application.MissionName + ".trg");
            TriggerFile triggers = TriggerFile.Read(triggerData);
            OriginalMissionMessageData messageData = OriginalMissionMessageData.Read(originalExecutable);
            messages = GodotMissionMessages.Create(messageData,
                OriginalBitmapFontReader.Read(archive.Read("data/sp_fnt00.fnt"), "sp_fnt00.fnt"));
            AddChild(messages);
            checkpoints = new(triggers, savedCheckpoint, messages);
            libraries = new GodotMissionLibraries { Repository = new PsxLibraryRepository(archive), Name = "MissionLibraries" };
            AddChild(libraries);
            assetRegions = new(libraries.Repository);
            PreloadMissionLibraries(triggers);
            if (libraries.World is null) libraries.LoadEnvironment(asset);
            ImportedLevelWorld world = libraries.World!;
            environmentVisibility = new(() => libraries.World ??
                throw new InvalidOperationException("No level world is loaded."), libraries.Repository,
                (source, index) => objectRuntime?.Environment.Items.FirstOrDefault(item =>
                    item.SourceAsset.Equals(source.AssetPath, StringComparison.OrdinalIgnoreCase) && item.SourceInstanceIndex == index));
            restart = checkpoints.Select(savedCheckpoint.RestartPointName).Point;
            verifyFirstRoof = asset.Equals("data/L1A1_G.psx", StringComparison.OrdinalIgnoreCase) && restart.NodeId == 14;
            initialPosition = new FixedVector3(restart.Position.X, restart.Position.Y, restart.Position.Z) << 12;
            PsxAssetLibrary playerLibrary = libraries.Repository.Load("spidey");
            var animations = playerLibrary.Animations as CompressedAnimationLibrary
                ?? throw new InvalidDataException("The player animation library is missing.");
            playerModelFactory = () => ImportedAnimatedModel.Build(playerLibrary.Scene,
                playerLibrary.Textures, animations, playerPartOrder: true);
            ImportedAnimatedModel model = playerModelFactory();
            playerCollisionBoxes = PsxActorCollisionBoxes.Read(archive.Read(playerLibrary.AssetPath));
            // Initial PC policies at 0x54EC7C/80/84 and 0x60F894; level-specific changes remain to be ported.
            effectTextures = PlayerEffectTextures.Load(archive);
            var random = new GameRandom(0x12A6CC58); // Original forced-level startup seed.
            PlayerStartingResources resources = PlayerWebInventoryRules.StartingResources(InitialDifficulty, 0,
                MissionLevelId.FromAssetName(asset), application.CarriedHistory.Saved.Webbing, application.CarriedHistory.Saved.Cartridges);
            player = GodotPlayer.Create(initialPosition, restart.Orientation.Y, model, new(true, new(false, 10, true)), effectTextures,
                health: resources.Health, random: random.Next);
            player.Combat.ConfigureCollisionBoxes(playerCollisionBoxes);
            player.ConfigureHandVariants(originalPlayerData.FistVariants);
            AddChild(player);
            var sounds = new GodotSoundChannels { Name = "SoundChannels", Archive = archive,
                LevelGroup = SoundCatalog.Find(asset[..^6]) };
            AddChild(sounds);
            sounds.Controller.PlayerBank = SoundDefinitionBank.Read(archive.Read("data/spidey.sfx"));
            sounds.Controller.LevelBank = SoundDefinitionBank.Read(archive.Read(asset[..^6] + ".sfx"), levelSpecific: true);
            var playerSounds = new GodotPlayerSoundEffects { Name = "PlayerSoundEffects", Backend = sounds };
            AddChild(playerSounds);
            player.SoundOutput = playerSounds.Play;
            var camera = new PlayerFollowCamera { Name = "PlayerCamera", Target = player, SourceTotalTime = () => unchecked((uint)ticks) };
            AddChild(camera);
            player.CameraHeading = () => camera.SourceHeading;
            renderEnvironment = GodotRenderEnvironment.Create(libraries,
                () => libraries.ObjectLibrary ?? throw new InvalidOperationException("No backdrop object library is selected."),
                libraries.ObjectTextures, () => camera, () => player.Controller.SourceTimeTicks);
            AddChild(renderEnvironment);
            renderEnvironment.Backdrops.SourceModelClaimed += (path, index) => libraries.World?.ClaimObjectModel(path, index);
            PrepareRenderSettings(triggers);
            player.Scripts = new(player, triggers, () => new PlayerScriptEnvironment(false, false,
                MissionLevelId.FromAssetName(asset), player.Controller.Position));
            var speech = new GodotSpeechPlayer { Name = "Speech", CacheDirectory = System.Environment.GetEnvironmentVariable("OPENSPIDEY_MEDIA")
                ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".local/share/openspidey/media") };
            AddChild(speech);
            player.Scripts.VoicePlayback = (bank, slot, priority) => speech.Play(bank, slot, priority);
            var actors = new GodotActorRegistry { Name = "Actors", Triggers = triggers, Player = player,
                Difficulty = InitialDifficulty,
                RandomSource = random.Next,
                VoicePlayback = (bank, slot, priority) => speech.Play(bank, slot, priority),
                Sounds = sounds.Controller,
                SoundListener = () => new(camera.SourcePosition >> 12),
                ObjectLibrary = () => libraries.ObjectLibrary ?? throw new InvalidOperationException("No object library is selected."),
                ClaimObjectModel = (path, modelIndex) => libraries.World?.ClaimObjectModel(path, modelIndex),
                ObjectTextures = libraries.ObjectTextures,
                ShatterPlatform = (actor, checkSeen, markProcessed) =>
                    (combatRuntime ?? throw new InvalidOperationException("Platform shatter runtime is not ready."))
                        .ShatterPlatform(actor, checkSeen, markProcessed),
                Environment = new(LevelId: MissionLevelId.FromAssetName(asset)) };
            AddChild(actors);
            player.Inventory = new(new() { Webbing = resources.Webbing, Cartridges = resources.Cartridges }, player,
                sounds.Controller, speech, () => new(camera.SourcePosition >> 12), () => actors.Difficulty,
                () => false, random.Next); // Original initial consumption-disable global is zero.
            var hudLayer = new CanvasLayer { Name = "GameplayHud", Layer = 20 };
            AddChild(hudLayer);
            GodotPlayerHud hud = GodotPlayerHud.Create(archive, player, () => unchecked((uint)ticks));
            hud.Countdown = application.Countdown;
            hudLayer.AddChild(hud);
            hud.Timer = GodotMissionTimerPresenter.Create(archive, application.Countdown, application.TimerDisplay, hud.Text, new()
            {
                LevelId = () => MissionLevelId.FromAssetName(asset),
                PlayerPresent = () => GodotObject.IsInstanceValid(player),
                PlayerScriptGate = () => player.Combat.State.FieldE18,
                PlayerSynthesizedInput = () => player.Combat.State.SynthesizedInputActive,
                Random = random.Next,
                PlaySound = (sound, volume, pitch) => player.Combat.PlaySound(sound, volume, pitch),
                CallModule = (module, function) => throw new NotSupportedException(
                    $"Timer display requires the unported mission module {module}, function {function}.")
            });
            hud.AddChild(hud.Timer);
            flash = new GodotScreenFlash { Name = "ScreenFlash" };
            AddChild(flash);
            var lifecycle = new GodotLevelLifecycle { Name = "LevelLifecycle" };
            lifecycle.Restarting += reason =>
            {
                if (reason == LevelExitReason.RestartFromBeginning) application.RestartFromBeginning();
            };
            AddChild(lifecycle);
            ConnectPlayerCombat(sounds, speech, camera, lifecycle);
            combatRuntime = GodotPlayerCombatRuntime.Create(archive, libraries.Repository, originalPlayerData, player, actors,
                () => libraries.World ?? throw new InvalidOperationException("No level world is loaded."),
                camera, speech, () => unchecked((uint)ticks));
            AddChild(combatRuntime);
            objectRuntime = GodotLevelObjectRuntime.Create(archive, libraries.Repository, world, actors, combatRuntime,
                sounds, camera, application.Countdown, triggerData, () => unchecked((uint)ticks), environmentVisibility);
            AddChild(objectRuntime);
            webRuntime = GodotPlayerWebRuntime.Create(archive, libraries.Repository, player, combatRuntime, camera,
                () => unchecked((uint)ticks), random.Next,
                (sound, position, _) => unchecked((int)sounds.Controller.PlayAt(sound, position, new(camera.SourcePosition >> 12))));
            AddChild(webRuntime);
            webRuntime.ConfigureDomes(actors, combatRuntime, libraries.Repository, camera, () => unchecked((uint)ticks), random.Next);
            var screenTarget = new GodotScreenTarget { Name = "LookaroundTarget", Camera = camera };
            hudLayer.AddChild(screenTarget);
            var lookaround = new GodotPlayerLookaroundRuntime(player, webRuntime, combatRuntime, camera, screenTarget, new()
            {
                PracticeMode = () => webRuntime.PracticeDifficulty != 0,
                InvertPitch = () => false,
                SwingWebPresent = () => webRuntime.Swing?.SwingPresent == true,
                BeginZip = player.Controller.BeginLookaroundZip,
                BeginSwing = _ => player.Controller.ApplyCombatState()
            });
            webRuntime.ConfigureSwing(camera, combatRuntime, lookaround.Targets);
            player.Combat.LookaroundExited = lookaround.Camera.ExitLookaroundMode;
            player.Controller.Lookaround = lookaround;
            player.Controller.Web = webRuntime;
            var visibilityLists = new GodotVisibilityBoxHost(actors, environmentVisibility, libraries.Repository);
            MissionRuntime? mission = null;
            MissionEventPump? events = null;
            enemyRuntime = GodotEnemyRuntime.Create(new()
            {
                Archive = archive, Repository = libraries.Repository, OriginalExecutable = originalExecutable,
                Registry = actors, Combat = combatRuntime, Web = webRuntime, Sounds = sounds, Speech = speech, Camera = camera,
                CurrentWorld = () => libraries.World ?? throw new InvalidOperationException("No level world is loaded."),
                Regions = assetRegions, EnvironmentVisibility = environmentVisibility, LightingState = actorLighting,
                Clock = () => unchecked((uint)ticks), AttackClock = () => ticks, TotalTime = () => ticks,
                TriggerCommandPoint = checksum => (mission ?? throw new InvalidOperationException("Mission startup is not ready.")).TriggerCommandPoint(checksum),
                HostageCamera = new GodotOptimumViewingCamera(triggers, combatRuntime.World, camera, visibilityLists)
            });
            AddChild(enemyRuntime);
            var cinema = new GodotCinemaPlayer { Name = "MissionCinema", CacheDirectory = speech.CacheDirectory, PauseGameplay = true };
            AddChild(cinema);
            var campaignFade = new GodotCampaignFade { Name = "CampaignFade" };
            AddChild(campaignFade);
            var saves = GodotSaveConfirmation.Create(application, new GameSaveStore(), sounds.Controller);
            AddChild(saves);
            var completion = new GodotCampaignCompletion(application, levelCatalog, campaignFade, saves);
            sceneObjects = new(new()
            {
                Player = () => player, Camera = camera, Registry = actors, Web = webRuntime,
                Combat = combatRuntime, Objects = objectRuntime, Enemies = enemyRuntime,
                Speech = speech, Messages = messages, ClearPlayer = () => { player = null; actors.Player = null; },
                StopProcessing = StopGameplay
            });
            levelTransitions = new(application, lifecycle, new()
            {
                Archive = archive, Player = () => player,
                KillAllSceneObjects = sceneObjects.KillAllSceneObjects,
                CompleteLevel = completion.CompleteAsync
            });
            var missionHandlers = SceneLibraryCommandHandler.CreateHandlers(libraries)
                .Concat(RenderEnvironmentCommandHandler.CreateHandlers(renderEnvironment)).Concat(new IMissionCommandHandler[] {
                new ActorSignalCommandHandler(MissionOpcode.SendSignal, triggers, actors),
                new ActorSignalCommandHandler(MissionOpcode.SendSuspend, triggers, actors),
                new ActorSignalCommandHandler(MissionOpcode.SendActivate, triggers, actors) })
                .Concat(CameraCommandHandler.CreateHandlers(camera.ScriptRig, () => new(player.Controller.OnWall,
                    player.Controller.OnCeiling, false, (uint)player.Controller.Action)))
                .Concat(FixedCameraCommandHandler.CreateHandlers(triggers, camera.ScriptRig, () => camera.SourcePosition,
                    () => camera.SourceRotation))
                .Append(new PlayerScriptCommandHandler(player.Scripts.Start))
                .Append(new PlayerInputCommandHandler(duration => player.Controller.SetIgnoreInputTimer(duration)))
                .Append(new EnvironmentVisibilityCommandHandler(environmentVisibility))
                .Append(new LinkedEnvironmentVisibilityCommandHandler(triggers, environmentVisibility))
                .Append(new GameLevelCommandHandler(application))
                .Append(new DualBufferSizeCommandHandler())
                .Append(new FightMusicTimeCommandHandler(player.Combat))
                .Append(new ModuleLoadCommandHandler(enemyRuntime.Modules))
                .Append(new RunCinemaCommandHandler(cinema))
                .Concat(LevelTransitionCommandHandler.CreateHandlers(levelTransitions))
                .Concat(new ushort[] { 141, 192, 209 }.Select(opcode => new VisibilityBoxCommandHandler(opcode, visibilityLists)))
                .Append(new ActorSuspensionDistanceCommandHandler(distance => actors.SuspensionDistance = unchecked((uint)distance)))
                .Concat(checkpoints.CreateHandlers())
                .Concat(MissionTextCommandHandler.CreateHandlers(messages, messageData,
                    new GodotMissionControlNames(PlayerControls.SourceActionName)))
                .Concat(SoundCommandHandler.CreateHandlers(sounds.Controller))
                .Concat(SpeechCommandHandler.CreateHandlers(speech.Controller, speech.Environment));
            mission = new MissionRuntime(triggers, actors, missionHandlers);
            mission.CommandListCompleted += _ => renderEnvironment.EndCommandList();
            actors.ReceivePulse = mission.ReceivePulse;
            actors.Faulted += exception =>
            {
                status.Text = "Actor execution stopped: " + exception.Message;
                player.SetPhysicsProcess(false);
                combatRuntime?.SetPhysicsProcess(false);
                SetPhysicsProcess(false);
                if (smoke) GetTree().Quit(1);
            };
            events = new MissionEventPump { Name = "MissionEvents", Runtime = mission };
            events.SourceFrameCompleted += () => messages.UpdateSourceTimer(ticks);
            events.Faulted += exception =>
            {
                status.Text = "Mission execution stopped: " + exception.Message;
                player.SetPhysicsProcess(false);
                actors.SetPhysicsProcess(false);
                combatRuntime?.SetPhysicsProcess(false);
                SetPhysicsProcess(false);
                if (smoke) GetTree().Quit(1);
            };
            AddChild(events);
            lifecycle.Exited += _ => StopGameplay();
            combatRuntime.Faulted += exception =>
            {
                status.Text = "Player combat effects stopped: " + exception.Message;
                player.SetPhysicsProcess(false);
                actors.SetPhysicsProcess(false);
                events.SetPhysicsProcess(false);
                SetPhysicsProcess(false);
                GD.PushError(exception.ToString());
                if (smoke) GetTree().Quit(1);
            };
            player.Faulted += exception =>
            {
                status.Text = "Player script/physics execution stopped: " + exception.Message;
                speech.Controller.Stop();
                actors.SetPhysicsProcess(false);
                events.SetPhysicsProcess(false);
                combatRuntime?.SetPhysicsProcess(false);
                SetPhysicsProcess(false);
                if (smoke) GetTree().Quit(1);
            };
            speech.Faulted += exception =>
            {
                status.Text = "Speech execution stopped: " + exception.Message;
                player.SetPhysicsProcess(false);
                actors.SetPhysicsProcess(false);
                events.SetPhysicsProcess(false);
                combatRuntime?.SetPhysicsProcess(false);
                SetPhysicsProcess(false);
                if (smoke) GetTree().Quit(1);
            };
            if (smoke && verifyFirstRoof) controlActorChecks = new(actors, mission, camera);
            GD.Print($"Loaded level session: {asset}, restart {restart.Name}, {world.InstanceCount} instances in {world.Sources.Count} source libraries, 64-bit Godot.");
            if (smoke)
            {
                player.SampleInput = () => ticks is >= 40 and < 80
                    ? new(true, new(-127, 0), false, false, false, false, false, false) : default;
                player.SampleButtons = () => default;
            }
            if (checkpointSmoke)
            {
                player.SampleInput = () => default;
                player.SampleButtons = () => default;
                player.SetPhysicsProcess(false);
                Callable.From(() => VerifyCheckpointReload(application, lifecycle)).CallDeferred();
            }
            if (transitionSmoke)
            {
                player.SampleInput = () => default;
                player.SampleButtons = () => default;
                player.SetPhysicsProcess(false);
                Callable.From(() => VerifyMissionTransition(application, lifecycle)).CallDeferred();
            }
            if (saveSmoke)
            {
                player.SampleInput = () => default;
                player.SampleButtons = () => default;
                player.SetPhysicsProcess(false);
                Callable.From(() => VerifySaveGame(application, sounds.Controller)).CallDeferred();
            }
            if (teardownSmoke)
            {
                player.SampleInput = () => default;
                player.SampleButtons = () => default;
                player.SetPhysicsProcess(false);
                Callable.From(VerifySceneTeardown).CallDeferred();
            }

            void StopGameplay()
            {
                player?.SetPhysicsProcess(false);
                actors.SetPhysicsProcess(false);
                events?.SetPhysicsProcess(false);
                speech.SetPhysicsProcess(false);
                combatRuntime?.SetPhysicsProcess(false);
                speech.Controller.Stop();
                renderEnvironment.ProcessMode = ProcessModeEnum.Disabled;
                hudLayer.ProcessMode = ProcessModeEnum.Disabled;
                hudLayer.Hide();
                status.Hide();
                SetProcess(false);
                SetPhysicsProcess(false);
            }
        }
        catch (Exception exception)
        {
            status.Text = "Could not load level: " + exception.Message;
            player?.SetPhysicsProcess(false);
            combatRuntime?.SetPhysicsProcess(false);
            SetPhysicsProcess(false);
            GD.PushError(exception.ToString());
            if (smoke || checkpointSmoke || transitionSmoke || saveSmoke || teardownSmoke) GetTree().Quit(1);
        }
    }

    public override void _ExitTree()
    {
        levelTransitions?.Dispose();
        assetRegions?.Dispose();
        effectTextures?.Dispose();
        archive?.Dispose();
    }

    public override void _Process(double delta) => enemyRuntime?.SynchronizeSourceLighting();

    private async void VerifySceneTeardown()
    {
        try
        {
            await LevelSceneObjectImportChecks.VerifyAsync(this, archive!, libraries.Repository, originalPlayerData,
                CreateVerificationPlayer, originalExecutable, assetRegions!);
            await PlayerStartOrientationImportChecks.VerifyAsync(this, archive!, CreateVerificationPlayer);
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private async void VerifySaveGame(GameSessionState application, SoundChannelController sounds)
    {
        try
        {
            await SaveGameImportChecks.VerifyAsync(this, application, sounds, archive!);
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private async void VerifyMissionTransition(GameSessionState application, GodotLevelLifecycle lifecycle)
    {
        try
        {
            await MissionLevelTransitionsImportChecks.VerifyAsync(this, application, levelTransitions!, lifecycle,
                checkpoints, player!, archive!);
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private async void VerifyCheckpointReload(GameSessionState application, GodotLevelLifecycle lifecycle)
    {
        try { await CheckpointReloadImportChecks.VerifyAsync(this, application, checkpoints, lifecycle, player!); }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private void PreloadMissionLibraries(TriggerFile triggers)
    {
        var handlers = SceneLibraryCommandHandler.CreateHandlers(libraries).ToDictionary(handler => handler.Opcode);
        // Prepare the original resource requests; this is not execution of the still-unported story commands.
        foreach (TriggerNode node in triggers.Nodes.Where(node => node.TypeCode == (ushort)TriggerNodeKind.Autoexec))
            foreach (TriggerInstruction instruction in TriggerCommandReader.Read(node))
                if (handlers.TryGetValue(instruction.Opcode, out var handler)) handler.Execute(new(node, 0), instruction);
    }

    private void PrepareRenderSettings(TriggerFile triggers)
    {
        var handlers = RenderEnvironmentCommandHandler.CreateHandlers(renderEnvironment!)
            .Append(new EnvironmentVisibilityCommandHandler(environmentVisibility)).ToDictionary(handler => handler.Opcode);
        IEnumerable<TriggerNode> lists = triggers.Nodes.Where(node => node.TypeCode == (ushort)TriggerNodeKind.Autoexec)
            .Append(triggers.Nodes[restart.NodeId]);
        foreach (TriggerNode node in lists)
        {
            // Prepare authored rendering while full story execution is still being connected.
            foreach (TriggerInstruction command in TriggerCommandReader.Read(node))
                if (handlers.TryGetValue(command.Opcode, out IMissionCommandHandler? handler)) handler.Execute(new(node, 0), command);
            renderEnvironment!.EndCommandList();
        }
    }

    private void ConnectPlayerCombat(GodotSoundChannels sounds, GodotSpeechPlayer speech,
        PlayerFollowCamera camera, GodotLevelLifecycle lifecycle)
    {
        GodotPlayerCombat combat = player!.Combat;
        combat.SourceTimer = () => unchecked((uint)ticks);
        combat.CameraFlag = () => camera.TerrainObstructed;
        // The recovered PC playSFX ignores pitch_offset; preserve the source channel mix.
        combat.Sound = (sound, volume, _) => sounds.Controller.Play(sound, unchecked((short)volume));
        combat.PositionSound = (sound, position, _) => sounds.Controller.PlayAt(sound, position, new(camera.SourcePosition >> 12));
        combat.SoundStopped = handle => sounds.Controller.Stop(unchecked((uint)handle));
        combat.XaStopped = () =>
        {
            if (combat.SpeechAttachment.CanStop(unchecked((uint)ticks), speech.Controller.Current)) speech.Controller.Stop();
        };
        combat.CameraBehind = camera.PutBehind;
        combat.CameraField12C = camera.SetFocusHook;
        combat.LevelStatus = lifecycle.RequestExit;
        combat.InitializationReset = lifecycle.ResetInitialization;
        combat.FlashReset = flash.Reset;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (player is null) return;
        ticks++;
        flash.Update();
        status.Text = smoke ? $"{restart.Name} · {player.Controller.Action} · Clip {player.Model.ClipId}" : "";
        if (!smoke) return;
        try { controlActorChecks?.Verify(); }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
            SetPhysicsProcess(false);
            return;
        }
        if (ticks == 31)
        {
            GD.Print($"Player contact after 15 source updates: position={player.Controller.Position}, grounded={player.Controller.IsGrounded}, action={player.Controller.Action}.");
            if (verifyFirstRoof && (!player.Controller.IsGrounded ||
                !string.Equals(player.Controller.LastTerrainStep.Fall?.Hit?.SourceAsset,
                    "data/L1A1_O.psx", StringComparison.OrdinalIgnoreCase) ||
                player.Controller.Position.Y != (-1474 << 12)))
            {
                GD.PushError("The canonical first-level spawn did not land on its original object-library roof.");
                GetTree().Quit(1);
                SetPhysicsProcess(false);
                return;
            }
        }
        if (player.SourceUpdates < 70 || controlActorChecks?.Finished == false) return;
        Vector3 position = player.GlobalPosition;
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z) ||
            player.Model.AdvanceAutomatically || player.Model.Playback.Frame < 0 ||
            player.Model.Playback.Frame >= player.Model.FrameCount || player.Controller.Position == initialPosition ||
            (verifyFirstRoof && (!player.Controller.IsGrounded || player.Controller.Position.Y != (-1474 << 12) ||
                player.Controller.Position.Z == initialPosition.Z || player.Controller.Velocity != default ||
                player.Controller.Action != PlayerAction.Idle || player.Model.ClipId != 0)))
        {
            GD.PushError("Player session smoke check failed: invalid actor position, animation ownership or frame.");
            GetTree().Quit(1);
        }
        else
        {
            player.Model.VerifyPlayback();
            GD.Print($"Verified level actor lifecycle: {player.SourceUpdates} two-tick updates, input, original terrain queries and skin playback; " +
                $"position={player.Controller.Position}, grounded={player.Controller.IsGrounded}, clip={player.Model.ClipId}.");
            CompleteSmoke();
        }
        SetPhysicsProcess(false);
    }

    private GodotPlayer CreateVerificationPlayer()
    {
        var random = new GameRandom(0x12A6CC58);
        PlayerStartingResources resources = PlayerWebInventoryRules.StartingResources(InitialDifficulty, 0, 0);
        GodotPlayer actor = GodotPlayer.Create(initialPosition, restart.Orientation.Y, playerModelFactory(),
            new(true, new(false, 10, true)), effectTextures!, health: resources.Health, random: random.Next);
        try
        {
            actor.Combat.ConfigureCollisionBoxes(playerCollisionBoxes);
            actor.ConfigureHandVariants(originalPlayerData.FistVariants);
            GodotSoundChannels sounds = GetNode<GodotSoundChannels>("SoundChannels");
            GodotSpeechPlayer speech = GetNode<GodotSpeechPlayer>("Speech");
            SoundListener Listener()
            {
                Camera3D camera = actor.GetViewport().GetCamera3D()
                    ?? throw new InvalidOperationException("Native player verification requires its own camera listener.");
                return new(PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12);
            }
            actor.SoundOutput = GetNode<GodotPlayerSoundEffects>("PlayerSoundEffects").Play;
            actor.Combat.SourceTimer = () => actor.Controller.SourceTimeTicks;
            actor.Combat.Sound = (sound, volume, _) => sounds.Controller.Play(sound, unchecked((short)volume));
            actor.Combat.PositionSound = (sound, position, _) => sounds.Controller.PlayAt(sound, position, Listener());
            actor.Combat.SoundStopped = handle => sounds.Controller.Stop(unchecked((uint)handle));
            actor.Inventory = new(new() { Webbing = resources.Webbing, Cartridges = resources.Cartridges }, actor,
                sounds.Controller, speech, Listener, () => InitialDifficulty, () => false, random.Next);
            return actor;
        }
        catch { actor.Free(); throw; }
    }

    private async void CompleteSmoke()
    {
        string? capture = System.Environment.GetEnvironmentVariable("OPENSPIDEY_CAPTURE");
        FixedVector3 verifiedFloorPosition = player!.Controller.Position;
        try
        {
            player!.SetPhysicsProcess(false);
            combatRuntime?.SetPhysicsProcess(false);
            if (verifyFirstRoof) LevelGeometryImportChecks.VerifyHiddenFaces(libraries.World!, libraries.Repository.Load("data/L1A1_G.psx").Scene);
            if (verifyFirstRoof) PlayerHandPartsImportChecks.Verify(this, playerModelFactory, originalPlayerData);
            if (verifyFirstRoof) await RenderEnvironmentImportChecks.VerifyAsync(this, renderEnvironment!,
                libraries.Repository.Load("data/L1A1_O.psx"), player, GetNode<PlayerFollowCamera>("PlayerCamera"));
            if (!string.IsNullOrWhiteSpace(capture))
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using Image image = GetViewport().GetTexture().GetImage();
                Error error = image.SavePng(capture);
                if (error != Error.Ok) throw new IOException($"Could not save player capture: {error}.");
                GD.Print($"Saved player capture: {capture}");
            }
            if (verifyFirstRoof) await InputSuppressionImportChecks.VerifyAsync(this, player);
            if (verifyFirstRoof) await PlayerScriptImportChecks.VerifyAsync(this, archive!, player, GetNode<PlayerFollowCamera>("PlayerCamera"));
            if (verifyFirstRoof) await PlayerSoundImportChecks.VerifyAsync(this, GetNode<GodotPlayerSoundEffects>("PlayerSoundEffects"));
            if (verifyFirstRoof) await SpeechImportChecks.VerifyAsync(this, player, GetNode<GodotSpeechPlayer>("Speech"));
            if (verifyFirstRoof) await PlayerKillQuipImportChecks.VerifyAsync(this, player, GetNode<GodotSpeechPlayer>("Speech"));
            if (verifyFirstRoof) await PlayerInventoryImportChecks.VerifyAsync(this, player, GetNode<GodotSoundChannels>("SoundChannels"),
                GetNode<GodotSpeechPlayer>("Speech"), GetNode<PlayerFollowCamera>("PlayerCamera"));
            if (verifyFirstRoof) BitmapFontImportChecks.Verify(this, archive!.Read("data/sp_fnt00.fnt"), "sp_fnt00.fnt");
            if (verifyFirstRoof) await PlayerHudImportChecks.VerifyAsync(this, archive!, player);
            if (verifyFirstRoof) await MissionTimerDisplayImportChecks.VerifyAsync(this, archive!, player);
            if (verifyFirstRoof) MissionMessageImportChecks.Verify(this, archive!, originalExecutable);
            if (verifyFirstRoof) await MissionTextBoxImportChecks.VerifyAsync(this, archive!, originalExecutable);
            if (verifyFirstRoof) await MusicImportChecks.VerifyAsync(this, archive!, player);
            if (verifyFirstRoof) await ActorSpeechImportChecks.VerifyAsync(this, archive!, GetNode<GodotSpeechPlayer>("Speech").CacheDirectory);
            if (verifyFirstRoof) await ScorpionApproachImportChecks.VerifyAsync(this, archive!, GetNode<GodotSpeechPlayer>("Speech").CacheDirectory);
            if (verifyFirstRoof) await ScorpionConstructionImportChecks.VerifyAsync(this, archive!, originalExecutable);
            if (verifyFirstRoof) await ScorpionTailImportChecks.VerifyAsync(this, archive!, originalExecutable);
            if (verifyFirstRoof) await JonahFootprintImportChecks.VerifyAsync(this, archive!, originalExecutable);
            if (verifyFirstRoof) await ScorpionHealthBarImportChecks.VerifyAsync(this, archive!);
            if (verifyFirstRoof) await MissionRenderCompatibilityImportChecks.VerifyAsync(this, archive!);
            if (verifyFirstRoof) await CompassImportChecks.VerifyAsync(this, archive!.Read("data/bits.psx"));
            if (verifyFirstRoof) await PlatformActorImportChecks.VerifyAsync(this, GetNode<GodotActorRegistry>("Actors"));
            if (verifyFirstRoof) await EnvironmentVisibilityImportChecks.VerifyAsync(this, archive!, libraries.Repository);
            if (verifyFirstRoof) await BaddyConstructionImportChecks.VerifyAsync(this, GetNode<GodotActorRegistry>("Actors").Triggers);
            if (verifyFirstRoof) await PlayerSupportImportChecks.VerifyAsync(this, GetNode<GodotActorRegistry>("Actors"), player);
            if (verifyFirstRoof) await GodotActorWorldQueryImportChecks.VerifyPlatformAsync(this, GetNode<GodotActorRegistry>("Actors"));
            if (verifyFirstRoof) await PlayerMeleeSessionImportChecks.VerifyAsync(this, GetNode<GodotActorRegistry>("Actors"),
                libraries.World!, libraries.Repository, player, verifiedFloorPosition);
            if (verifyFirstRoof) await ActorOrderImportChecks.VerifyAsync(this, GetNode<GodotActorRegistry>("Actors"));
            if (verifyFirstRoof) await PlayerCombatImportChecks.VerifyAsync(this, playerModelFactory, playerCollisionBoxes);
            if (verifyFirstRoof) PlayerMeleeModelImportChecks.Verify(this, playerModelFactory, OriginalPlayerData.Read(
                System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("OPENSPIDEY_ASSETS")!, "SpideyPC.exe"))));
            if (verifyFirstRoof) await SpriteRingImportChecks.VerifyAsync(this, archive!.Read("data/bits.psx"),
                GetNode<GodotActorRegistry>("Actors").Triggers,
                new GodotActorWorldQuery(this, GetNode<GodotActorRegistry>("Actors").Triggers));
            if (verifyFirstRoof) await ThugBulletImportChecks.VerifyAsync(this, GetNode<GodotActorRegistry>("Actors").Triggers);
            if (verifyFirstRoof) await ThugImpactImportChecks.VerifyAsync(this, archive!.Read("data/bits.psx"),
                archive.Read("data/henchman.psx"), GetNode<GodotActorRegistry>("Actors").Triggers, 312);
            if (verifyFirstRoof) await ThugCombatRingImportChecks.VerifyAsync(this, archive!.Read("data/bits.psx"), GetNode<GodotActorRegistry>("Actors").Triggers);
            if (verifyFirstRoof) await PlayerCombatImpactImportChecks.VerifyAsync(this, archive!.Read("data/bits.psx"));
            if (verifyFirstRoof) await BouncingRockImportChecks.VerifyAsync(this, archive!.Read("data/bits.psx"));
            if (verifyFirstRoof) await WebEffectImportChecks.VerifyAsync(this,
                archive!.Read("data/bits.psx"), archive.Read("data/shell_dc.psx"));
            if (verifyFirstRoof) await WebStrandImportChecks.VerifyAsync(this, archive!.Read("data/bits.psx"));
            if (verifyFirstRoof) await ImpactWebProjectileImportChecks.VerifyGeometryAsync(this, archive!.Read("data/bits.psx"));
            if (verifyFirstRoof) await KnottedWebSplatImportChecks.VerifyAsync(this, archive!.Read("data/bits.psx"));
            if (verifyFirstRoof) await FlamingWebImportChecks.VerifyAsync(this, archive!.Read("data/firebits.psx"));
            if (verifyFirstRoof) await FireEmberImportChecks.VerifyAsync(this, archive!.Read("data/bits.psx"));
            if (verifyFirstRoof) await CrateEffectsImportChecks.VerifyAsync(this, archive!.Read("data/bits.psx"));
            if (verifyFirstRoof) await ManipulationEffectsImportChecks.VerifySmokeAsync(this, archive!.Read("data/bits.psx"));
            if (verifyFirstRoof) await ManipulationEffectsImportChecks.VerifyDebrisAsync(this, archive!, libraries.Repository);
            if (verifyFirstRoof) await ManipulationChunkImportChecks.VerifyAsync(this, archive!, libraries.Repository);
            if (verifyFirstRoof)
            {
                OriginalPlayerData original = OriginalPlayerData.Read(System.IO.File.ReadAllBytes(
                    System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("OPENSPIDEY_ASSETS")!, "SpideyPC.exe")));
                GodotSoundChannels sounds = GetNode<GodotSoundChannels>("SoundChannels");
                using PlayerInteractionFixture fixture = PlayerInteractionFixture.Create(this, archive!, libraries.Repository,
                    original, CreateVerificationPlayer, new(archive!, libraries.Repository), libraries.Repository.Load("items"),
                    sounds.Controller, () => new(GetNode<PlayerFollowCamera>("PlayerCamera").SourcePosition >> 12));
                await PlayerInteractionImportChecks.VerifyFixtureAsync(this, fixture);
                await PlayerSmashAttackImportChecks.VerifyAsync(this, fixture, archive!.Read("data/bits.psx"));
                await PlayerMeleeNativeServicesImportChecks.VerifyFixtureAsync(this, fixture, archive!, libraries.Repository, original);
                CameraShakeImportChecks.Verify(fixture);
                await PlayerZipDiveImportChecks.VerifyFixtureAsync(this, fixture, archive!, libraries.Repository);
            }
            if (verifyFirstRoof) await PlayerCombatRuntimeImportChecks.VerifyAsync(this, archive!, libraries.Repository,
                originalPlayerData, CreateVerificationPlayer);
            if (verifyFirstRoof) await WebDomeImportChecks.VerifyAsync(this, archive!, libraries.Repository,
                originalPlayerData, CreateVerificationPlayer);
            if (verifyFirstRoof) await LevelOneBombImportChecks.VerifyAsync(this, archive!, libraries.Repository,
                originalPlayerData, CreateVerificationPlayer);
            if (verifyFirstRoof) await WebSwingImportChecks.VerifyAsync(this, archive!, libraries.Repository,
                originalPlayerData, CreateVerificationPlayer);
            if (verifyFirstRoof)
            {
                var enemyLibraries = new PsxLibraryRepository(archive!);
                using var regions = new PsxAssetRegions(enemyLibraries);
                await EnemyRuntimeImportChecks.VerifyAsync(this, archive!, enemyLibraries, originalPlayerData,
                    playerModelFactory, originalExecutable, regions);
            }
            if (verifyFirstRoof) await PlayerZipDiveImportChecks.VerifyRuntimeAsync(this, archive!, libraries.Repository,
                originalPlayerData, CreateVerificationPlayer);
            if (verifyFirstRoof) await GrenadeEffectImportChecks.VerifyAsync(this, archive!.Read("data/expgrnd.psx"));
            if (verifyFirstRoof) await BigExplosionImportChecks.VerifyAsync(this, archive!.Read("data/expgrnd.psx"));
            if (verifyFirstRoof) await ActorVisualEffectImportChecks.VerifyAsync(this, archive!);
            if (verifyFirstRoof) await GrenadeExplosionOwnershipImportChecks.VerifyAsync(this, archive!.Read("data/expgrnd.psx"));
            if (verifyFirstRoof) await ThugVisualEffectsImportChecks.VerifyAsync(this, archive!, libraries.Repository,
                GetNode<GodotSoundChannels>("SoundChannels").Controller,
                () => new(GetNode<PlayerFollowCamera>("PlayerCamera").SourcePosition >> 12));
            if (verifyFirstRoof) await ThugNativeServicesImportChecks.VerifyAsync(this, GetNode<GodotActorRegistry>("Actors"),
                archive!, libraries.Repository, playerModelFactory, playerCollisionBoxes, verifiedFloorPosition,
                player.Controller.Orientation.Heading, () => unchecked((uint)ticks), originalExecutable);
            if (verifyFirstRoof) await GrenadeNativeServicesImportChecks.VerifyAsync(this, GetNode<GodotActorRegistry>("Actors"),
                archive!, libraries.Repository, playerModelFactory, playerCollisionBoxes,
                player.Controller.Orientation.Heading, () => unchecked((uint)ticks));
            if (verifyFirstRoof) await HostageActorImportChecks.VerifyAsync(this, archive!, libraries.Repository,
                playerModelFactory, playerCollisionBoxes);
            if (verifyFirstRoof) await SwitchActorImportChecks.VerifyAsync(this, archive!, libraries.Repository);
            if (verifyFirstRoof)
            {
                await MissionLibraryImportChecks.VerifyFirstLevelAsync(this, libraries, player);
                MissionResourceCleanupImportChecks.Verify(this, archive!);
            }
            await CameraProjectionImportChecks.VerifyAsync(this, GetNode<PlayerFollowCamera>("PlayerCamera"));
            if (verifyFirstRoof) await FixedCameraImportChecks.VerifyAsync(this, archive!, GetNode<PlayerFollowCamera>("PlayerCamera"));
            if (verifyFirstRoof) await CameraRotationImportChecks.VerifyAsync(this, archive!, GetNode<PlayerFollowCamera>("PlayerCamera"));
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }
}
