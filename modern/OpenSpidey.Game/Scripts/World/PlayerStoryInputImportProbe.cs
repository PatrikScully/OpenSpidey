using System.Diagnostics;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Observes the actual startup/VM/web/pending-command boundaries; injects a real engine input action.</summary>
internal sealed class PlayerStoryInputImportProbe : IDisposable
{
    private const string CrossAction = "source_pad_cross";
    private readonly StoryLevelSession scene;
    private readonly GodotPlayer player;
    private readonly PlayerFollowCamera camera;
    private readonly GodotPlayerCutsceneSkip skip;
    private readonly GodotSpeechPlayer speech;
    private readonly Action<bool, string> assert;
    private int aiVisits;
    private int observedCleanup;
    private int idleAnimationVisits;
    private int expectedStandClip;
    private bool preStandCrawling;
    private int? preStandHeldFlags;
    private bool requestingSkip;
    private bool sawAbortBeforePrivateWebs;
    private bool disposed;
    private int expectedCheckpoint;
    private readonly List<string> missionCommands = [];
    private readonly List<string> spawnedControls = [];
    private readonly List<MissionTrace> traces = [];
    private readonly List<ushort> spawnedNodes = [];
    private CleanupSnapshot? cleanupSnapshot;

    public PlayerStoryInputImportProbe(StoryLevelSession scene, Action<bool, string> assert)
    {
        this.scene = scene; this.assert = assert;
        player = scene.Player ?? throw new InvalidDataException("Native story input proof requires the actual source player.");
        camera = Descendants(scene).OfType<PlayerFollowCamera>().Single();
        speech = Descendants(scene).OfType<GodotSpeechPlayer>().Single();
        skip = player.Scripts?.CutsceneSkip ?? throw new InvalidDataException("Actual story222 has no genuine manual skip/cleanup owner.");
        assert(player.CameraStartup is { State.Initialized: 0 } && player.SourceUpdates == 0 &&
            ReferenceEquals(camera.SourceProjectionCache, scene.Application.CameraProjectionCache) &&
            ReferenceEquals(camera.Target, player),
            "Playing begins with genuine player53C uninitialized, the same camera tripod player and application-owned retained projection cache.");
        player.Controller.BeginPlayerAi += BeforePrivateWebs;
        skip.IdleAnimationPlayed += BeforeStand;
        skip.CleanupCompleted += AfterPendingCleanup;
        scene.Mission.Trace += RecordMission;
        scene.Registry.Spawned += RecordSpawn;
    }

    public async Task VerifyManualSkipAsync()
    {
        assert(aiVisits > 0 && player.CameraStartup is { State.Initialized: 1 },
            "Actual player AI performs the genuine first-camera latch before its private effect phase.");
        assert(player.Scripts is { Active: true } && skip.State.Allowed == 1 &&
            player.Scripts.Runtime is { CheckpointNode: 0 } && scene.Registry.Difficulty != 0 && scene.Application.ForceEarlyLevelExit == 0,
            "Untouched L1A1 script85 is still active, has checkpoint0, and actual live source222 enabled the original normal-difficulty manual gate.");
        assert(InputMap.HasAction(CrossAction) && InputMap.ActionGetEvents(CrossAction).Count > 0,
            "The raw physical Cross slot has actual native keyboard/controller bindings independent of virtual action remapping.");
        int oldManual = skip.ManualSkips, oldCleanup = skip.CleanupCount;
        requestingSkip = true;
        SendCrossKey(true);
        try
        {
            var deadline = Stopwatch.StartNew();
            while (observedCleanup == 0)
            {
                if (scene.Failure is { } failure) throw new InvalidDataException("Actual native cutscene skip stopped the story scene.", failure);
                if (deadline.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("Actual physical Cross input did not reach post-pending cutscene cleanup.");
                await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
        finally { SendCrossKey(false); requestingSkip = false; }
        assert(sawAbortBeforePrivateWebs && skip.ManualSkips == oldManual + 1 && skip.CleanupCount == oldCleanup + 1 &&
            observedCleanup == 1 && player.Scripts?.Active == false && !scene.Registry.Environment.FastForwardDeath,
            "The real input action raises one abort before private webs; the actual VM completes before once-only cleanup clears the shared abort after pending mission commands. " +
            $"Manual={oldManual}/{skip.ManualSkips}, cleanup={oldCleanup}/{skip.CleanupCount}, observed={observedCleanup}, sawAbort={sawAbortBeforePrivateWebs}, active={player.Scripts?.Active}, abort={scene.Registry.Environment.FastForwardDeath}, " +
            $"programs={player.Scripts?.ProgramsStarted}, programOffset={player.Scripts?.CurrentProgram?.Instructions[0].SourceOffset:X}, runtimeCursor={player.Scripts?.Runtime?.InstructionIndex}, runtimeCommands={player.Scripts?.Runtime?.PendingCommands}, " +
            $"instant={cleanupSnapshot}, mission=[{string.Join(',', missionCommands)}], controls=[{string.Join(',', spawnedControls)}].");
        int completed = skip.CleanupCount;
        await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.PhysicsFrame);
        await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.PhysicsFrame);
        assert(skip.CleanupCount == completed && !scene.Registry.Environment.FastForwardDeath,
            "Subsequent genuine source frames cannot repeat the completed cutscene cleanup. " +
            $"Cleanup={completed}/{skip.CleanupCount}, active={player.Scripts?.Active}, abort={scene.Registry.Environment.FastForwardDeath}, programs={player.Scripts?.ProgramsStarted}, " +
            $"programOffset={player.Scripts?.CurrentProgram?.Instructions[0].SourceOffset:X}, instant={cleanupSnapshot}, currentClock={scene.SourceClock}, " +
            $"mission=[{string.Join(',', missionCommands)}], controls=[{string.Join(',', spawnedControls)}].");
        await VerifyFollowOnCutsceneAsync();
    }

    private async Task VerifyFollowOnCutsceneAsync()
    {
        GodotPlayerScripts scripts = player.Scripts ?? throw new InvalidDataException("The actual first-mission player input owner disappeared.");
        TriggerNode producerNode = scene.Registry.Triggers.Nodes[0], commandNode = scene.Registry.Triggers.Nodes[281];
        TriggerActorDefinition producer = TriggerActorDefinition.Read(producerNode);
        IReadOnlyList<TriggerInstruction> program = TriggerCommandReader.Read(commandNode);
        TriggerInstruction input = program.Single(instruction => instruction.Opcode == 199);
        PlayerScriptProgram authored = PlayerScriptProgram.Read(input.Arguments.Span, input.SourceOffset + 2);
        TriggerNode checkpoint = scene.Registry.Triggers.Nodes[96];
        TriggerPosition checkpointPosition = TriggerPlacement.Read(checkpoint).Position;
        FixedVector3 target = new FixedVector3(checkpointPosition.X, checkpointPosition.Y, checkpointPosition.Z) << 12;
        assert(producer.ActorType == 203 && !producer.InitializeOnLoad && producer.Links.SequenceEqual(new ushort[] { 43, 209, 228, 174, 184, 182, 187, 186, 281 }) &&
            program.Select(instruction => instruction.Opcode).SequenceEqual(new ushort[] { 195, 199 }) &&
            authored.Instructions[0] is { Opcode: PlayerScriptOpcode.SetCheckpoint, First: 96, SourceOffset: 0x2F8E } &&
            checkpoint.TypeCode == 3 && MissionRuntime.ReadLinks(checkpoint).Count == 0,
            "The unchanged L1A1 follow-on is control0's authored281 pulse, source195/199 and stream2F8E/checkpoint96, with no substituted actor or script.");

        await Until(() => scripts.ProgramsStarted == 2 && scripts.Active && scripts.Runtime?.CheckpointNode == 96 && skip.State.Allowed == 1,
            "The genuine follow-on281 input and original222 manual-skip gate did not become ready.");
        assert(scripts.CurrentProgram is { } actual && actual.Instructions.SequenceEqual(authored.Instructions) &&
            spawnedNodes.Contains(0) && traces.Count(trace => trace.Kind == MissionTraceKind.Queued && trace.NodeId == 281) == 1 &&
            traces.Where(trace => trace.Kind == MissionTraceKind.CommandCompleted && trace.NodeId == 281)
                .Select(trace => (trace.Opcode, trace.SourceOffset)).SequenceEqual(program.Select(instruction => ((ushort?)instruction.Opcode, instruction.SourceOffset))) &&
            traces.Any(trace => trace.Kind == MissionTraceKind.CommandCompleted && trace.Opcode == 222) && !scene.Registry.Environment.FastForwardDeath,
            "The real source actor0 constructs/completes, queues281 once and executes its exact195/199 bytes before the second original physical skip is legal.");

        int oldManual = skip.ManualSkips, oldCleanup = skip.CleanupCount;
        GodotPlayerDistanceAttackPreparation preparation = Descendants(scene).OfType<GodotPlayerWebRuntime>().Single().DistanceAttackPreparation!;
        int oldPreparation = preparation.PreparationCount;
        expectedCheckpoint = 96;
        observedCleanup = 0; idleAnimationVisits = 0; sawAbortBeforePrivateWebs = false; cleanupSnapshot = null;
        requestingSkip = true;
        SendCrossKey(true);
        try { await Until(() => observedCleanup == 1, "Physical Cross did not complete the genuine checkpoint96 follow-on cleanup."); }
        finally { SendCrossKey(false); requestingSkip = false; }
        assert(sawAbortBeforePrivateWebs && skip.ManualSkips == oldManual + 1 && skip.CleanupCount == oldCleanup + 1 &&
            scripts.ProgramsStarted == 2 && !scripts.Active && !player.Combat.State.SynthesizedInputActive &&
            preparation.PreparationCount == oldPreparation + 1 && player.Controller.Position == target &&
            player.Combat.State.Position == target && !scene.Registry.Environment.FastForwardDeath && scene.Mission.PendingCount == 0,
            "One second legal physical trigger must finish the actual281 VM, teleport to its original96 checkpoint, run genuine Prior cleanup once and drain pending commands before effects.");

        int sourceUpdates = scene.Registry.SourceUpdates;
        await Until(() => scene.Registry.SourceUpdates >= sourceUpdates + 2, "Source simulation did not resume after actual checkpoint96 cleanup.");
        assert(scripts.ProgramsStarted == 2 && !scripts.Active && skip.CleanupCount == oldCleanup + 1 &&
            preparation.PreparationCount == oldPreparation + 1 && !scene.Registry.Environment.FastForwardDeath,
            "Two completed source-list visits after checkpoint96 retain the same completed program and cleanup counts before the live effect probe starts.");

        async Task Until(Func<bool> complete, string failure)
        {
            var deadline = Stopwatch.StartNew();
            while (!complete())
            {
                if (scene.Failure is { } sourceFailure) throw new InvalidDataException("The actual follow-on story script stopped the source scene.", sourceFailure);
                if (deadline.Elapsed > TimeSpan.FromSeconds(15))
                    throw new TimeoutException(failure + $" programs={scripts.ProgramsStarted},active={scripts.Active},checkpoint={scripts.Runtime?.CheckpointNode},allowed={skip.State.Allowed}," +
                        $"manual={skip.ManualSkips},cleanup={skip.CleanupCount},clock={scene.SourceClock},mission=[{string.Join(',', missionCommands)}],controls=[{string.Join(',', spawnedControls)}].");
                await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
    }

    private void BeforePrivateWebs(uint time)
    {
        if (++aiVisits == 1)
        {
            assert(player.CameraStartup is { State.Initialized: 1 } && player.Combat.State.FloorCameraState == 0,
                "Source53C becomes1 and540 becomes0 at the first real player AI boundary, before private webs/SpideyAI0.");
            CameraNormalPlacementState state = camera.SourceNormalPlacement;
            var expected = new CameraNormalPlacementState { Pitch = state.Pitch, Heading = state.Heading, Roll = state.Roll,
                Focus = player.Controller.Position };
            expected.CalculateNormal(scene.Application.CameraProjectionCache);
            assert(state.Focus == player.Controller.Position && camera.SourceTripodPosition == player.Controller.Position &&
                state.Position == expected.DesiredPosition &&
                state.Rotation == expected.DesiredRotation,
                "Actual first-AI SetStartPosition copies the real tripod body position into camera104, consumes the retained projection cache and immediately publishes the verified original normal pose without interpolation, another AI or collision phase.");
            byte lockValue = player.Combat.State.CameraAngleLock;
            camera.ScriptRig.SetAngleLock(0);
            assert(camera.ScriptRig.AngleLocked && player.Combat.State.CameraAngleLock == 1,
                "Actual187 camera angle-lock writes the canonical player8EC used by both PutBehind and player facing.");
            camera.ScriptRig.SetAngleLock(lockValue == 0 ? (ushort)1 : (ushort)0);
            assert(camera.ScriptRig.AngleLocked == (lockValue != 0) && player.Combat.State.CameraAngleLock == lockValue,
                "Restoring the source lock uses the same player field without an independent rig-owned lock.");
        }
        if (!requestingSkip || !scene.Registry.Environment.FastForwardDeath) return;
        sawAbortBeforePrivateWebs = true;
        assert(player.Scripts is { Active: true } && player.Scripts.Runtime?.CheckpointNode == expectedCheckpoint && player.PhysicalPad[3].Triggered == 0 &&
            player.PhysicalPad[2].Triggered == 0 && player.PhysicalPad[16].Triggered == 0,
            "The genuine physical pad gate consumes exactly original Cross/Circle/virtual Jump triggers while the VM is still active before private webs.");
    }

    private void AfterPendingCleanup()
    {
        if (!requestingSkip) return;
        observedCleanup++;
        GodotPlayerScripts scripts = player.Scripts ?? throw new InvalidDataException("The live source player input owner disappeared during cleanup.");
        cleanupSnapshot = new(skip.ManualSkips, skip.CleanupCount, scripts.Active,
            scene.Registry.Environment.FastForwardDeath, scripts.ProgramsStarted,
            scripts.CurrentProgram!.Instructions[0].SourceOffset, scene.SourceClock);
        assert(sawAbortBeforePrivateWebs && player.Scripts?.Active == false && !player.Combat.State.SynthesizedInputActive && scene.Mission.PendingCount == 0 &&
            scripts.Runtime?.CheckpointNode == expectedCheckpoint &&
            !scene.Registry.Environment.FastForwardDeath && skip.State.FieldE00 == 0 &&
            player.PhysicalPad[3].Pressed == 1 && player.PhysicalPad[16].Pressed == 1,
            "After actual pending commands drain, original cleanup clears E00/shared abort and forces pressed Cross/virtual Jump after the VM has completed.");
        assert(idleAnimationVisits == 1 && player.Controller.Action == PlayerAction.Idle &&
            player.Combat.State.ActionState == 1 && player.Model.ClipId == expectedStandClip &&
            player.Controller.IsCrawling == preStandCrawling &&
            camera.SourceControlMode == 3 && !speech.Controller.Busy && speech.Controller.Cooldown == 30,
            "Genuine cleanup stops speech, plays animation0 then executes the original crawling/held-item stand branch and restores the actual demo camera start pose. " +
            $"Busy={speech.Controller.Busy}, cooldown={speech.Controller.Cooldown}, clip={player.Model.ClipId} (sourceExpected{expectedStandClip}), preStandCrawling={preStandCrawling}, preStandHeldFlags={preStandHeldFlags}, action={player.Controller.Action}, combatAction={player.Combat.State.ActionState}, " +
            $"mode={camera.SourceControlMode}, position={camera.SourcePosition}, startPosition={camera.SourceNormalPlacement.Position}, player={player.Controller.Position}, combatPosition={player.Combat.State.Position}.");
    }

    private void BeforeStand()
    {
        if (!requestingSkip) return;
        idleAnimationVisits++;
        assert(player.Model.ClipId == 0 && player.Controller.IsCrawling == player.Combat.State.IsCrawling &&
            observedCleanup == 0 && scene.Registry.Environment.FastForwardDeath,
            "Actual PlaySingleAnim0 completes before Stand, while the original live crawl flag agrees with combat state and shared abort remains set.");
        preStandCrawling = player.Combat.State.IsCrawling;
        preStandHeldFlags = player.Melee?.HeldObjectFlags;
        // Independently read SwitchToStandMode's source default branch after mAnim0:
        // crawling selects19; held field10C bit8 selects200/194; otherwise0.
        expectedStandClip = preStandCrawling ? 19 : preStandHeldFlags is { } flags ? (flags & 8) != 0 ? 200 : 194 : 0;
    }
    private void RecordMission(MissionTrace trace)
    {
        traces.Add(trace);
        if (trace.Kind == MissionTraceKind.CommandCompleted)
            missionCommands.Add($"{trace.NodeId}/{trace.Opcode}@{trace.SourceOffset:X}/clock{scene.SourceClock}");
    }
    private void RecordSpawn(GodotScriptActor actor)
    {
        spawnedNodes.Add(actor.SourceNodeId);
        spawnedControls.Add($"{actor.SourceNodeId}/{actor.SourceActorType}/clock{scene.SourceClock}");
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (requestingSkip) SendCrossKey(false);
        player.Controller.BeginPlayerAi -= BeforePrivateWebs;
        skip.IdleAnimationPlayed -= BeforeStand;
        skip.CleanupCompleted -= AfterPendingCleanup;
        scene.Mission.Trace -= RecordMission;
        scene.Registry.Spawned -= RecordSpawn;
    }
    private static void SendCrossKey(bool pressed)
    {
        using var key = new InputEventKey { PhysicalKeycode = Key.Space, Keycode = Key.Space, Pressed = pressed };
        Input.ParseInputEvent(key);
    }
    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child)) yield return descendant;
        }
    }
    private sealed record CleanupSnapshot(int ManualSkips, int CleanupCount, bool ScriptsActive, bool Abort,
        int ProgramsStarted, int ProgramOffset, uint Clock);
}
