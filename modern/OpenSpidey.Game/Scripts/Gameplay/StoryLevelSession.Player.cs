using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Player.Scripting;
using OpenSpidey.Core.UI;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.UI;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Gameplay;

public partial class StoryLevelSession
{
    private void ComposePlayer(PlayerStartingResources resources)
    {
        GodotPlayer player = Player ?? throw new InvalidOperationException("The genuine player has not been constructed.");
        playerBrightness = GodotPlayerBrightness.Create(player, OriginalPlayerLightingData.Read(executable), lighting);
        player.AddChild(playerBrightness); playerBrightness.SetProcess(false);
        var playerSounds = new GodotPlayerSoundEffects { Name = "PlayerSoundEffects", Backend = sounds }; AddChild(playerSounds);
        player.SoundOutput = playerSounds.Play;
        camera = new() { Name = "PlayerCamera", Target = player, SourceTotalTime = () => unchecked((uint)LogicFrame),
            SourceProjectionCache = application.CameraProjectionCache, SourceRenderPhase = true }; AddChild(camera);
        camera.ScriptRig.CinematicScreen = application.CinematicScreen;
        player.ResetCinematicScreen = application.CinematicScreen.Reset;
        player.CameraStartup = new(player, () => camera);
        player.CameraHeading = () => camera.SourceHeading;
        player.Inventory = new(new() { Webbing = resources.Webbing, Cartridges = resources.Cartridges }, player,
            sounds.Controller, speech, Listener, () => Registry.Difficulty, () => false, random.Next);
        player.Scripts = new(player, triggers, () => new PlayerScriptEnvironment(false, false,
            MissionLevelId.FromAssetName(geometryAsset), player.Controller.Position));
        player.Scripts.VoicePlayback = (bank, slot, priority) => speech.Play(bank, slot, priority);
        cutsceneSkip = new(player, triggers, Registry, camera, speech, () => application.ForceEarlyLevelExit != 0,
            direction => (web?.DistanceAttackPreparation ?? throw new InvalidOperationException("Source distance-attack preparation is not composed.")).Prepare(direction));
        flash = new() { Name = "ScreenFlash" }; AddChild(flash);
        Registry.ScreenFlash = flash;
        player.Combat.SourceTimer = () => SourceClock;
        player.Combat.CameraFlag = () => camera.TerrainObstructed;
        player.Combat.Sound = (sound, volume, _) => sounds.Controller.Play(sound, unchecked((short)volume));
        player.Combat.PositionSound = (sound, position, _) => sounds.Controller.PlayAt(sound, position, Listener());
        player.Combat.SoundStopped = handle => sounds.Controller.Stop(unchecked((uint)handle));
        player.Combat.XaStopped = () =>
        {
            if (player.Combat.SpeechAttachment.CanStop(SourceClock, speech.Controller.Current)) speech.Controller.Stop();
        };
        player.Combat.CameraBehind = camera.PutBehind;
        player.Combat.CameraField12C = camera.SetFocusHook;
        player.Combat.LevelStatus = lifecycle.RequestExit;
        player.Combat.InitializationReset = lifecycle.ResetInitialization;
        player.Combat.FlashReset = flash.Reset;
        combat = GodotPlayerCombatRuntime.Create(archive!, libraries.Repository, playerData, player, Registry,
            () => World, camera, speech, () => SourceClock, ownership);
        combat.ProcessMode = ProcessModeEnum.Disabled; AddChild(combat); combat.Faulted += Fail;
        objects!.BindPlayer(combat, camera);
        web = GodotPlayerWebRuntime.Create(archive!, libraries.Repository, player, combat, camera,
            () => SourceClock, random.Next,
            (sound, position, _) => unchecked((int)sounds.Controller.PlayAt(sound, position, Listener())));
        web.ProcessMode = ProcessModeEnum.Disabled; AddChild(web);
        web.ConfigureDomes(Registry, combat, libraries.Repository, camera, () => SourceClock, random.Next);
        hudLayer = new() { Name = "GameplayHud", Layer = 20 }; AddChild(hudLayer); hudLayer.Hide();
        var screenTarget = new GodotScreenTarget { Name = "LookaroundTarget", Camera = camera }; hudLayer.AddChild(screenTarget);
        var lookaround = new GodotPlayerLookaroundRuntime(player, web, combat, camera, screenTarget, new()
        {
            PracticeMode = () => web.PracticeDifficulty != 0, InvertPitch = () => false,
            SwingWebPresent = () => web.Swing?.SwingPresent == true,
            BeginZip = player.Controller.BeginLookaroundZip, BeginSwing = _ => player.Controller.ApplyCombatState()
        });
        web.ConfigureSwing(camera, combat, lookaround.Targets);
        player.Scripts.BindSourceOwners(web, combat);
        player.Combat.LookaroundExited = lookaround.Camera.ExitLookaroundMode;
        player.Controller.Lookaround = lookaround; player.Controller.Web = web;
        web.ConfigureDistanceAttack(combat, camera, libraries.Repository.Load("items"), sounds.Controller,
            application.WaterEffect, () => default);
        web.EnablePlayerEffectPhases(rendering);
        var visibilityLists = new GodotVisibilityBoxHost(Registry, visibility, libraries.Repository);
        enemies.BindPlayer(new()
        {
            Player = player, Camera = camera, Combat = combat, Web = web,
            HostageCamera = new GodotOptimumViewingCamera(triggers, combat.World, camera, visibilityLists)
        });
        var pickups = new GodotPlayerPowerUpResources(player, archive!, player.PowerUpCollection, () => application.Difficulty);
        pickups.ApplyConstructorState(PlayerCarriedSavePolicy.CreatePlayer(application.CarriedHistory.Saved,
            application.Difficulty, player.Combat.CurrentSuit, MissionLevelId.FromAssetName(geometryAsset), armourSuppressed: false));
        player.Combat.ArmorTextures = pickups.ProcessArmorTextures;
        player.Combat.ArmorSaved = (amount, active) => application.CarriedHistory = application.CarriedHistory with
        {
            Saved = application.CarriedHistory.Saved with { ArmourAmount = amount, ArmourUnlocked = active }
        };
        Registry.PowerUps = new GodotPowerUpFactory(new()
        {
            Items = libraries.Repository.Load("items"),
            ShadowTexture = libraries.Repository.Load("shell_dc").Textures.Single(texture => texture.Checksum == 0x9F6CA828),
            TrainingPickupTexture = () =>
            {
                uint checksum = NamedTextureTable.Find(archive!.Read(geometryAsset[..^4] + ".tex"), "TrainingPick-Up_01");
                return libraries.Repository.RequireLoaded(geometryAsset).Textures.Single(texture => texture.Checksum == checksum);
            },
            World = new GodotPlayerWorldQuery(player), Glows = combat.Glows,
            Progress = application.Campaign, Collection = player.PowerUpCollection,
            SourceClock = () => SourceClock, LogicFrame = () => LogicFrame,
            SpeechBusy = () => speech.Controller.Busy,
            SendSignal = Registry.SignalActiveActors, PlaySound = player.Combat.PlaySound,
            PlaySpeech = (bank, track, priority) => speech.Play(bank, track, priority), SetArmor = pickups.SetArmor,
            SourceBitCreated = (list, bit) => ownership!.Particles.Attach(list, bit), SourceBitRemoved = identity => ownership!.Particles.Detach(identity)
        });
        player.Controller.PowerUps = new GodotPlayerPowerUpCollection(player, Registry, player.PowerUpCollection, player.Combat.PlaySound);
        if (libraries.World is { } loadedWorld) BindPlayerWorld(loadedWorld);
        PlayerModules = GodotPlayerModuleHook.Bind(player, application.PlayerModuleHook, codeModules.Modules);
        player.MissionContacts = Mission;
        GodotPlayerHud hud = GodotPlayerHud.Create(archive!, player, () => SourceClock);
        hud.SourceScreenOffsetY = () => application.CinematicScreen.WideScreenShadow;
        hud.BindPowerUpResources(pickups); hud.Countdown = application.Countdown; hudLayer.AddChild(hud);
        GodotCompass compass = GodotCompass.Create(archive!.Read("data/bits.psx"), Registry.RequireControlServices().Compass,
            () => new CompassView(camera.SourcePosition >> 12, camera.SourceViewRotation, SourceClock, hud.ScreenOffsetY));
        hudLayer.AddChild(compass);
        hud.Timer = GodotMissionTimerPresenter.Create(archive!, application.Countdown, application.TimerDisplay, hud.Text, new()
        {
            LevelId = () => MissionLevelId.FromAssetName(geometryAsset), PlayerPresent = () => Player is not null,
            PlayerScriptGate = () => player.Combat.State.FieldE18,
            PlayerSynthesizedInput = () => player.Combat.State.SynthesizedInputActive,
            Random = random.Next, PlaySound = (sound, volume, pitch) => player.Combat.PlaySound(sound, volume, pitch),
            CallModule = codeModules.Modules.CallParameterlessUserFunction,
            BeginModulePresentation = ScorpionApproach.BeginPresentationFrame,
            DrawModulePresentation = ScorpionApproach.DrawOn
        });
        hud.AddChild(hud.Timer);
        var fade = new GodotCampaignFade { Name = "CampaignFade" }; AddChild(fade);
        var saves = GodotSaveConfirmation.Create(application, new GameSaveStore(), sounds.Controller); AddChild(saves);
        var completion = new GodotCampaignCompletion(application, levelCatalog, fade, saves);
        sceneObjects = new(new()
        {
            Player = () => Player, Camera = camera, Registry = Registry, Web = web, Combat = combat,
            Objects = objects, Enemies = enemies, Speech = speech, Messages = messages,
            StopProcessing = StopGameplay, ClearPlayer = () => { Player = null; Registry.Player = null; }
        });
        transitions = new(application, lifecycle, new()
        {
            Archive = archive!, Player = () => Player, KillAllSceneObjects = sceneObjects.KillAllSceneObjects,
            CompleteLevel = completion.CompleteAsync
        });
        playerCommands.Bind(CameraCommandHandler.CreateHandlers(camera.ScriptRig, () => new(player.Controller.OnWall,
                player.Controller.OnCeiling, false, (uint)player.Controller.Action),
                () => GodotObject.IsInstanceValid(camera), () => Player is not null && GodotObject.IsInstanceValid(Player))
            .Concat(FixedCameraCommandHandler.CreateHandlers(triggers, camera.ScriptRig, () => camera.SourcePosition,
                () => camera.SourceRotation, () => GodotObject.IsInstanceValid(camera)))
            .Append(new PlayerScriptCommandHandler(player.Scripts.Start))
            .Append(new PlayerInputCommandHandler(duration => player.Controller.SetIgnoreInputTimer(duration)))
            .Append(new FightMusicTimeCommandHandler(player.Combat))
            .Append(new PlayerShadowBrightnessCommandHandler(playerBrightness.State))
            .Append(new PlayerLookaroundCameraValueCommandHandler(() => lookaround.Aim.State,
                () => Player is { } live && GodotObject.IsInstanceValid(live)))
            .Append(new PlayerSenseBuzzCommandHandler(() => web.AdditionalBodies!.State))
            .Append(new PlayerScriptDeathCommandHandler(() => new GodotPlayerScriptDeath(
                Player ?? throw new InvalidOperationException("Source152 has no actual current player."), Registry),
                () => Player is { } live && GodotObject.IsInstanceValid(live)))
            .Concat(new ushort[] { 215, 216 }.Select(opcode => new FightMusicFadeCommandHandler(opcode,
                () => (Player ?? throw new InvalidOperationException("Fight-music fade requires the actual current player.")).Combat.State)))
            .Concat(PlayerStoryInputCommandHandler.CreateHandlers(cutsceneSkip.State, player.SetSourceFootstepBank))
            .Concat(LevelTransitionCommandHandler.CreateHandlers(transitions)
                .Where(handler => handler.Opcode != (ushort)MissionOpcode.EndLevelNode)));
        player.Faulted += Fail;
    }

    private void BindPlayerWorld(ImportedLevelWorld world)
    {
        if (Player is not { } player || combat is null || ReferenceEquals(world, playerWorld)) return;
        if (playerWorld is not null)
            throw new NotSupportedException("Replacing a live player's environment requires source world-generation teardown.");
        foreach (var claim in pendingModelClaims)
            if (world.Sources.Any(source => ReferenceEquals(source.Scene, claim.Scene)))
                world.ClaimObjectModel(claim.AssetPath, claim.Model);
        pendingModelClaims.Clear();
        objects!.PrepareEnvironment();
        ImportedLevelSource source = world.Sources.Single(source => source.AssetPath.Equals(geometryAsset, StringComparison.OrdinalIgnoreCase));
        PsxZoneGrid grid = PsxZoneGridReader.Read(archive!.Read(geometryAsset), source.Scene.Instances.Count)
            ?? throw new InvalidDataException("The original environment has no trigger grid.");
        player.Controller.TriggerZones = new GodotPlayerTriggerZones(player, world, geometryAsset, grid, application.TriggerSweep,
            () => Registry.SourceList(ActorUpdateGroup.Environment).SourceOrder.OfType<GodotScriptActor>().Where(Registry.Owns).ToArray(),
            checksum => Mission.TriggerCommandPoint(checksum));
        playerWorld = world;
    }
}
