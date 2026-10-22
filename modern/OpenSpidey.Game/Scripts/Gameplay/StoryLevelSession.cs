using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Audio;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Fonts;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Player.Scripting;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Core.UI;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Cinematics;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.UI;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Gameplay;

/// <summary>Original story startup: autoexec, initial source actors, real player/camera, then the authored restart.</summary>
public partial class StoryLevelSession : Node3D
{
    private static readonly string[] StoryChecks = ["--smoke-mission-startup", "--smoke-mission-two-startup",
        "--smoke-mission-three-startup", "--smoke-mission-four-startup", "--smoke-mission-five-startup",
        "--smoke-mission-six-startup",
        "--smoke-story-transition", "--smoke-story-two-transition", "--smoke-story-four-transition",
        "--smoke-mission-generic-startup"];
    private PkrArchive? archive;
    private ReadOnlyMemory<byte> executable;
    private OriginalPlayerData playerData = null!;
    private OriginalLevelCatalog levelCatalog = null!;
    private GameSessionState application = null!;
    private GodotMissionLibraries libraries = null!;
    private PsxAssetRegions? regions;
    private GodotCombatSceneOwnership? ownership;
    private PlayerEffectTextures? effectTextures;
    private GodotEnemyRuntime enemies = null!;
    private GodotActorCodeModules codeModules = null!;
    private GodotPlayerCutsceneSkip? cutsceneSkip;
    private GodotPlayerCombatRuntime? combat;
    private GodotPlayerWebRuntime? web;
    private GodotPlayerBrightness? playerBrightness;
    private GodotLevelObjectRuntime? objects;
    private PlayerFollowCamera? camera;
    private GodotSoundChannels sounds = null!;
    private GodotMusicController music = null!;
    private GodotSpeechPlayer speech = null!;
    private GodotRenderEnvironment rendering = null!;
    private GodotEnvironmentVisibility visibility = null!;
    private GodotMissionMessages messages = null!;
    private MissionEventPump events = null!;
    private GodotLevelLifecycle lifecycle = null!;
    private GodotScreenFlash flash = null!;
    private GodotSourceWaterEffect waterEffect = null!;
    private GodotLevelSceneObjects? sceneObjects;
    private GodotMissionLevelTransitions? transitions;
    private CanvasLayer? hudLayer;
    private Label status = null!;
    private DeferredMissionCommands playerCommands = null!;
    private TriggerFile triggers = null!;
    private byte[] triggerBytes = [];
    private string geometryAsset = "";
    private string restartOverride = "";
    private MissionEnvironmentPlan environmentPlan = null!;
    private ImportedLevelWorld? playerWorld;
    private readonly List<(string AssetPath, PsxScene Scene, int Model)> pendingModelClaims = [];
    private readonly GameRandom random = new(0x12A6CC58);
    private readonly SourceVertexLightingState lighting = new();
    private int physicsFrames, worldFirstFrame = -1, sourceTicks;
    private int renderedLogicFrame;
    private bool destroyed, gameplayStarted;
    public GodotPlayer? Player { get; private set; }
    public GameSessionState Application => application;
    public GodotRenderEnvironment Rendering => rendering;
    public GodotPlayerBrightness PlayerBrightness => playerBrightness ?? throw new InvalidOperationException("The source player light is not initialized.");
    public GodotLevelSceneObjects SceneObjects => sceneObjects ?? throw new InvalidOperationException("Source scene destruction is unavailable before player composition.");
    public GodotActorRegistry Registry { get; private set; } = null!;
    public MissionRuntime Mission { get; private set; } = null!;
    public MissionStartupSequence Startup { get; private set; } = null!;
    public MissionCheckpointSession Checkpoints { get; private set; } = null!;
    public GodotCinemaPlayer Cinema { get; private set; } = null!;
    internal GodotScorpionApproachModule ScorpionApproach { get; private set; } = null!;
    internal GodotScorpionEncounterRuntime ScorpionEncounter { get; private set; } = null!;
    internal GodotSubwayLevelModule SubwayLevel { get; private set; } = null!;
    internal GodotCampaignWaterLevelModule CampaignWaterLevel { get; private set; } = null!;
    internal GodotTrainingModule Training { get; private set; } = null!;
    internal GodotPlayerModuleHook PlayerModules { get; private set; } = null!;
    public uint SourceClock => unchecked((uint)sourceTicks);
    public int LogicFrame => sourceTicks / 2;
    public Exception? Failure { get; private set; }
    public event Action? GameplayStarted;
    public event Action? PlayerConstructed;
    private ImportedLevelWorld World => libraries.World ?? throw new InvalidOperationException("The authored environment is not loaded.");

    public override void _Ready()
    {
        ProcessPriority = 100;
        ProcessPhysicsPriority = -100;
        var overlay = new CanvasLayer { Layer = 100 };
        status = new Label { Position = new(20, 20), Text = "Loading mission…" };
        overlay.AddChild(status); AddChild(overlay);
        try
        {
            PrepareMission();
            if (IsStoryVerificationRequested())
                VerifyStoryStartup();
        }
        catch (Exception error) { Fail(error); }
    }

    private void PrepareMission()
    {
        if (!System.Environment.Is64BitProcess) throw new PlatformNotSupportedException("A 64-bit runtime is required.");
        string directory = System.Environment.GetEnvironmentVariable("OPENSPIDEY_ASSETS")
            ?? throw new InvalidOperationException("Supply your original game folder with --assets.");
        application = GetTree().Root.GetNode<GameSessionState>("GameSession");
        LevelExitReason previousExit = application.ExitReason;
        geometryAsset = application.SceneAsset ?? System.Environment.GetEnvironmentVariable("OPENSPIDEY_SCENE") ?? "data/L1A1_G.psx";
        MissionCheckpointSave saved = application.OpenLevel(geometryAsset,
            System.Environment.GetEnvironmentVariable("OPENSPIDEY_RESTART") ?? "");
        restartOverride = saved.RestartPointName;
        archive = new(System.IO.Path.Combine(directory, "data.pkr"));
        executable = System.IO.File.ReadAllBytes(System.IO.Path.Combine(directory, "SpideyPC.exe"));
        playerData = OriginalPlayerData.Read(executable.Span);
        levelCatalog = OriginalLevelCatalog.Read(executable);
        application.PlayerModuleHook.Set(null, 0);
        triggerBytes = archive.Read("data/" + application.MissionName + ".trg");
        triggers = TriggerFile.Read(triggerBytes);
        environmentPlan = MissionEnvironmentPlan.Read(triggers, restartOverride, application.AlternativeAutoexec);
        if (!environmentPlan.AssetPath.Equals(geometryAsset, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Mission {application.MissionName} requests {environmentPlan.AssetPath}, but its scene selected {geometryAsset}.");
        var messageData = OriginalMissionMessageData.Read(executable);
        messages = GodotMissionMessages.Create(messageData,
            OriginalBitmapFontReader.Read(archive.Read("data/sp_fnt00.fnt"), "sp_fnt00.fnt"));
        AddChild(messages);
        Checkpoints = new(triggers, saved, messages);
        libraries = new() { Name = "MissionLibraries", Repository = new(archive) };
        regions = new(libraries.Repository); AddChild(libraries);
        libraries.LoadProtectedCommonLibraries(regions);
        visibility = new(() => World, libraries.Repository, (source, index) => objects?.PreparedEnvironment?.Items.FirstOrDefault(item =>
            item.SourceAsset.Equals(source.AssetPath, StringComparison.OrdinalIgnoreCase) && item.SourceInstanceIndex == index));
        string soundGroup = application.MissionName[..4];
        sounds = new() { Name = "SoundChannels", Archive = archive, LevelGroup = SoundCatalog.Find(soundGroup) };
        AddChild(sounds);
        sounds.Controller.PlayerBank = SoundDefinitionBank.Read(archive.Read("data/spidey.sfx"));
        sounds.Controller.LevelBank = SoundDefinitionBank.Read(archive.Read("data/" + soundGroup + ".sfx"), levelSpecific: true);
        speech = new() { Name = "Speech", CacheDirectory = System.Environment.GetEnvironmentVariable("OPENSPIDEY_MEDIA")
            ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".local/share/openspidey/media") };
        AddChild(speech);
        rendering = GodotRenderEnvironment.Create(libraries,
            () => libraries.ObjectLibrary ?? throw new InvalidOperationException("No authored object library is selected."),
            libraries.ObjectTextures, () => camera, () => SourceClock);
        AddChild(rendering);
        rendering.SetProcess(false);
        waterEffect = GodotSourceWaterEffect.Create(application.WaterPost, () => SourceClock,
            () => application.FrontPauseState, () =>
            {
                Vector2 size = GetViewport().GetVisibleRect().Size;
                return (checked((uint)size.X), checked((uint)size.Y));
            }, GodotGlowFlash.SourceBrightness());
        AddChild(waterEffect); waterEffect.SetProcess(false);
        rendering.Backdrops.SourceModelClaimed += ClaimObjectModel;
        Registry = new() { Name = "Actors", Triggers = triggers, Difficulty = application.Difficulty,
            RandomSource = random.Next, Sounds = sounds.Controller, SoundListener = Listener,
            VoicePlayback = (bank, slot, priority) => speech.Play(bank, slot, priority),
            PositionVoicePlayback = (bank, slot, _, priority) => speech.PlayAtPosition(bank, slot, priority, () => SourceClock),
            ObjectLibrary = () => libraries.ObjectLibrary ?? throw new InvalidOperationException("No authored object library is selected."),
            ObjectTextures = libraries.ObjectTextures,
            ClaimObjectModel = ClaimObjectModel,
            ShatterPlatform = (actor, checkSeen, processed) => (combat ?? throw new InvalidOperationException("Player combat is not initialized.")).ShatterPlatform(actor, checkSeen, processed),
            Environment = new(LevelId: MissionLevelId.FromAssetName(geometryAsset)), ProcessMode = ProcessModeEnum.Disabled };
        AddChild(Registry); ownership = new(Registry);
        music = new(application.Music, sounds, () => Player?.Combat.State,
            () => Registry.Environment.LevelId, () => application.FrontPauseState, () => application.EndTrainingFlag);
        var electroLines = new GodotElectroLines
        {
            Name = "ElectroLines",
            Random = random.Next,
            TextureManifest = () => archive!.Read(geometryAsset[..^4] + ".tex"),
            PlayerBodies = () => Player is null ? null :
                [new(Player, Player.Controller.Position, Player.ActorState.MinorRadius, Player.ActorState.BodyFlags)]
        };
        electroLines.SourceBitCreated += bit => ownership!.Particles.Attach(SourceParticleList.Special, bit);
        AddChild(electroLines); Registry.ElectroLines = electroLines;
        var sparks = GodotSparkParticles.Create(ownership.Particles, application.Sparks, random.Next);
        AddChild(sparks); Registry.Sparks = sparks;
        Registry.CameraShake = new GodotActorCameraShakeHost(() => camera);
        Registry.CinematicScreenHost = application.CinematicScreen;
        Registry.PlayerScriptHost = new GodotActorPlayerScriptHost(() => Player);
        var textBoxes = GodotMissionTextBoxes.Create(messages.Messages, ownership.Particles, () => sourceTicks);
        messages.AddChild(textBoxes);
        Registry.ControlServices = new GodotControlSceneServices(application.Compass, sounds.Controller, Listener,
            () => unchecked((uint)LogicFrame));
        objects = GodotLevelObjectRuntime.Prepare(new()
        {
            Archive = archive, Repository = libraries.Repository, CurrentWorld = () => World, Registry = Registry,
            SceneOwnership = ownership, Sounds = sounds, Listener = Listener, Countdown = application.Countdown,
            TriggerData = triggerBytes, Clock = () => SourceClock, Visibility = visibility
        });
        objects.ProcessMode = ProcessModeEnum.Disabled; AddChild(objects);
        Registry.ActorVisualEffects = new GodotActorVisualEffects(
            () => combat?.Glows ?? throw new InvalidOperationException("The genuine source GlowList owner is not composed."),
            objects.Explosions, random.Next);
        rendering.Backdrops.BindSourceList(Registry.SourceList(ActorUpdateGroup.Background));
        enemies = GodotEnemyRuntime.Prepare(new()
        {
            Archive = archive, Repository = libraries.Repository, Registry = Registry, SceneOwnership = ownership,
            OriginalExecutable = executable, Regions = regions, CurrentWorld = () => World,
            Sounds = sounds, Speech = speech, EnvironmentVisibility = visibility, LightingState = lighting,
            Clock = () => SourceClock, AttackClock = () => application.AttackClock, TotalTime = () => LogicFrame,
            TriggerCommandPoint = checksum => Mission.TriggerCommandPoint(checksum),
            WhatIf = () => application.WhatIf,
            UnloadCodeModulesOnDestroy = false
        });
        codeModules = enemies.Modules;
        enemies.ProcessMode = ProcessModeEnum.Disabled; AddChild(enemies);
        ScorpionApproach = GodotScorpionApproachModule.Create(new()
        {
            Archive = archive, Repository = libraries.Repository, Registry = Registry,
            Countdown = application.Countdown, State = application.ScorpionApproach,
            PlayerHook = application.PlayerModuleHook, Speech = speech, Clock = () => SourceClock,
            ScreenOffsetY = () => application.CinematicScreen.WideScreenShadow,
            AlternateStory = () => application.WhatIf, SendPulse = node => Mission.ReceivePulse(node)
        });
        AddChild(ScorpionApproach); ScorpionApproach.Register(codeModules.Modules);
        ScorpionEncounter = GodotScorpionEncounterRuntime.Create(new()
        {
            Archive = archive, Repository = libraries.Repository, OriginalExecutable = executable, Registry = Registry,
            Regions = regions!, LightingState = lighting, JoelJewettCheat = () => application.AlternativeAutoexec,
            SceneOwnership = ownership, Player = () => Player, Clock = () => SourceClock, AttackClock = () => application.AttackClock,
            Difficulty = () => application.Difficulty,
            Music = application.Music, PositionSpeech = (bank, slot, priority) => speech.PlayAtPosition(bank, slot, priority, () => SourceClock)
        });
        AddChild(ScorpionEncounter); ScorpionEncounter.Register(codeModules.Modules);
        SubwayLevel = GodotSubwayLevelModule.Create(new()
        {
            Archive = archive, Repository = libraries.Repository, Registry = Registry,
            Countdown = application.Countdown, PlayerHook = application.PlayerModuleHook,
            Clock = () => SourceClock,
            ScreenOffsetY = () => application.CinematicScreen.WideScreenShadow,
            WideScreenValue = () => application.CinematicScreen.WideScreenValue,
            EndLevelNode = () => application.EndLevelNode,
            SendPulse = node => Mission.ReceivePulse(node),
            PlayerBrightness = () => playerBrightness
        });
        AddChild(SubwayLevel); SubwayLevel.Register(codeModules.Modules);
        CampaignWaterLevel = GodotCampaignWaterLevelModule.Create(new()
        {
            Registry = Registry, SceneWater = application.SceneWater,
            PlayerHook = application.PlayerModuleHook, SendPulse = node => Mission.ReceivePulse(node)
        });
        AddChild(CampaignWaterLevel); CampaignWaterLevel.Register(codeModules.Modules);
        Training = GodotTrainingModule.Create(new()
        {
            Archive = archive, Registry = Registry, Countdown = application.Countdown, Results = application.TrainingResults,
            PlayerHook = application.PlayerModuleHook, Progress = application.Campaign, Clock = () => SourceClock,
            ScreenOffsetY = () => application.CinematicScreen.WideScreenShadow,
            KillCount = () => (combat ?? throw new InvalidOperationException("Training kill count requires the player combat owner.")).KillQuips.Rules.History.NotificationCount,
            SendPulse = node => Mission.ReceivePulse(node)
        });
        AddChild(Training); Training.Register(codeModules.Modules);
        Cinema = new() { Name = "MissionCinema", CacheDirectory = speech.CacheDirectory, PauseGameplay = true }; AddChild(Cinema);
        lifecycle = new() { Name = "LevelLifecycle" }; AddChild(lifecycle);
        lifecycle.Exited += _ => StopGameplay();
        lifecycle.Restarting += reason => { if (reason == LevelExitReason.RestartFromBeginning) application.RestartFromBeginning(); };
        playerCommands = new(new ushort[] { 130, 152, 158, 160, 163, 167, 180, 181, 182, 183, 184, 185, 186, 187, 188, 195, 196, 197, 199, 213, 215, 216, 217, 222, 223, 305 });
        var visibilityLists = new GodotVisibilityBoxHost(Registry, visibility, libraries.Repository);
        var actorDeath = new GodotMissionActorDeath(Registry, visibility);
        var baddies = new GodotMissionBaddies(Registry);
        var reverb = new SourceReverbState();
        IEnumerable<IMissionCommandHandler> handlers = SceneLibraryCommandHandler.CreateHandlers(libraries)
            .Concat(RenderEnvironmentCommandHandler.CreateHandlers(rendering))
            .Concat(OrderTablePushbackCommandHandler.CreateHandlers(rendering.SourceDepth))
            .Concat(new[] { MissionOpcode.SendSignal, MissionOpcode.SendSuspend, MissionOpcode.SendActivate }
                .Select(opcode => new ActorSignalCommandHandler(opcode, triggers, Registry)))
            .Append(new EnvironmentVisibilityCommandHandler(visibility))
            .Append(new LinkedEnvironmentVisibilityCommandHandler(triggers, visibility))
            .Concat(MissionActorDeathCommandHandler.CreateHandlers(triggers, actorDeath))
            .Append(MissionActorDeathCommandHandler.CreateLoudHandler(triggers,
                new GodotMissionLoudDeath(actorDeath, () => combat?.Actors, () => enemies.Environment)))
            .Concat(ChopperCommandHandler.CreateHandlers(triggers, baddies))
            .Concat(VenomWaitCommandHandler.CreateHandlers(baddies))
            .Append(new ReverbTypeCommandHandler(reverb))
            .Append(new GameLevelCommandHandler(application))
            .Append(new DualBufferSizeCommandHandler())
            .Append(new EndLevelNodeCommandHandler(application))
            .Concat(SpeedupCommandHandler.CreateHandlers())
            .Concat(PaletteFadeCommandHandler.CreateHandlers(new GodotMissionPaletteFade(codeModules.Modules)))
            .Append(new ModuleLoadCommandHandler(enemies.Modules))
            .Concat(MissionResourceCleanupCommandHandler.CreateHandlers(new GodotMissionResourceCleanup(libraries, regions, codeModules)))
            .Append(new RunCinemaCommandHandler(Cinema, previousExit is LevelExitReason.PlayerDied or LevelExitReason.RetryRequested))
            .Concat(new ushort[] { 141, 192, 209 }.Select(opcode => new VisibilityBoxCommandHandler(opcode, visibilityLists)))
            .Append(new ActorSuspensionDistanceCommandHandler(distance => Registry.SuspensionDistance = unchecked((uint)distance)))
            .Concat(Checkpoints.CreateHandlers())
            .Concat(MissionTextCommandHandler.CreateHandlers(messages, messageData, new GodotMissionControlNames(PlayerControls.SourceActionName)))
            .Concat(MissionTextBoxCommandHandler.CreateHandlers(textBoxes))
            .Concat(SoundCommandHandler.CreateHandlers(sounds.Controller))
            .Concat(SpeechCommandHandler.CreateHandlers(speech.Controller, speech.Environment))
            .Concat(playerCommands.Commands);
        Mission = new(triggers, Registry, handlers);
        Mission.CommandListCompleted += _ => rendering.EndCommandList();
        Registry.ReceivePulse = Mission.ReceivePulse;
        Registry.EnvironmentChunks = new GodotEnvironmentChunks(Registry, visibility,
            path => archive.Read(path), checksum => Mission.TriggerCommandPoint(checksum), () => SourceClock);
        Registry.Faulted += Fail;
        speech.Faulted += Fail;
        events = new() { Name = "MissionEvents", Runtime = Mission };
        events.Faulted += Fail; events.SourceFrameCompleted += AdvanceStartup; AddChild(events);
        Startup = new(triggers, Mission, Registry, InitializePlayerAndRestart);
        libraries.EnvironmentLoaded += BindPlayerWorld;
        Startup.Begin(application.AlternativeAutoexec);
    }

    private async void VerifyStoryStartup()
    {
        SceneTree tree = GetTree();
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--smoke-story-four-transition"))
            {
                await StoryFourthMissionTransitionImportChecks.VerifyAsync(this);
                if (!application.HasMeta(StoryFourthMissionTransitionImportChecks.ProofState)) tree.Quit();
                return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--smoke-story-two-transition"))
            {
                await StorySecondMissionTransitionImportChecks.VerifyAsync(this);
                if (!application.HasMeta(StorySecondMissionTransitionImportChecks.ProofState)) tree.Quit();
                return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--smoke-story-transition"))
            {
                await StoryMissionTransitionImportChecks.VerifyAsync(this);
                if (!application.HasMeta("story_end_transition_proof")) tree.Quit();
                return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--smoke-mission-six-startup"))
                await StoryLevelSixSessionImportChecks.VerifyAsync(this);
            else if (OS.GetCmdlineUserArgs().Contains("--smoke-mission-generic-startup"))
                await StoryGenericStartupImportChecks.VerifyAsync(this);
            else if (OS.GetCmdlineUserArgs().Contains("--smoke-mission-five-startup"))
                await StoryLevelFiveSessionImportChecks.VerifyAsync(this);
            else if (OS.GetCmdlineUserArgs().Contains("--smoke-mission-four-startup"))
                await StoryLevelFourSessionImportChecks.VerifyAsync(this);
            else if (OS.GetCmdlineUserArgs().Contains("--smoke-mission-three-startup"))
                await StoryMissionThreeImportChecks.VerifyAsync(this);
            else if (OS.GetCmdlineUserArgs().Contains("--smoke-mission-two-startup"))
                await StoryLevelTwoSessionImportChecks.VerifyAsync(this);
            else if (OS.GetCmdlineUserArgs().Contains("--smoke-mission-l3a1-startup"))
                await StoryGenericStartupImportChecks.VerifyAsync(this);
            else await StoryLevelSessionImportChecks.VerifyAsync(this);
            await StoryLevelSessionImportChecks.VerifyNativeDeletionAsync(this, tree);
            tree.Quit();
        }
        catch (Exception error)
        {
            if (GodotObject.IsInstanceValid(this)) Fail(error);
            else { GD.PushError(error.ToString()); tree.Quit(1); }
        }
    }

    private ushort InitializePlayerAndRestart()
    {
        PsxAssetLibrary bank = libraries.Repository.Load("spidey");
        var animations = bank.Animations as CompressedAnimationLibrary ?? throw new InvalidDataException("The original player skeleton is missing.");
        ImportedAnimatedModel model = ImportedAnimatedModel.Build(bank.Scene, bank.Textures, animations, playerPartOrder: true);
        PlayerStartingResources resources = PlayerWebInventoryRules.StartingResources(application.Difficulty, 0,
            MissionLevelId.FromAssetName(geometryAsset), application.CarriedHistory.Saved.Webbing, application.CarriedHistory.Saved.Cartridges);
        effectTextures = PlayerEffectTextures.Load(archive!);
        Player = GodotPlayer.Create(default, 0, model, new(true, new(false, 10, true)), effectTextures,
            health: resources.Health, random: random.Next);
        Player.Combat.InitializeFightMusicLevel(Registry.Environment.LevelId);
        Player.Combat.ConfigureCollisionBoxes(PsxActorCollisionBoxes.Read(archive!.Read(bank.AssetPath)));
        Player.ConfigureHandVariants(playerData.FistVariants);
        Player.ProcessMode = ProcessModeEnum.Disabled; Player.Hide(); AddChild(Player);
        Registry.Player = Player;
        PlayerConstructed?.Invoke();
        if (restartOverride.Length != 0) Checkpoints.Select(restartOverride);
        RestartPoint restart = Checkpoints.Current ?? throw new InvalidDataException("Autoexec did not select an authored restart.");
        ComposePlayer(resources);
        Player.Controller.SetScriptPosition(new FixedVector3(restart.Position.X, restart.Position.Y, restart.Position.Z) << 12);
        Player.Controller.SetStartOrientation(restart.Orientation.Y);
        Player.RefreshCombatTransform();
        return restart.NodeId;
    }

    private SoundListener Listener() => new(application.AudioListenerPosition);

    private void AdvanceStartup()
    {
        if (Failure is not null) return;
        if (Player is not null) cutsceneSkip?.FinishSourceFrame();
        if (Startup.Phase == MissionStartupPhase.Autoexec && environmentPlan.LoadsInAutoexec && libraries.World is null) return;
        if ((Startup.Phase is MissionStartupPhase.Autoexec or MissionStartupPhase.Restart) && libraries.World is not null &&
            (worldFirstFrame < 0 || physicsFrames - worldFirstFrame < 2)) return;
        Startup.Advance();
        if (Startup.Phase == MissionStartupPhase.Playing && !gameplayStarted)
        {
            Checkpoints.SaveCurrentRestart();
            camera!.ScriptRig.SetMode(3); // Front_LoadGame's actual CAMERAMODE_DEMO.
            application.AttackClock = 0; // PlayAway begins after source constructors and resets its world attack phase.
            gameplayStarted = true;
            Player!.ProcessMode = ProcessModeEnum.Inherit; Player.Show();
            Registry.ProcessMode = enemies.ProcessMode = ProcessModeEnum.Inherit;
            combat!.ProcessMode = web!.ProcessMode = objects!.ProcessMode = ProcessModeEnum.Inherit;
            hudLayer!.Show(); status.Text = "";
            GD.Print($"Started story mission {application.MissionName}, restart {Checkpoints.Current!.Name}, {Startup.InitialActorsCreated} authored initial actors.");
            GameplayStarted?.Invoke();
        }
        if (gameplayStarted) messages.UpdateSourceTimer(sourceTicks);
    }

    private void StopGameplay()
    {
        foreach (Node? node in new Node?[] { Player, Registry, enemies, combat, web, objects })
            if (node is not null && GodotObject.IsInstanceValid(node)) node.ProcessMode = ProcessModeEnum.Disabled;
        // KillEverything305 may precede110/162/cinema190/mission181 in the same authored list.
        // Keep its real event continuation alive until an actual level exit, while body simulation stays stopped.
        if (lifecycle.ExitReason != LevelExitReason.Running) events.ProcessMode = ProcessModeEnum.Disabled;
        if (hudLayer is not null) { hudLayer.ProcessMode = ProcessModeEnum.Disabled; hudLayer.Hide(); }
        rendering.SetProcess(false); speech.Controller.Stop();
        SetProcess(false); SetPhysicsProcess(false);
    }

    private void Fail(Exception error)
    {
        if (Failure is not null) return;
        Failure = error; status.Text = "Mission stopped: " + error.Message;
        if (events is not null) events.SetPhysicsProcess(false);
        foreach (Node? node in new Node?[] { Player, Registry, enemies, combat, web, objects })
            if (node is not null && GodotObject.IsInstanceValid(node)) node.ProcessMode = ProcessModeEnum.Disabled;
        SetPhysicsProcess(false); GD.PushError(error.ToString());
        if (IsStoryVerificationRequested())
            GetTree().Quit(1);
    }

    public override void _PhysicsProcess(double delta)
    {
        physicsFrames++;
        if (worldFirstFrame < 0 && libraries?.World is not null) worldFirstFrame = physicsFrames;
        if (!gameplayStarted || lifecycle.ExitReason != LevelExitReason.Running) return;
        sourceTicks++;
        if ((sourceTicks & 1) == 0)
        {
            application.AttackClock = unchecked(application.AttackClock + 1);
            flash.Update();
        }
    }

    public override void _Process(double delta)
    {
        if (!gameplayStarted || Failure is not null || lifecycle.ExitReason != LevelExitReason.Running) return;
        music.Render(SourceClock);
        if (LogicFrame > renderedLogicFrame)
        {
            camera!.RenderSourceFrame();
            renderedLogicFrame = LogicFrame;
        }
        application.AudioListenerPosition = camera!.SourcePosition >> 12;
        rendering.SynchronizeSourceRender();
        music.Render(SourceClock);
        playerBrightness!.SynchronizeSourceLighting();
        Player!.SynchronizeSourceTranslucency();
        enemies.SynchronizeSourceLighting();
        waterEffect.SynchronizeSourceRender();
        music.Render(SourceClock);
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || destroyed) return;
        destroyed = true; Startup?.Dispose(); transitions?.Dispose();
        if (libraries is not null) libraries.EnvironmentLoaded -= BindPlayerWorld;
        if (rendering is not null) rendering.Backdrops.SourceModelClaimed -= ClaimObjectModel;
        if (sceneObjects is { IsDestroyed: false })
            sceneObjects.KillAllSceneObjects(application.ExitReason);
        codeModules?.UnloadAllCodeModules();
        // Free scene-owned native children before their shared source directories, caches and archive.
        foreach (Node child in GetChildren().ToArray()) child.Free();
        ownership?.Dispose(); effectTextures?.Dispose(); regions?.Dispose(); archive?.Dispose();
    }

    private static bool IsStoryVerificationRequested() => OS.GetCmdlineUserArgs().Any(StoryChecks.Contains);

    private void ClaimObjectModel(string path, int model)
    {
        if (libraries.World is { } world) { world.ClaimObjectModel(path, model); return; }
        PsxAssetLibrary bank = libraries.Repository.RequireLoaded(path);
        pendingModelClaims.Add((bank.AssetPath, bank.Scene, model));
    }
}
