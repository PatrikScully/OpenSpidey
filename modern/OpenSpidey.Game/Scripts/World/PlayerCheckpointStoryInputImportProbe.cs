using System.Diagnostics;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Untouched L1A2 checkpoint124 skip through physical input, player AI, shared fast-forward and post-pending cleanup.</summary>
internal sealed class PlayerCheckpointStoryInputImportProbe : IDisposable
{
    private readonly StoryLevelSession scene;
    private readonly GodotPlayer player;
    private readonly GodotPlayerScripts scripts;
    private readonly GodotPlayerCutsceneSkip skip;
    private readonly GodotPlayerDistanceAttackPreparation preparation;
    private readonly PlayerFollowCamera camera;
    private readonly GodotSpeechPlayer speech;
    private readonly GodotBlackCatActor cat;
    private readonly Action<bool, string> assert;
    private readonly FixedVector3 checkpointPosition;
    private readonly FixedVector3 targetPosition;
    private readonly List<MissionTrace> traces = [];
    private readonly List<ushort> spawned = [];
    private int oldManual, oldCleanup, oldPreparation, programsStarted;
    private int abortVisits, idleVisits, cleanupVisits, expectedStandClip;
    private bool requesting, disposed;
    private CleanupSnapshot? cleanup;

    public PlayerCheckpointStoryInputImportProbe(StoryLevelSession scene, Action<bool, string> assert)
    {
        this.scene = scene; this.assert = assert;
        player = scene.Player ?? throw new InvalidDataException("Checkpoint skip requires the actual story player.");
        scripts = player.Scripts ?? throw new InvalidDataException("Checkpoint skip requires the real authored input VM.");
        skip = scripts.CutsceneSkip ?? throw new InvalidDataException("Checkpoint skip requires the genuine player AI/end-source owner.");
        Node[] nodes = Descendants(scene).ToArray();
        camera = nodes.OfType<PlayerFollowCamera>().Single();
        speech = nodes.OfType<GodotSpeechPlayer>().Single();
        GodotPlayerWebRuntime webs = nodes.OfType<GodotPlayerWebRuntime>().Single();
        preparation = webs.DistanceAttackPreparation ?? throw new InvalidDataException("Checkpoint124 requires the real PriorToVenomDistanceAttack owner.");
        cat = scene.Registry.SourceActors(OpenSpidey.Core.Actors.ActorUpdateGroup.Enemy).OfType<GodotBlackCatActor>().Single();
        TriggerNode checkpoint = scene.Registry.Triggers.Nodes[124], target = scene.Registry.Triggers.Nodes[142];
        TriggerPosition sourceCheckpoint = TriggerPlacement.Read(checkpoint).Position, sourceTarget = TriggerPlacement.Read(target).Position;
        checkpointPosition = new FixedVector3(sourceCheckpoint.X, sourceCheckpoint.Y, sourceCheckpoint.Z) << 12;
        targetPosition = new FixedVector3(sourceTarget.X, sourceTarget.Y, sourceTarget.Z) << 12;
        assert(checkpoint.TypeCode == 3 && target.TypeCode == 3 &&
            sourceCheckpoint == new TriggerPosition(-17291, -3578, -8183) && sourceTarget == new TriggerPosition(-17280, -3578, -7855) &&
            MissionRuntime.ReadLinks(checkpoint).SequenceEqual(new ushort[] { 142 }) &&
            ReferenceEquals(preparation.State.Combat, player.Combat.State) && ReferenceEquals(preparation.State.Water, scene.Application.WaterEffect),
            "The real checkpoint124/first-link142 positions and Prior resource/combat/water owners retain original L1A2 asset and process identities.");
        player.Controller.BeginPlayerAi += BeforePrivateWebs;
        skip.IdleAnimationPlayed += BeforeStand;
        skip.CleanupCompleted += AfterPendingCleanup;
        scene.Mission.Trace += RecordTrace;
        scene.Registry.Spawned += RecordSpawn;
    }

    public async Task VerifyAsync()
    {
        assert(scripts is { Active: true, Runtime.CheckpointNode: 124 } && skip.State.Allowed == 1 &&
            player.Combat.State.SynthesizedInputActive && scene.Registry.Difficulty != 0 &&
            scene.Application.ForceEarlyLevelExit == 0 && !scene.Registry.Environment.FastForwardDeath &&
            player.CameraStartup is { State.Initialized: 1 } && !cat.State.IsDead,
            "The untouched114 VM is active at checkpoint124, live141/222 enabled physical skip, and genuine Cat/first-player AI remain live before input.");
        assert(InputMap.HasAction("source_pad_cross") && InputMap.ActionGetEvents("source_pad_cross").Count > 0,
            "Checkpoint skip uses the existing physical keyboard/controller Cross bindings.");
        oldManual = skip.ManualSkips; oldCleanup = skip.CleanupCount;
        oldPreparation = preparation.PreparationCount; programsStarted = scripts.ProgramsStarted;
        requesting = true;
        SendCrossKey(true);
        try
        {
            var deadline = Stopwatch.StartNew();
            while (cleanup is null)
            {
                Healthy();
                if (deadline.Elapsed > TimeSpan.FromSeconds(5))
                    throw new TimeoutException("Physical Cross did not reach the genuine checkpoint124 end-source cleanup. " + Diagnostics());
                await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
        finally { SendCrossKey(false); requesting = false; }
        Healthy();
        CleanupSnapshot completed = cleanup ?? throw new InvalidDataException("No actual checkpoint cleanup boundary was observed.");
        assert(completed is { AbortVisits: 1, IdleVisits: 1, CleanupVisits: 1, ScriptActive: false, Abort: false, Pending: 0 } &&
            completed.Manual == oldManual + 1 && completed.Cleanup == oldCleanup + 1 && completed.Preparation == oldPreparation + 1 &&
            completed.ProgramsStarted == programsStarted && completed.Checkpoint == 124,
            "One physical trigger aborts the original VM, performs one real checkpoint resource preparation and completes cleanup only after the actual pending queue drains. " + Diagnostics());
        assert(scene.Application.MissionName.Equals("L1A2_T", StringComparison.OrdinalIgnoreCase) &&
            scene.Application.EndLevelNode == 81 && !scene.SceneObjects.IsDestroyed && scene.Player == player,
            "Checkpoint124 skipping retains the genuine mission/end-node/application and allocated player instead of requesting a retry or scene transition.");
        int updates = scene.Registry.SourceUpdates;
        var afterDeadline = Stopwatch.StartNew();
        while (scene.Registry.SourceUpdates < updates + 2)
        {
            Healthy();
            if (afterDeadline.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("Actual source simulation did not resume after checkpoint124 cleanup.");
            await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        assert(skip.ManualSkips == completed.Manual && skip.CleanupCount == completed.Cleanup &&
            preparation.PreparationCount == completed.Preparation && cleanupVisits == 1 && !scene.Registry.Environment.FastForwardDeath,
            "Later genuine source frames resume simulation without repeating completed checkpoint124 preparation or end-source cleanup. " + Diagnostics());
        player.Model.VerifyCurrentPose();
    }

    private void BeforePrivateWebs(uint time)
    {
        if (!requesting || !scene.Registry.Environment.FastForwardDeath) return;
        abortVisits++;
        assert(abortVisits == 1 && scripts.Active && scripts.Runtime?.CheckpointNode == 124 &&
            player.Combat.State.SynthesizedInputActive && preparation.PreparationCount == oldPreparation && cleanupVisits == 0 &&
            player.PhysicalPad[3].Triggered == 0 && player.PhysicalPad[2].Triggered == 0 && player.PhysicalPad[16].Triggered == 0,
            "The physical Cross gate consumes source3/2/16 triggers and sets the shared abort before private webs and the still-active124 VM, before Prior or cleanup. " + Diagnostics());
    }

    private void BeforeStand()
    {
        if (!requesting) return;
        idleVisits++;
        assert(abortVisits == 1 && idleVisits == 1 && cleanupVisits == 0 && scripts.Runtime is
            { Active: false, Decoding: false, PendingCommands: 0, CheckpointNode: 124 } && !player.Combat.State.SynthesizedInputActive &&
            player.Controller.Position == checkpointPosition && player.Combat.State.Position == checkpointPosition &&
            scene.Registry.Environment.FastForwardDeath && scene.Mission.PendingCount == 0 && preparation.PreparationCount == oldPreparation + 1,
            "The real VM stops/clears its concurrent commands and teleports to original124 before post-pending Prior and PlaySingleAnim0; the shared abort stays set until cleanup ends. " + Diagnostics());
        FixedVector3 direction = FixedVectorMath.Normalize(((checkpointPosition - targetPosition) >> 12) with { Y = 0 });
        FixedVector3 right = FixedVectorMath.Normalize(Cross(new(0, 4096, 0), direction));
        FixedVector3 forward = Cross(right, new(0, 4096, 0));
        assert(player.Controller.Orientation.Forward == forward && player.Combat.State.ForwardDirection == forward &&
            player.Controller.Orientation.Up == new FixedVector3(0, -4096, 0) &&
            !player.Controller.IsCrawling && !player.Combat.State.IsCrawling &&
            player.Combat.State.CrawlSurfaceNormal == new FixedVector3(0, -4096, 0) && player.Controller.Velocity == default &&
            player.Combat.State.Velocity == default && player.Combat.State.HeadingTurnTicksRemaining == 0,
            "Actual checkpoint cleanup derives facing from124 minus its first link142, orients to the original floor and applies genuine Prior locomotion resets before Stand. " + Diagnostics());
        assert(player.Model.ClipId == 0 && preparation.State.Water.Value == 0 && player.Combat.State.ArenaConstraintActive == 0 &&
            player.Combat.State.Field5AC == 0 && player.Combat.State.Field5B0 == 0 && camera.FocusHookIndex == -1 &&
            player.Combat.State.FloorCameraState == 0 && cat.State.IsDead && cat.State.DeathPulseSent,
            "Prior reaches its real shared water/resource/floor-camera owners before Idle0; genuine Black Cat AI observes the same abort and executes source Die. " + Diagnostics());
        int? heldFlags = player.Melee?.HeldObjectFlags;
        expectedStandClip = heldFlags is { } flags ? (flags & 8) != 0 ? 200 : 194 : 0;
        assert(heldFlags is null, "The untouched L1A2 checkpoint cutscene has no held401 after real Prior cleanup.");
        // Original enabled-death-skip control128 constructs130, then135, then120;
        // source120 has duplicate122/121 links and each actual pulse remains queued.
        // Spawned is an after-construction event, so nested constructors finish120,
        // then135, then130; the actual pulse queue remains in source link order.
        assert(spawned.SequenceEqual(new ushort[] { 120, 135, 130 }) &&
            traces.Where(trace => trace.Kind == MissionTraceKind.Queued).Select(trace => trace.NodeId)
                .SequenceEqual(new ushort[] { 127, 129, 122, 121, 122, 121 }) &&
            Queued(127) == 1 && Queued(129) == 1 && Queued(122) == 2 && Queued(121) == 2,
            "Shared fast-forward genuinely clears authored128/130/135/120 delays and retains the duplicate122/121 death-pulse order before cleanup. " + Diagnostics());
        foreach ((ushort node, int repetitions) in new[] { ((ushort)127, 1), ((ushort)129, 1), ((ushort)122, 2), ((ushort)121, 2) })
        {
            (ushort? Opcode, int SourceOffset)[] expected = Enumerable.Range(0, repetitions)
                .SelectMany(_ => TriggerCommandReader.Read(scene.Registry.Triggers.Nodes[node]))
                .Select(instruction => ((ushort?)instruction.Opcode, instruction.SourceOffset)).ToArray();
            assert(traces.Where(trace => trace.Kind == MissionTraceKind.CommandCompleted && trace.NodeId == node)
                .Select(trace => (trace.Opcode, trace.SourceOffset)).SequenceEqual(expected),
                $"Original fast-forward point {node} executes every authored command in file order exactly {repetitions} time(s), with no skipped production dependency. " + Diagnostics());
        }
    }

    private void AfterPendingCleanup()
    {
        if (!requesting) return;
        cleanupVisits++;
        cleanup = new(abortVisits, idleVisits, cleanupVisits, skip.ManualSkips, skip.CleanupCount,
            preparation.PreparationCount, scripts.ProgramsStarted, scripts.Active,
            scene.Registry.Environment.FastForwardDeath, scene.Mission.PendingCount, scripts.Runtime?.CheckpointNode ?? -1, scene.SourceClock);
        assert(cleanupVisits == 1 && !scripts.Active && !player.Combat.State.SynthesizedInputActive && !scene.Registry.Environment.FastForwardDeath &&
            skip.State.FieldE00 == 0 && player.PhysicalPad[3].Pressed == 1 && player.PhysicalPad[16].Pressed == 1 &&
            scene.Mission.PendingCount == 0 && player.Controller.Action == PlayerAction.Idle && player.Combat.State.ActionState == 1 &&
            player.Model.ClipId == expectedStandClip && camera.SourceControlMode == 3 && !speech.Controller.Busy && speech.Controller.Cooldown == 30,
            "Actual end-source cleanup follows Idle0 with the original Stand branch, stops speech, restores mode3/start placement, clears E00/shared abort and forces pressed Cross/Jump. " + Diagnostics());
        assert(camera.SourceNormalPlacement.Focus == checkpointPosition && camera.SourceTripodPosition == checkpointPosition &&
            camera.SourcePosition == camera.SourceNormalPlacement.Position,
            "Real SetStartPosition publishes the new124 body into camera104 and the verified source normal pose before a later camera/render frame. " + Diagnostics());
    }

    private int Queued(ushort node) => traces.Count(trace => trace.Kind == MissionTraceKind.Queued && trace.NodeId == node);
    private void RecordTrace(MissionTrace trace) { if (requesting) traces.Add(trace); }
    private void RecordSpawn(GodotScriptActor actor) { if (requesting) spawned.Add(actor.SourceNodeId); }
    private void Healthy()
    {
        if (scene.Failure is { } failure) throw new InvalidDataException("Genuine L1A2 checkpoint124 skip failed.", failure);
    }
    private string Diagnostics() =>
        $"clock={scene.SourceClock}, manual={oldManual}/{skip.ManualSkips}, cleanup={oldCleanup}/{skip.CleanupCount}, prior={oldPreparation}/{preparation.PreparationCount}, " +
        $"visits={abortVisits}/{idleVisits}/{cleanupVisits}, VM={scripts.Active}/{scripts.Runtime?.Decoding}/{scripts.Runtime?.PendingCommands}/{scripts.Runtime?.CheckpointNode}, programs={programsStarted}/{scripts.ProgramsStarted}, " +
        $"abort={scene.Registry.Environment.FastForwardDeath}, pending={scene.Mission.PendingCount}, player={player.Controller.Position}, checkpoint={checkpointPosition}, forward={player.Controller.Orientation.Forward}, " +
        $"clip={player.Model.ClipId}, action={player.Controller.Action}/{player.Combat.State.ActionState}, crawl={player.Controller.IsCrawling}/{player.Combat.State.IsCrawling}, " +
        $"camera={camera.SourceControlMode}/{camera.SourcePosition}, speech={speech.Controller.Busy}/{speech.Controller.Cooldown}, " +
        $"instant={cleanup}, spawns=[{string.Join(',', spawned)}], traces=[{string.Join(',', traces.Select(trace => $"{trace.NodeId}/{trace.Kind}/{trace.Opcode}@{trace.SourceOffset:X}"))}].";

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (requesting) SendCrossKey(false);
        player.Controller.BeginPlayerAi -= BeforePrivateWebs;
        skip.IdleAnimationPlayed -= BeforeStand;
        skip.CleanupCompleted -= AfterPendingCleanup;
        scene.Mission.Trace -= RecordTrace;
        scene.Registry.Spawned -= RecordSpawn;
    }
    private static void SendCrossKey(bool pressed)
    {
        using var key = new InputEventKey { PhysicalKeycode = Key.Space, Keycode = Key.Space, Pressed = pressed };
        Input.ParseInputEvent(key);
    }
    private static FixedVector3 Cross(FixedVector3 a, FixedVector3 b) => new(
        unchecked(a.Y * b.Z - a.Z * b.Y) >> 12, unchecked(a.Z * b.X - a.X * b.Z) >> 12,
        unchecked(a.X * b.Y - a.Y * b.X) >> 12);
    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child)) yield return descendant;
        }
    }
    private sealed record CleanupSnapshot(int AbortVisits, int IdleVisits, int CleanupVisits, int Manual, int Cleanup,
        int Preparation, int ProgramsStarted, bool ScriptActive, bool Abort, int Pending, int Checkpoint, uint Clock);
}
