using System.Diagnostics;
using System.Text;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Untouched L1A2 story autoexec/census/death restart, genuine Black Cat and player checkpoint program.</summary>
internal static class StoryLevelTwoSessionImportChecks
{
    private static readonly ushort[] InitialNodes = [15, 16, 68, 74, 100, 102, 103, 104, 105, 151, 154, 156, 158, 163, 168];
    private static readonly ushort[] RestartOpcodes = [213, 170, 195, 171, 171, 171, 104, 202, 200, 141, 166, 169,
        180, 180, 180, 180, 180, 180, 180, 180, 180, 180, 184, 302, 3];

    public static async Task VerifyAsync(StoryLevelSession scene)
    {
        TriggerFile triggers = scene.Registry.Triggers;
        TriggerNode autoexec = triggers.Nodes[1], restartNode = triggers.Nodes[0], playerPoint = triggers.Nodes[114];
        RestartPoint restart = RestartPoint.Read(restartNode);
        IReadOnlyList<TriggerInstruction> autoProgram = TriggerCommandReader.Read(autoexec), restartProgram = TriggerCommandReader.Read(restartNode);
        IReadOnlyList<TriggerInstruction> pointProgram = TriggerCommandReader.Read(playerPoint);
        TriggerInstruction playerInput = pointProgram.Single(instruction => instruction.Opcode == 199);
        PlayerScriptProgram originalInput = PlayerScriptProgram.Read(playerInput.Arguments.Span, playerInput.SourceOffset + 2);
        var traces = new List<MissionTrace>();
        var initial = new List<GodotScriptActor>();
        var later = new List<(GodotScriptActor Actor, MissionStartupPhase Phase)>();
        (GodotScriptActor Actor, MissionStartupPhase Phase)[] startupBodies = [];
        GodotSourceBackdrop[] backgrounds = [];
        GodotBlackCatActor? cat = null;
        GodotPlayer? player = null;
        int checks = 0, constructedPlayers = 0, gameplayStarts = 0, startupCount = 0;
        FixedVector3 restartPosition = default;
        uint startClock = scene.SourceClock;
        Assert(scene.Failure is null && scene.Startup.Phase == MissionStartupPhase.Autoexec && startClock == 0 &&
            scene.Registry.LiveCount == 0 && scene.Registry.Player is null && scene.Player is null &&
            scene.Application.MissionName.Equals("L1A2_T", StringComparison.OrdinalIgnoreCase) && scene.Checkpoints.Save.RestartPointName.Length == 0,
            "The genuine empty-save L1A2 launch attaches before autoexec, all no-player constructors and any source clock advance.");
        Assert(autoexec.TypeCode == 4 && autoProgram.Select(instruction => instruction.Opcode)
            .SequenceEqual(new ushort[] { 140, 147, 126, 126, 126, 126, 142, 128, 189, 189 }) &&
            Text(autoProgram.Single(instruction => instruction.Opcode == 140)) == "Re_Start_Death" &&
            autoProgram.Where(instruction => instruction.Opcode == 189).Select(Text).SequenceEqual(new[] { "thug", "blackcat" }),
            "Original L1A2 autoexec1 selects the death restart, loads the authored libraries and exactly its genuine thug/Black Cat modules; it requests no movie.");
        Assert(restart.Name == "Re_Start_Death" && restart.NodeId == 0 && restart.Position == new TriggerPosition(-16997, -3719, -9780) &&
            restart.Links.SequenceEqual(new ushort[] { 67, 113, 111, 114, 140 }) &&
            restartProgram.Select(instruction => instruction.Opcode).SequenceEqual(RestartOpcodes) &&
            pointProgram.Select(instruction => instruction.Opcode).SequenceEqual(new ushort[] { 195, 199 }) &&
            originalInput.Instructions[0].Opcode == PlayerScriptOpcode.SetCheckpoint && originalInput.Instructions[0].First == 124 &&
            MissionRuntime.ReadLinks(triggers.Nodes[124]).SequenceEqual(new ushort[] { 142 }),
            "Untouched restart0 retains its full command list and67/113/111/114/140 pulse order; linked114 starts the genuine checkpoint124 player program and real target142.");

        scene.Registry.Spawned += OnSpawn; scene.Mission.Trace += OnTrace;
        scene.PlayerConstructed += OnPlayer; scene.GameplayStarted += OnGameplay;
        try
        {
            var deadline = Stopwatch.StartNew();
            while (gameplayStarts == 0)
            {
                Healthy();
                Assert(scene.SourceClock == startClock && scene.Registry.SourceUpdates == 0 && initial.All(actor => actor.SourceUpdates == 0) && !scene.Cinema.IsPlaying,
                    "Autoexec, constructor census and restart keep actual AI/time stopped; L1A2 has no invented intro barrier.");
                if (deadline.Elapsed > TimeSpan.FromSeconds(45)) throw new TimeoutException("Actual L1A2 startup did not reach Playing.");
                await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Healthy();
            Assert(constructedPlayers == 1 && gameplayStarts == 1 && scene.Startup.InitialActorsCreated == 15 &&
                scene.Startup.Phase == MissionStartupPhase.Playing && scene.Startup.Failure is null && startupCount == 19 &&
                initial.Select(actor => actor.SourceNodeId).SequenceEqual(InitialNodes) && initial.OfType<GodotPlatformActor>().Count() == 4 &&
                initial.OfType<GodotControlActor>().Count() == 11,
                "L1A2 completes its exact15 no-player census, one real CPlayer and four restart actor constructors once, preserving the original source count19.");
            Assert(startupBodies.Select(entry => entry.Actor.SourceNodeId).SequenceEqual(new ushort[] { 67, 113, 111, 140 }) &&
                startupBodies.All(entry => entry.Phase == MissionStartupPhase.Restart) && cat is not null &&
                cat.State.SourceConstructionOrdinal == 17 && cat.State.ActorPhase == 17 && cat.State.LevelId == 0x102 && cat.Model.SourcePartCount >= 18,
                "The actual restart pulse constructs67/113, real Black Cat319/111 at source postincrement ordinal17, then140; all use canonical model/count ownership after genuine player binding. " +
                $"Startup=[{string.Join(',', startupBodies.Select(entry => $"{entry.Actor.SourceNodeId}/{entry.Actor.SourceActorType}/{entry.Phase}"))}], " +
                $"allLater=[{string.Join(',', later.Select(entry => $"{entry.Actor.SourceNodeId}/{entry.Actor.SourceActorType}/{entry.Phase}"))}], " +
                $"cat={(cat is null ? "absent" : $"node{cat.SourceNodeId},ordinal{cat.State.SourceConstructionOrdinal},phase{cat.State.ActorPhase},level{cat.State.LevelId:X},parts{cat.Model.SourcePartCount},valid{GodotObject.IsInstanceValid(cat)},updates{cat.SourceUpdates}")}, " +
                $"startupCount={startupCount}, live={scene.Registry.LiveCount}, baddies={scene.Registry.BaddyConstruction.State.Count}, sourceClock={scene.SourceClock}, startupPhase={scene.Startup.Phase}, " +
                $"restartTraces=[{string.Join(',', traces.Where(trace => trace.NodeId == 0).Select(trace => $"{trace.Kind}/{trace.Opcode}@{trace.SourceOffset:X}"))}].");
            Assert(scene.Checkpoints.Current is { NodeId: 0, Name: "Re_Start_Death" } &&
                scene.Checkpoints.Save.RestartPointName == "Re_Start_Death" && scene.Application.EndLevelNode == 81 &&
                scene.PlayerBrightness.State.DarkSurfaceTarget == 140 && scene.Rendering.SourceDepth.Globals is { First: 300, Second: -60 } &&
                restartPosition == new FixedVector3(restart.Position.X, restart.Position.Y, restart.Position.Z) << 12,
                "Authored restart0 applies exact player placement, save-name handoff,302 end-node81,184 brightness140 and signed166/169 globals300/-60.");
            CompletedOnce(autoexec, autoProgram); CompletedOnce(restartNode, restartProgram); CompletedOnce(playerPoint, pointProgram);
            Assert(traces.Count(trace => trace.Kind == MissionTraceKind.Queued && trace.NodeId == 0) == 1 &&
                traces.Count(trace => trace.Kind == MissionTraceKind.Queued && trace.NodeId == 114) == 1 && scene.Mission.CommandPoints[114].PulsesReceived == 1 &&
                backgrounds.Length == 3 && backgrounds.Select(backdrop => backdrop.State.Checksum)
                    .SequenceEqual(new uint[] { 1657896729, 370132326, 3733145154 }) &&
                backgrounds.All(backdrop => GodotObject.IsInstanceValid(backdrop) && backdrop.Mesh is ArrayMesh mesh && mesh.GetSurfaceCount() > 0) &&
                scene.Rendering.Backdrops.Count == 3,
                "Original restart pulse114 and three real source502 background meshes are constructed once, retaining source head order and independent original OBJ checksums.");
            GodotPlayer actualPlayer = player ?? throw new InvalidDataException("Actual L1A2 player construction was not observed.");
            GodotPlayerScripts actualScripts = actualPlayer.Scripts ?? throw new InvalidDataException("Actual L1A2 player script ownership is missing.");
            Assert(actualScripts.CurrentProgram is { } current && current.Instructions.SequenceEqual(originalInput.Instructions),
                "The real player owns the untouched114 program, including original scheduled actions/voice and checkpoint124; no substitute script was installed.");
            deadline.Restart();
            while (scene.SourceClock < 120 || actualScripts.Runtime?.CheckpointNode != 124)
            {
                Healthy();
                if (deadline.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Actual L1A2 source player/NPC frames did not advance to checkpoint124.");
                await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            Healthy();
            GodotBlackCatActor actualCat = cat ?? throw new InvalidDataException("The real319 actor was not observed.");
            Assert(scene.Registry.SourceUpdates > 0 && actualCat.SourceUpdates > 0 && actualCat.FrameTiming.AccumulatedTicks > 0 &&
                actualPlayer.CameraStartup?.State.Initialized == 1 && actualScripts.Runtime is { CheckpointNode: 124, ElapsedTicks: > 0 } &&
                actualScripts.Active && actualPlayer.Combat.State.SynthesizedInputActive,
                "Completed startup releases actual world phases,319 animation/AI, first-player camera initialization and the retained124 input VM with canonical1AC publication.");
            actualCat.Model.VerifyCurrentPose(); actualPlayer.Model.VerifyCurrentPose();
            Assert(traces.Any(trace => trace.Kind == MissionTraceKind.CommandCompleted && trace.NodeId == 141 && trace.Opcode == 222) &&
                actualScripts.SkipState.Allowed == 1 && scene.PlayerBrightness.RenderVisits > 0 && scene.PlayerBrightness.LitPartCount == 18 &&
                scene.Rendering.SourceDepth.RenderScratch is { First: 300, Second: -60 } &&
                backgrounds.All(backdrop => backdrop.State.BodyFlags == 0x10),
                "Real control140 completion reaches source222 at point141, while actual render phases keep all18 player lighting parts and original L1A2 OT/background state.");
            scene.PlayerBrightness.VerifyUploads();
            Assert(constructedPlayers == 1 && gameplayStarts == 1 && scene.Startup.InitialActorsCreated == 15,
                "Subsequent real source frames do not replay constructor/startup phases.");
            CompletedOnce(restartNode, restartProgram); CompletedOnce(playerPoint, pointProgram);
            using (var input = new PlayerCheckpointStoryInputImportProbe(scene, Assert))
                await input.VerifyAsync();
            GodotScriptActor[] allocated = Enum.GetValues<ActorUpdateGroup>().SelectMany(scene.Registry.SourceActors)
                .Concat(scene.Registry.SuspendedSourceList.SourceOrder.OfType<GodotScriptActor>()).Distinct().ToArray();
            scene.SceneObjects.KillAllSceneObjects(LevelExitReason.RetryRequested);
            Assert(scene.SceneObjects.IsDestroyed && scene.Player is null && scene.Registry.Player is null &&
                allocated.All(actor => !GodotObject.IsInstanceValid(actor)) && backgrounds.All(backdrop => !GodotObject.IsInstanceValid(backdrop)) &&
                !GodotObject.IsInstanceValid(actualPlayer) && scene.Registry.LiveCount == 0 && scene.Registry.BaddyConstruction.State.Count == 0 &&
                scene.Rendering.Backdrops.Count == 0 && Enum.GetValues<ActorUpdateGroup>().All(group => scene.Registry.SourceList(group).Count == 0),
                "Actual L1A2 Init_KillAll deletes its player, Black Cat and all active/suspended native bodies/backdrops and releases each genuine constructor lease.");
            GD.Print($"Passed {checks} actual L1A2 story checks: autoexec1,15 no-player constructors, original restart0/319/111/114 checkpoint124,302/502 metadata, live222/AI/lighting, physical checkpoint124 skip/Prior/pending phases and genuine teardown.");
        }
        finally
        {
            scene.Registry.Spawned -= OnSpawn; scene.Mission.Trace -= OnTrace;
            scene.PlayerConstructed -= OnPlayer; scene.GameplayStarted -= OnGameplay;
        }

        void OnSpawn(GodotScriptActor actor)
        {
            if (scene.Startup.Phase == MissionStartupPhase.CreatingInitialActors)
            {
                Assert(scene.Player is null && scene.Registry.Player is null && scene.SourceClock == startClock && actor.SourceUpdates == 0,
                    $"Actual initial L1A2 node{actor.SourceNodeId} is constructed without a player/AI clock.");
                initial.Add(actor);
            }
            else later.Add((actor, scene.Startup.Phase));
        }
        void OnTrace(MissionTrace trace) => traces.Add(trace);
        void OnPlayer()
        {
            constructedPlayers++;
            Assert(initial.Select(actor => actor.SourceNodeId).SequenceEqual(InitialNodes) && scene.Registry.BaddyConstruction.State.Count == 15 &&
                scene.Startup.Phase == MissionStartupPhase.SelectingRestart && scene.Player is not null && ReferenceEquals(scene.Registry.Player, scene.Player) &&
                scene.SourceClock == 0 && initial.All(actor => actor.SourceUpdates == 0),
                "Only the completed original L1A2 census constructs the real player before restart and any source AI.");
            GodotControlActor control = initial.OfType<GodotControlActor>().Single(actor => actor.SourceNodeId == 154);
            Assert(control.ScriptState.WaitTicks == 8100 && !control.ScriptState.SkipDuringDeath && control.Body.Recorded.ElapsedTicks == 16200,
                "Full production census executes actual154 raw4511/4512 and exact stationary4280 rather than bypassing its inherited constructor.");
        }
        void OnGameplay()
        {
            gameplayStarts++; player = scene.Player; startupBodies = later.ToArray(); startupCount = scene.Registry.BaddyConstruction.State.Count;
            cat = startupBodies.Select(entry => entry.Actor).OfType<GodotBlackCatActor>().Single();
            backgrounds = scene.Registry.SourceList(ActorUpdateGroup.Background).SourceOrder.OfType<GodotSourceBackdrop>().ToArray();
            restartPosition = player?.Controller.Position ?? throw new InvalidDataException("Actual gameplay began without CPlayer.");
            Assert(scene.SourceClock == 0 && scene.Registry.SourceUpdates == 0 && startupBodies.All(entry => entry.Actor.SourceUpdates == 0),
                "Restart completes before the first actual source actor/player simulation frame.");
        }
        void CompletedOnce(TriggerNode node, IReadOnlyList<TriggerInstruction> expected)
        {
            MissionTrace[] completed = traces.Where(trace => trace.Kind == MissionTraceKind.CommandCompleted && trace.NodeId == node.Id).ToArray();
            Assert(completed.Select(trace => (trace.Opcode, trace.SourceOffset)).SequenceEqual(expected.Select(instruction => ((ushort?)instruction.Opcode, instruction.SourceOffset))),
                $"Actual node{node.Id} retains every original instruction/order exactly once; observed[{string.Join(',', completed.Select(trace => $"{trace.Opcode}@{trace.SourceOffset:X}"))}].");
        }
        void Healthy()
        {
            if (scene.Failure is { } failure) throw new InvalidDataException("Actual L1A2 production dependency failed.", failure);
            if (scene.Startup.Failure is { } startupFailure) throw new InvalidDataException("Actual L1A2 startup failed.", startupFailure);
        }
        void Assert(bool condition, string evidence)
        { if (!condition) throw new InvalidDataException($"Native L1A2 story: {evidence}"); checks++; }
    }
    private static string Text(TriggerInstruction instruction)
    {
        int end = instruction.Arguments.Span.IndexOf((byte)0);
        if (end < 0) throw new InvalidDataException("Original string has no terminator.");
        return Encoding.Latin1.GetString(instruction.Arguments.Span[..end]);
    }
}
