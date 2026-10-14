using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Bounded direct trigger of untouched L1A4 end197, whose181 performs body destruction before162; actual SceneTree navigation reaches independently verified L2A1.</summary>
internal static class StoryFourthMissionTransitionImportChecks
{
    public const string ProofState = "story_fourth_end_transition_proof";
    private sealed record Transfer(ulong Application, ulong Scene, int CompletionCount, ulong[] SourceBodies, ulong World);

    public static async Task VerifyAsync(StoryLevelSession scene)
    {
        GameSessionState application = scene.Application;
        if (application.HasMeta(ProofState))
        {
            Transfer transfer = JsonSerializer.Deserialize<Transfer>(application.GetMeta(ProofState).AsString())
                ?? throw new InvalidDataException("The genuine fourth-mission transition lost its application proof state.");
            Assert(application.GetInstanceId() == transfer.Application && scene.GetInstanceId() != transfer.Scene &&
                application.MissionName.Equals("L2A1_T", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(application.SceneAsset, "data/L2A1_G.psx", StringComparison.OrdinalIgnoreCase) &&
                scene.Checkpoints.Save.RestartPointName.Length == 0 && !GodotObject.IsInstanceIdValid(transfer.Scene) &&
                !GodotObject.IsInstanceIdValid(transfer.World) && transfer.SourceBodies.All(identity => !GodotObject.IsInstanceIdValid(identity)),
                "Actual181 replaces L1A4 with its selected L2A1 scene, retains the same application, clears restart selection and releases captured player/camera/bodies/world during genuine scene replacement. " +
                $"Application={application.GetInstanceId()}/{transfer.Application}, scene={scene.GetInstanceId()}/{transfer.Scene}, " +
                $"mission={application.MissionName}, asset={application.SceneAsset}, restart='{scene.Checkpoints.Save.RestartPointName}', " +
                $"oldSceneValid={GodotObject.IsInstanceIdValid(transfer.Scene)}, oldWorldValid={GodotObject.IsInstanceIdValid(transfer.World)}, retainedBodies={transfer.SourceBodies.Count(GodotObject.IsInstanceIdValid)}.");
            // Attach before the destination's first event frame and run its untouched cinema/census/restart, real source AI and native teardown.
            await StoryLevelFiveSessionImportChecks.VerifyAsync(scene);
            Assert(application.Campaign.CompletionCounts.Sum(value => value) == transfer.CompletionCount + 1,
                "Genuine L1A4 completion increments exactly one original campaign counter across actual L2A1 scene navigation/startup.");
            application.RemoveMeta(ProofState);
            GD.Print("PASS: bounded direct trigger of untouched L1A4 end197, actual181 body destruction followed by162 module cleanup with no added305/110, source completion and SceneTree navigation into independently verified L2A1 startup/live/HUD/teardown. Full manual mission traversal was not exercised.");
            await StoryLevelSessionImportChecks.VerifyNativeDeletionAsync(scene, scene.GetTree());
            return;
        }

        Assert(application.MissionName.Equals("L1A4_T", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(application.SceneAsset, "data/L1A4_G.psx", StringComparison.OrdinalIgnoreCase),
            "Fourth-mission transition proof starts from the genuine authored L1A4 scene.");
        var deadline = Stopwatch.StartNew();
        while (scene.Startup.Phase != MissionStartupPhase.Playing)
        {
            Healthy();
            Assert(!scene.Cinema.IsPlaying, "The actual source L1A4 startup has no invented cinema barrier.");
            await Frame();
        }
        Assert(scene.Player is not null && application.EndLevelNode == 197 && scene.Checkpoints.Current is { NodeId: 1, Name: "Re_Start_Death" },
            "Real L1A4 autoexec/census/death restart selects its original end197 before the bounded trigger.");
        TriggerNode end = scene.Registry.Triggers.Nodes[application.EndLevelNode];
        IReadOnlyList<TriggerInstruction> commands = TriggerCommandReader.Read(end);
        Assert(end.Id == 197 && commands.Select(command => command.Opcode).SequenceEqual(new ushort[] { 181, 162 }) &&
            Encoding.Latin1.GetString(commands[0].Arguments.Span).TrimEnd('\0') == "L2A1_T",
            "The proof executes exact original end197=[181 L2A1_T,162], retaining its genuine command order and omitting unauthored305/110.");
        GodotMissionLibraries libraries = scene.GetNode<GodotMissionLibraries>("MissionLibraries");
        GodotEnemyRuntime enemies = Descendants(scene).OfType<GodotEnemyRuntime>().Single();
        GodotPlayerCombatRuntime combat = Descendants(scene).OfType<GodotPlayerCombatRuntime>().Single();
        GodotCombatSceneOwnership ownership = combat.SceneOwnership;
        GodotActorCodeModules modules = enemies.Modules;
        ulong[] bodyIds = Enum.GetValues<ActorUpdateGroup>().SelectMany(group => scene.Registry.SourceList(group).SourceOrder)
            .Concat(scene.Registry.SuspendedSourceList.SourceOrder).OfType<GodotObject>()
            .Concat(new GodotObject[] { scene.Player!, Descendants(scene).OfType<PlayerFollowCamera>().Single() })
            .Select(body => body.GetInstanceId()).Distinct().ToArray();
        Assert(bodyIds.Length > 2 && libraries.World is not null && libraries.ObjectLibrary is not null &&
            modules.Modules.SourceOrder.Select(record => record.Name).SequenceEqual(new[] { "thug", "hostage" }),
            "The source end trigger begins with actual mixed L1A4 native player/camera/bodies, loaded G/O banks and genuine thug/hostage module heads.");
        ulong worldId = libraries.World!.GetInstanceId();
        var completed = new List<ushort>();
        scene.Mission.Trace += Observe;
        application.SetMeta(ProofState, JsonSerializer.Serialize(new Transfer(application.GetInstanceId(), scene.GetInstanceId(),
            application.Campaign.CompletionCounts.Sum(value => value), bodyIds, worldId)));
        // Deliberately bounded: queue the untouched authored end point directly; do not claim the intervening gameplay was traversed.
        scene.Mission.Queue(end.Id);
        try
        {
            while (GodotObject.IsInstanceValid(scene))
            {
                Healthy();
                GodotSaveConfirmation? confirmation = Descendants(scene).OfType<GodotSaveConfirmation>().SingleOrDefault();
                if (confirmation is { InputReady: true, Dialog.Visible: true })
                    confirmation.Dialog.GetCancelButton().EmitSignal(BaseButton.SignalName.Pressed);
                await Frame();
            }
            Assert(completed.SequenceEqual(new ushort[] { 181, 162 }),
                "Both original end197 commands complete exactly once and in original order before native source-scene deletion. " +
                $"Completed=[{string.Join(',', completed)}].");
        }
        finally { scene.Mission.Trace -= Observe; }

        void Observe(MissionTrace trace)
        {
            if (trace.Kind == MissionTraceKind.CommandStarted)
                GD.Print($"Fourth story transition source command node{trace.NodeId}/{trace.Opcode}, player present={scene.Player is not null}, bounded end={end.Id}.");
            if (trace.NodeId != end.Id || trace.Kind != MissionTraceKind.CommandCompleted || trace.Opcode is not ushort opcode) return;
            completed.Add(opcode);
            if (opcode == 181)
                Assert(application.MissionName.Equals("L2A1_T", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(application.SceneAsset, "data/L2A1_G.psx", StringComparison.OrdinalIgnoreCase) &&
                    application.ExitReason == LevelExitReason.LevelCompleted && scene.SceneObjects.IsDestroyed &&
                    scene.Player is null && scene.Registry.Player is null && scene.Registry.LiveCount == 0 &&
                    scene.Registry.BaddyConstructionState.Count == 0 && scene.Registry.SuspendedSourceList.Count == 0 &&
                    bodyIds.All(identity => !GodotObject.IsInstanceIdValid(identity)) && ownership.Particles.Count == 0 &&
                    ownership.Directory.SourceBodies.Count == 0 && ownership.Directory.SourceControls.Count == 0 &&
                    libraries.World is not null && libraries.World.GetInstanceId() == worldId && libraries.ObjectLibrary is not null &&
                    modules.Modules.SourceOrder.Count == 2,
                    "Original181 selects its actual mission/geometry and destroys all source bodies/player/camera/bits itself. Original G/O banks and both module records remain before subsequent162; no305/110 is substituted.");
            else if (opcode == 162)
                Assert(modules.Modules.SourceOrder.Count == 0 && libraries.World is not null && libraries.World.GetInstanceId() == worldId &&
                    libraries.ObjectLibrary is not null && ownership.Particles.Count == 0,
                    "Original162 runs genuine module clear after181 body destruction, retaining static world/bank ownership until actual SceneTree replacement.");
        }
        void Healthy()
        {
            if (scene.Failure is { } failure) throw new InvalidDataException("Actual fourth-mission transition failed.", failure);
            if (deadline.Elapsed > TimeSpan.FromSeconds(90)) throw new TimeoutException("Authored L1A4-to-L2A1 navigation did not finish.");
        }
        async Task Frame() => await application.ToSignal(application.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Assert(bool condition, string evidence) { if (!condition) throw new InvalidDataException(evidence); }
    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child)) yield return descendant;
        }
    }
}
