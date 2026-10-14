using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player.Scripting;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Untouched L2A1 autoexec/cinema/census and deferred restart128, then actual scripted-input phases and a bounded legal manual skip.</summary>
internal static class StoryLevelFiveSessionImportChecks
{
    private static readonly ushort[] InitialNodes = [2, 3, 4, 5, 31, 41, 44, 49, 73, 79, 81, 91, 102, 105, 108, 146, 150, 172];
    private static readonly ushort[] RestartOpcodes = [213, 104, 202, 200, 169, 166, 180, 180, 180, 180, 180, 171, 171, 141, 128, 3];
    private static readonly ushort[] RestartBodies = [117, 116, 118, 123, 147, 149, 152];
    private sealed record StartupInputSnapshot(GodotPlayer Player, PlayerScriptProgram? Program, int ProgramsStarted,
        bool Active, int? Checkpoint, byte SkipAllowed, int InputTimer, int ScreenValue, int ScreenShadow,
        bool HookBound, bool ControllerMatches, int HookVisits, int MonitorCalls, bool ModuleLoaded);

    public static async Task VerifyAsync(StoryLevelSession story)
    {
        TriggerFile triggers = story.Registry.Triggers;
        TriggerNode autoexec = triggers.Nodes[0], restartNode = triggers.Nodes[1];
        RestartPoint restart = RestartPoint.Read(restartNode);
        IReadOnlyList<TriggerInstruction> autoProgram = TriggerCommandReader.Read(autoexec), restartProgram = TriggerCommandReader.Read(restartNode);
        GodotMissionLibraries libraries = story.GetNode<GodotMissionLibraries>("MissionLibraries");
        GodotEnemyRuntime enemies = Descendants(story).OfType<GodotEnemyRuntime>().Single();
        GodotLevelObjectRuntime objects = Descendants(story).OfType<GodotLevelObjectRuntime>().Single();
        GodotScorpionApproachModule module = story.ScorpionApproach;
        var initial = new List<GodotScriptActor>();
        var later = new List<(GodotScriptActor Actor, MissionStartupPhase Phase)>();
        var traces = new List<MissionTrace>();
        (GodotScriptActor Actor, MissionStartupPhase Phase)[] startupBodies = [];
        StartupInputSnapshot? startupInput = null;
        GodotPlayer? player = null;
        FixedVector3 restartPosition = default;
        int checks = 0, playerConstructions = 0, gameplayStarts = 0, startupLive = 0, startupLeases = 0;
        bool movieBarrier = false, coverObserved = false, movieObserved = false, skippedMovie = false, loadedWorldAfterPlayer = false;
        Assert(story.Failure is null && story.Startup.Phase == MissionStartupPhase.Autoexec && story.SourceClock == 0 &&
            story.Player is null && story.Registry.Player is null && story.Registry.LiveCount == 0 && libraries.World is null &&
            !libraries.Repository.Loaded.ContainsKey("data/L2A1_G.psx") && !enemies.IsPlayerBound && !objects.IsPlayerBound &&
            objects.PreparedEnvironment is null && !module.SourceModuleLoaded &&
            story.Application.MissionName.Equals("L2A1_T", StringComparison.OrdinalIgnoreCase) && story.Checkpoints.Save.RestartPointName.Length == 0,
            "Actual L2A1 launch attaches before autoexec/player/modules and has no preloaded G bank or invented scene geometry.");
        Assert(autoProgram.Select(instruction => instruction.Opcode).SequenceEqual(new ushort[]
                { 140, 302, 147, 190, 126, 126, 126, 126, 142, 189, 189 }) &&
            Text(autoProgram.Single(instruction => instruction.Opcode == 140)) == "Re_Start_Death" &&
            Word(autoProgram.Single(instruction => instruction.Opcode == 302)) == 6 &&
            Word(autoProgram.Single(instruction => instruction.Opcode == 147)) == 3 &&
            Word(autoProgram.Single(instruction => instruction.Opcode == 190)) == 21 &&
            autoProgram.Where(instruction => instruction.Opcode == 126).Select(Text).SequenceEqual(new[] { "Henchngt", "L2A1_L", "L2A1_O", "expgrnd" }) &&
            Text(autoProgram.Single(instruction => instruction.Opcode == 142)) == "L2A1_O" &&
            autoProgram.Where(instruction => instruction.Opcode == 189).Select(Text).SequenceEqual(new[] { "thug", "l2a1lsc" }),
            "Untouched autoexec0 keeps EndLevelNode6 and GameLevel3 separate before cinema21, exact four banks and thug/l2a1lsc189; no128 appears in autoexec.");
        Assert(restart is { NodeId: 1, Name: "Re_Start_Death" } && restart.Position == new TriggerPosition(83, -256, -8291) &&
            restart.Orientation == new TriggerOrientation(0, 0, 0) && restart.Links.SequenceEqual(new ushort[] { 117, 116, 118, 125, 123, 147, 149, 152 }) &&
            restartProgram.Select(instruction => instruction.Opcode).SequenceEqual(RestartOpcodes) &&
            Text(restartProgram[^2]) == "L2A1_G" && restartProgram[^2].Opcode == 128 && restartProgram[^1].Opcode == 3,
            "The original death restart retains exact placement, eight-link order and source128 immediately before3, after genuine player construction.");
        TriggerActorDefinition authored123 = TriggerActorDefinition.Read(triggers.Nodes[123]);
        int embeddedOffset = checked(triggers.Nodes[123].SourceOffset + authored123.ObjectDataOffset + 18 + 12);
        int embeddedLength = PlayerSynthesizedInput.StreamLength(authored123.BehaviorData.Span[12..], embeddedOffset);
        PlayerScriptProgram authoredInput = PlayerScriptProgram.Read(authored123.BehaviorData.Span.Slice(12, embeddedLength), embeddedOffset);
        story.Registry.Spawned += OnSpawn; story.Mission.Trace += OnTrace;
        story.PlayerConstructed += OnPlayer; story.GameplayStarted += OnGameplay;
        try
        {
            var deadline = Stopwatch.StartNew();
            while (gameplayStarts == 0)
            {
                Healthy();
                Assert(story.SourceClock == 0 && story.Registry.SourceUpdates == 0 && initial.All(actor => actor.SourceUpdates == 0) &&
                    module.MonitorCalls == 0 && module.DisplayCalls == 0,
                    "Real L2A1 startup holds source time/AI/module callbacks through cinema, no-player census and restart.");
                if (story.Cinema.IsPlaying)
                {
                    movieBarrier = true; coverObserved |= story.Cinema.ShowingCover;
                    Assert(story.Startup.Phase == MissionStartupPhase.Autoexec && story.Mission.IsBlocked && initial.Count == 0 &&
                        story.Player is null && libraries.World is null && story.Application.EndLevelNode == 6 && story.Application.GameLevel == 3 &&
                        enemies.Modules.Modules.SourceOrder.Count == 0,
                        "Original cinema21 blocks its actual continuation after pre-player3026/1473 and before authored libraries/modules/census.");
                    if (!story.Cinema.ShowingCover && story.Cinema.Video.IsPlaying()) movieObserved = true;
                    if (!story.Cinema.ShowingCover && story.Cinema.CanSkipMovie)
                    {
                        Assert(movieObserved, "The original L2M1 native stream actually begins before its legal skip gate.");
                        story.Cinema.RequestSkip(); skippedMovie = true;
                    }
                }
                if (deadline.Elapsed > TimeSpan.FromSeconds(90)) throw new TimeoutException("Genuine L2A1 startup did not reach Playing.");
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Healthy();
            GodotPlayer actualPlayer = player ?? throw new InvalidDataException("Actual L2A1 CPlayer construction was not observed.");
            GodotPlayerScripts scripts = actualPlayer.Scripts ?? throw new InvalidDataException("Actual L2A1 player has no script VM.");
            Assert(movieBarrier && coverObserved && movieObserved && skippedMovie && !story.Cinema.IsPlaying && loadedWorldAfterPlayer,
                "Real cinema21 cover/video/legal skip finishes before the18 census; only untouched post-player restart128 loads the actual world.");
            Assert(playerConstructions == 1 && gameplayStarts == 1 && story.Startup.InitialActorsCreated == 18 &&
                initial.Select(actor => actor.SourceNodeId).SequenceEqual(InitialNodes) && initial.OfType<GodotThugActor>().Count() == 15 &&
                initial.OfType<GodotControlActor>().Count() == 3 && startupBodies.Select(entry => entry.Actor.SourceNodeId).SequenceEqual(RestartBodies) &&
                startupBodies.All(entry => entry.Phase == MissionStartupPhase.Restart) && startupBodies.Count(entry => entry.Actor is GodotPowerUpActor) == 5 &&
                startupBodies.Count(entry => entry.Actor is GodotControlActor) == 2 && startupLive == 25 && startupLeases == 20,
                "Actual18 census/18 CBaddy leases precedes one player and seven genuine restart bodies; five pickups and two203 controls yield25 live/20 leases. " + Diagnostics());
            ImportedLevelWorld? loadedWorld = libraries.World;
            Assert(enemies.IsPlayerBound && objects.IsPlayerBound && loadedWorld is not null &&
                loadedWorld.Sources.Select(source => source.AssetPath).SequenceEqual(new[] { "data/L2A1_G.psx", "data/L2A1_O.psx" }, StringComparer.OrdinalIgnoreCase) &&
                loadedWorld.InstanceCount > 0 && loadedWorld.Geometry.GetChildren().Sum(group => group.GetChildCount()) > 0 &&
                loadedWorld.Collision.GetChildren().Sum(group => group.GetChildCount()) > 0 &&
                actualPlayer.Controller.TriggerZones is not null,
                "Actual deferred128 publishes authored G/O native geometry/collision and binds canonical ENV/trigger-grid queries before gameplay begins. " +
                $"enemyBound={enemies.IsPlayerBound}, objectsBound={objects.IsPlayerBound}, worldPresent={loadedWorld is not null}, " +
                $"sourcePaths=[{string.Join(',', loadedWorld?.Sources.Select(source => source.AssetPath) ?? [])}], instances={loadedWorld?.InstanceCount}, " +
                $"geometryChildren={loadedWorld?.Geometry.GetChildren().Sum(group => group.GetChildCount())}, " +
                $"collisionChildren={loadedWorld?.Collision.GetChildren().Sum(group => group.GetChildCount())}, triggerGridBound={actualPlayer.Controller.TriggerZones is not null}.");
            CompletedOnce(autoexec, autoProgram); CompletedOnce(restartNode, restartProgram);
            CompletedOnce(triggers.Nodes[125], TriggerCommandReader.Read(triggers.Nodes[125]));
            CompletedOnce(triggers.Nodes[122], TriggerCommandReader.Read(triggers.Nodes[122]));
            Assert(story.Application.EndLevelNode == 6 && story.Application.GameLevel == 3 &&
                story.Checkpoints.Current is { NodeId: 1, Name: "Re_Start_Death" } &&
                restartPosition == new FixedVector3(restart.Position.X, restart.Position.Y, restart.Position.Z) << 12 &&
                story.Rendering.SourceDepth.Globals is { First: 1024, Second: 128 } && story.PlayerBrightness.State.DarkSurfaceTarget == 160,
                "Early3026 survives restart;1473, original213/205, OT1024/128 and constructor brightness160 reach their actual owners and exact Q12 spawn.");
            uint[] backgrounds = restartProgram.Where(instruction => instruction.Opcode == 171).Select(BackgroundChecksum).ToArray();
            GodotSourceBackdrop[] backdropBodies = story.Rendering.Backdrops.GetChildren().OfType<GodotSourceBackdrop>().OrderBy(body => body.State.Index).ToArray();
            Assert(backgrounds.SequenceEqual(new uint[] { 0x76E13E1B, 0x1827C06F }) && backdropBodies.Length == 2 &&
                backdropBodies.Select(body => body.State.Checksum).SequenceEqual(backgrounds) &&
                story.Registry.SourceList(ActorUpdateGroup.Background).SourceOrder.SequenceEqual(backdropBodies.Reverse()),
                "Both actual171 OBJ backdrops retain original checksums, native meshes and canonical newest-first Background source identity.");
            Assert(startupInput is not null && ReferenceEquals(startupInput.Player, actualPlayer) &&
                scripts.CurrentProgram is { } started && started.Instructions.SequenceEqual(authoredInput.Instructions) && scripts.ProgramsStarted == 1,
                "The exact pre-AI123/player/module initialization snapshot was verified synchronously at GameplayStarted. Later process-frame observation retains the same player/program while genuine AI may already advance checkpoint124 and module0. " +
                InputDiagnostics() + $" startupSnapshot={startupInput}.");
            deadline.Restart();
            while (story.SourceClock < 120)
            {
                Healthy();
                if (deadline.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Actual L2A1 source frames did not advance120 ticks.");
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            Healthy();
            Assert(actualPlayer.SourceUpdates > 0 && story.Registry.SourceUpdates > 0 && initial.OfType<GodotThugActor>().All(actor => actor.SourceUpdates > 0) &&
                actualPlayer.CameraStartup is { State.Initialized: 1 } && scripts.Runtime is { CheckpointNode: 124 } && scripts.Active &&
                actualPlayer.Combat.State.FieldE18 == 8000 && story.PlayerModules.SourceHookVisits == actualPlayer.SourceUpdates && module.MonitorCalls == actualPlayer.SourceUpdates &&
                module.DisplayCalls == 0 && module.TextureLookups == 0 && story.Application.Countdown is { Rate: 4096, Running: 1, DisplayEnabled: 1 } &&
                story.Application.Countdown.RemainingTicks == ScorpionApproachCountdown.Duration(story.Registry.Difficulty),
                "Ordinary source player/thug AI executes real checkpoint124 and one module0 after each full player AI; authored synthesized input/E18 freezes countdown and correctly suppresses module1 texture/render work. " +
                InputDiagnostics() + $" initialThugUpdates=[{string.Join(',', initial.OfType<GodotThugActor>().Select(actor => $"{actor.SourceNodeId}:{actor.SourceUpdates}"))}], registryUpdates={story.Registry.SourceUpdates}, cameraInitialized={actualPlayer.CameraStartup?.State.Initialized}.");
            foreach (GodotThugActor thug in initial.OfType<GodotThugActor>()) thug.Model.VerifyCurrentPose();
            actualPlayer.Model.VerifyCurrentPose(); story.PlayerBrightness.VerifyUploads();
            Assert(story.PlayerBrightness.RenderVisits > 0 && story.PlayerBrightness.LitPartCount == 18 &&
                story.Rendering.SourceDepth.RenderScratch is { First: 1024, Second: 128 } && backdropBodies.All(body => body.State.BodyFlags == 0x10),
                "Actual L2A1 source render retains all18 player lighting parts, authored signed OT and both genuine updated background bodies.");
            await VerifyBoundedManualSkip();
            Assert(module.DisplayCalls > 0 && module.TextureLookups == 5 && module.LiveTextures == 5 &&
                module.Draws.Count == 23 && module.SubmittedPrimitives == 23 && module.AllocatedPolys == 23 &&
                module.Draws.Count(draw => draw.Texture == 2) == 19 && story.Application.Countdown.RemainingTicks < ScorpionApproachCountdown.Duration(story.Registry.Difficulty),
                "After the legal source input skip, actualHUD module1 resolves five original world textures and submits its23 genuine progress quads while countdown decay resumes. " +
                InputDiagnostics() + $" draws={module.Draws.Count}, submitted={module.SubmittedPrimitives}, allocated={module.AllocatedPolys}, trackPieces={module.Draws.Count(draw => draw.Texture == 2)}.");
            CompletedOnce(autoexec, autoProgram); CompletedOnce(restartNode, restartProgram);
            GodotScriptActor[] allocated = Enum.GetValues<ActorUpdateGroup>().SelectMany(story.Registry.SourceActors)
                .Concat(story.Registry.SuspendedSourceList.SourceOrder.OfType<GodotScriptActor>()).Distinct().ToArray();
            GodotPlayerModuleHook hook = story.PlayerModules;
            story.SceneObjects.KillAllSceneObjects(LevelExitReason.RetryRequested);
            Assert(story.SceneObjects.IsDestroyed && story.Player is null && story.Registry.Player is null && !GodotObject.IsInstanceValid(actualPlayer) &&
                hook.ResourcesDisposed && !hook.PlayerHookBound && allocated.All(actor => !GodotObject.IsInstanceValid(actor)) &&
                story.Registry.LiveCount == 0 && story.Registry.BaddyConstructionState.Count == 0 &&
                Enum.GetValues<ActorUpdateGroup>().All(group => story.Registry.SourceList(group).Count == 0),
                "Actual L2A1 Init_KillAll deletes player/generic hook and every original active/suspended source body, background and constructor lease.");
            ImageTexture[] textures = Enumerable.Range(0, 5).Select(module.SourceTexture).Cast<ImageTexture>().ToArray();
            enemies.Modules.UnloadAllCodeModules();
            Assert(enemies.Modules.Modules.SourceOrder.Count == 0 && !module.SourceModuleLoaded && module.LiveTextures == 0 &&
                textures.All(texture => !GodotObject.IsInstanceValid(texture)) && story.Application.Countdown.Running == 0 && story.Application.Countdown.DisplayEnabled == 0 &&
                story.Application.Countdown.LastUpdateTick == story.SourceClock,
                "Genuine fixed-order l2a1lsc module clear releases native progress textures and the same panel countdown after body deletion.");
            GD.Print($"Passed {checks} actual L2A1 story checks: unchanged3026/1473/cinema21,18 no-player constructors, genuine post-player128/restart, actual470D/470E/checkpoint124/module0 phases, bounded legal input skip and real progressHUD/teardown. Full mission traversal was not exercised.");
        }
        finally
        {
            story.Registry.Spawned -= OnSpawn; story.Mission.Trace -= OnTrace;
            story.PlayerConstructed -= OnPlayer; story.GameplayStarted -= OnGameplay;
        }

        void OnSpawn(GodotScriptActor actor)
        {
            if (story.Startup.Phase != MissionStartupPhase.CreatingInitialActors) { later.Add((actor, story.Startup.Phase)); return; }
            Assert(story.Player is null && story.Registry.Player is null && libraries.World is null && !enemies.IsPlayerBound && !objects.IsPlayerBound &&
                story.SourceClock == 0 && actor.SourceUpdates == 0,
                $"Actual L2A1 initial{actor.SourceNodeId}/{actor.SourceActorType} constructor precedes CPlayer/world128 and sees no AI.");
            initial.Add(actor);
        }
        void OnPlayer()
        {
            playerConstructions++;
            Assert(initial.Select(actor => actor.SourceNodeId).SequenceEqual(InitialNodes) && story.Registry.LiveCount == 18 &&
                story.Registry.BaddyConstructionState.Count == 18 && libraries.World is null && !libraries.Repository.Loaded.ContainsKey("data/L2A1_G.psx") &&
                story.Startup.Phase == MissionStartupPhase.SelectingRestart && !enemies.IsPlayerBound && !objects.IsPlayerBound,
                "Only the exact18 no-player census constructs CPlayer; original geometry128 has still not run.");
            Assert(enemies.Modules.Modules.SourceOrder.Select(record => record.Name).SequenceEqual(new[] { "l2a1lsc", "thug" }) &&
                module.SourceModuleLoaded && story.Application.PlayerModuleHook.ModuleName == "l2a1lsc" && story.Application.PlayerModuleHook.FunctionMask == 1 &&
                story.Application.Countdown.Running == 0 && module.MonitorCalls == 0 && module.DisplayCalls == 0,
                "Actual189 initializes the genuine module/head and installs its process callback before CPlayer without executing countdown or presentation functions.");
        }
        void OnGameplay()
        {
            gameplayStarts++; player = story.Player; startupBodies = later.ToArray();
            startupLive = story.Registry.LiveCount; startupLeases = story.Registry.BaddyConstructionState.Count;
            restartPosition = player?.Controller.Position ?? throw new InvalidDataException("Actual L2A1 gameplay has no CPlayer.");
            Assert(story.SourceClock == 0 && story.Registry.SourceUpdates == 0 && startupBodies.All(entry => entry.Actor.SourceUpdates == 0) &&
                story.Mission.PendingCount == 0 && !story.Mission.IsBlocked,
                "Genuine restart and linked125/122 command lists drain before the first real player/actor/module source frame.");
            GodotPlayer actual = player ?? throw new InvalidDataException("Actual L2A1 startup lacks its original player.");
            GodotPlayerScripts scripts = actual.Scripts ?? throw new InvalidDataException("Actual L2A1 startup lacks its original player input VM.");
            startupInput = new(actual, scripts.CurrentProgram, scripts.ProgramsStarted, scripts.Active, scripts.Runtime?.CheckpointNode,
                scripts.SkipState.Allowed, actual.Combat.State.FieldE18, story.Application.CinematicScreen.WideScreenValue,
                story.Application.CinematicScreen.WideScreenShadow, story.PlayerModules.PlayerHookBound,
                ReferenceEquals(story.PlayerModules.BoundPlayerController, actual.Controller), story.PlayerModules.SourceHookVisits,
                module.MonitorCalls, module.SourceModuleLoaded);
            Assert(startupInput.Program is { } original && original.Instructions.SequenceEqual(authoredInput.Instructions) &&
                startupInput.ProgramsStarted == 1 && startupInput.Active && startupInput.Checkpoint == 0 && startupInput.SkipAllowed == 1 &&
                startupInput.InputTimer == 8000 && startupInput.ScreenValue == 30 && startupInput.ScreenShadow == 30 &&
                startupInput.HookBound && startupInput.ControllerMatches && startupInput.HookVisits == 0 && startupInput.MonitorCalls == 0 &&
                startupInput.ModuleLoaded && actual.SourceUpdates == 0,
                "At the exact GameplayStarted boundary, real123470D/42B9/470E owns its untouched active program/checkpoint0, linked125/222 permission, E188000, both screen globals30 and the genuine generic module hook before any player AI. " +
                InputDiagnostics() + $" startupSnapshot={startupInput}, expectedInstructions=[{string.Join(';', authoredInput.Instructions.Select(instruction => $"{instruction.DueTime}/{instruction.Opcode}/{instruction.First}/{instruction.Second}/{instruction.Third}@{instruction.SourceOffset:X}"))}].");
        }
        void OnTrace(MissionTrace trace)
        {
            traces.Add(trace);
            if (trace.NodeId == 0 && trace.Kind == MissionTraceKind.CommandCompleted && trace.Opcode == 302)
                Assert(story.Player is null && story.Registry.Player is null && story.Application.EndLevelNode == 6,
                    "Original autoexec3026 writes only the application end-node while the player is absent.");
            if (trace.NodeId == 1 && trace.Opcode == 128)
            {
                if (trace.Kind == MissionTraceKind.CommandStarted)
                    Assert(story.Player is not null && story.Registry.Player is not null && libraries.World is null &&
                        initial.Count == 18 && story.Startup.Phase == MissionStartupPhase.Restart && story.SourceClock == 0,
                        "Authored restart128 begins only after the actual player/census and before source time, with no earlier map preload.");
                else if (trace.Kind == MissionTraceKind.CommandCompleted)
                {
                    loadedWorldAfterPlayer = true;
                    Assert(libraries.World is not null && libraries.Repository.Loaded.ContainsKey("data/L2A1_G.psx"),
                        "Actual source128 completion owns the newly loaded original G region.");
                }
            }
        }
        async Task VerifyBoundedManualSkip()
        {
            GodotPlayer actual = player!;
            GodotPlayerScripts scripts = actual.Scripts!;
            GodotPlayerCutsceneSkip skip = scripts.CutsceneSkip!;
            Assert(story.Registry.Difficulty != 0 && scripts.Active && scripts.SkipState.Allowed == 1 && scripts.Runtime?.CheckpointNode == 124 &&
                InputMap.HasAction("source_pad_cross") && InputMap.ActionGetEvents("source_pad_cross").Count > 0,
                "The bounded skip uses actual normal-difficulty physical Cross bindings and authored222/checkpoint124 permission. " +
                InputDiagnostics() + $" difficulty={story.Registry.Difficulty}, crossAction={InputMap.HasAction("source_pad_cross")}, crossEvents={(InputMap.HasAction("source_pad_cross") ? InputMap.ActionGetEvents("source_pad_cross").Count : 0)}.");
            int before = skip.CleanupCount, manual = skip.ManualSkips;
            SendCross(true);
            try
            {
                var deadline = Stopwatch.StartNew();
                while (skip.CleanupCount == before || module.DisplayCalls == 0)
                {
                    Healthy();
                    if (deadline.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("Actual L2A1 physical Cross skip did not reach original post-pending cleanup/HUD.");
                    await story.ToSignal(story.GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
            finally { SendCross(false); }
            Assert(skip.ManualSkips == manual + 1 && skip.CleanupCount == before + 1 && !scripts.Active && !story.Registry.Environment.FastForwardDeath &&
                actual.Combat.State.FieldE18 == 0 && later.Any(entry => entry.Actor.SourceNodeId == 123 && entry.Actor.ScriptState.SkipDuringDeath),
                "One real physical skip completes original skip-enabled123 through its final470D0, clears player VM/abort after genuine mission events, and reopens the actual HUD gate. " +
                InputDiagnostics() + $" manualSkips={manual}->{skip.ManualSkips}, cleanup={before}->{skip.CleanupCount}, abort={story.Registry.Environment.FastForwardDeath}, " +
                $"controls123=[{string.Join(',', later.Where(entry => entry.Actor.SourceNodeId == 123).Select(entry => $"skip={entry.Actor.ScriptState.SkipDuringDeath},active={entry.Actor.ScriptState.Active},wait={entry.Actor.ScriptState.WaitTicks}"))}].");
            await story.ToSignal(story.GetTree(), SceneTree.SignalName.ProcessFrame);
            await story.ToSignal(story.GetTree(), SceneTree.SignalName.ProcessFrame);
            Healthy();
            Assert(skip.CleanupCount == before + 1 && !scripts.Active,
                "Subsequent original source/render frames do not repeat the completed L2A1 manual cleanup. " +
                InputDiagnostics() + $" cleanup={before}->{skip.CleanupCount}, manualSkips={manual}->{skip.ManualSkips}.");
        }
        void CompletedOnce(TriggerNode node, IReadOnlyList<TriggerInstruction> expected)
        {
            MissionTrace[] completed = traces.Where(trace => trace.Kind == MissionTraceKind.CommandCompleted && trace.NodeId == node.Id).ToArray();
            Assert(completed.Select(trace => (trace.Opcode, trace.SourceOffset)).SequenceEqual(expected.Select(instruction => ((ushort?)instruction.Opcode, instruction.SourceOffset))),
                $"Original L2A1 node{node.Id} executes every untouched command once in exact order. " + Diagnostics());
        }
        string Diagnostics() => $"phase={story.Startup.Phase}, clock={story.SourceClock}, live={story.Registry.LiveCount}, leases={story.Registry.BaddyConstructionState.Count}, " +
            $"initial=[{string.Join(',', initial.Select(actor => actor.SourceNodeId))}], later=[{string.Join(',', later.Select(entry => $"{entry.Actor.SourceNodeId}/{entry.Actor.SourceActorType}/{entry.Phase}"))}], " +
            $"traces=[{string.Join(',', traces.Where(trace => trace.Kind == MissionTraceKind.CommandCompleted).Select(trace => $"{trace.NodeId}/{trace.Opcode}@{trace.SourceOffset:X}"))}].";
        string InputDiagnostics()
        {
            GodotPlayer? actual = player ?? story.Player;
            GodotPlayerScripts? scripts = actual?.Scripts;
            GodotPlayerModuleHook? hook = story.PlayerModules;
            string instructions = scripts?.CurrentProgram is { } current
                ? string.Join(';', current.Instructions.Select(instruction => $"{instruction.DueTime}/{instruction.Opcode}/{instruction.First}/{instruction.Second}/{instruction.Third}@{instruction.SourceOffset:X}"))
                : "<absent>";
            MissionCountdown countdown = story.Application.Countdown;
            return $"clock={story.SourceClock}, playerUpdates={actual?.SourceUpdates}, programPresent={scripts?.CurrentProgram is not null}, " +
                $"programsStarted={scripts?.ProgramsStarted}, active={scripts?.Active}, checkpoint={scripts?.Runtime?.CheckpointNode}, instructionIndex={scripts?.Runtime?.InstructionIndex}, elapsed={scripts?.Runtime?.ElapsedTicks}, " +
                $"skipAllowed={scripts?.SkipState.Allowed}, E18={actual?.Combat.State.FieldE18}, suppression={actual?.Controller.InputSuppression.RemainingTicks}, " +
                $"screen={story.Application.CinematicScreen.WideScreenValue}/{story.Application.CinematicScreen.WideScreenShadow}, " +
                $"hookBound={hook?.PlayerHookBound}, controllerMatch={ReferenceEquals(hook?.BoundPlayerController, actual?.Controller)}, hookVisits={hook?.SourceHookVisits}, " +
                $"monitorCalls={module.MonitorCalls}, displayCalls={module.DisplayCalls}, moduleLoaded={module.SourceModuleLoaded}, textureLookups={module.TextureLookups}, liveTextures={module.LiveTextures}, " +
                $"countdown={countdown.Running}/{countdown.DisplayEnabled}, rate={countdown.Rate}, remaining={countdown.RemainingTicks}, expectedDuration={ScorpionApproachCountdown.Duration(story.Registry.Difficulty)}, lastTick={countdown.LastUpdateTick}, " +
                $"instructions=[{instructions}].";
        }
        void Healthy()
        {
            if (story.Failure is { } failure) throw new InvalidDataException("Actual L2A1 production dependency failed.", failure);
            if (story.Startup.Failure is { } startupFailure) throw new InvalidDataException("Actual L2A1 startup failed.", startupFailure);
        }
        void Assert(bool condition, string evidence) { if (!condition) throw new InvalidDataException("Native L2A1 story: " + evidence); checks++; }
    }

    private static void SendCross(bool pressed)
    {
        using var key = new InputEventKey { PhysicalKeycode = Key.Space, Keycode = Key.Space, Pressed = pressed };
        Input.ParseInputEvent(key);
    }
    private static ushort Word(TriggerInstruction instruction) => BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span);
    private static string Text(TriggerInstruction instruction)
    {
        int end = instruction.Arguments.Span.IndexOf((byte)0);
        if (end < 0) throw new InvalidDataException("Original mission string has no terminator.");
        return Encoding.Latin1.GetString(instruction.Arguments.Span[..end]);
    }
    private static uint BackgroundChecksum(TriggerInstruction instruction)
    {
        int padding = (-(instruction.SourceOffset + 2)) & 3;
        return BinaryPrimitives.ReadUInt32LittleEndian(instruction.Arguments.Span[padding..]);
    }
    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child)) yield return descendant;
        }
    }
}
