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

/// <summary>Bounded end81 trigger over the untouched L1A2 program and actual SceneTree navigation into the separately verified L1A3 story.</summary>
internal static class StorySecondMissionTransitionImportChecks
{
    public const string ProofState = "story_second_end_transition_proof";
    private sealed record Transfer(ulong Application, ulong Scene, int CompletionCount, ulong[] SourceBodies);

    public static async Task VerifyAsync(StoryLevelSession scene)
    {
        GameSessionState application = scene.Application;
        if (application.HasMeta(ProofState))
        {
            Transfer transfer = JsonSerializer.Deserialize<Transfer>(application.GetMeta(ProofState).AsString())
                ?? throw new InvalidDataException("The genuine second-mission transition lost its application proof state.");
            Assert(application.GetInstanceId() == transfer.Application && scene.GetInstanceId() != transfer.Scene &&
                application.MissionName.Equals("L1A3_T", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(application.SceneAsset, "data/L1A3_G.psx", StringComparison.OrdinalIgnoreCase) &&
                scene.Checkpoints.Save.RestartPointName.Length == 0 && !GodotObject.IsInstanceIdValid(transfer.Scene) &&
                transfer.SourceBodies.All(identity => !GodotObject.IsInstanceIdValid(identity)),
                "Actual181 must replace L1A2 with the selected L1A3 scene, retain the same application, clear restart selection and release every captured source body. " +
                $"Application={application.GetInstanceId()}/{transfer.Application}, scene={scene.GetInstanceId()}/{transfer.Scene}, " +
                $"mission={application.MissionName}, asset={application.SceneAsset}, restart='{scene.Checkpoints.Save.RestartPointName}', " +
                $"oldSceneValid={GodotObject.IsInstanceIdValid(transfer.Scene)}, retainedBodies={transfer.SourceBodies.Count(GodotObject.IsInstanceIdValid)}.");
            // This independent proof attaches before the destination's first autoexec/event frame and performs its real live teardown.
            await StoryMissionThreeImportChecks.VerifyAsync(scene);
            Assert(application.Campaign.CompletionCounts.Sum(value => value) == transfer.CompletionCount + 1,
                "Real L1A2 completion must increment exactly one original campaign counter across the actual L1A3 navigation/startup.");
            application.RemoveMeta(ProofState);
            GD.Print("PASS: bounded direct trigger of untouched L1A2 end81, actual305/110/162/181 teardown/completion, SceneTree navigation and independently verified authored L1A3 startup/live/teardown. Full manual mission traversal was not exercised.");
            await StoryLevelSessionImportChecks.VerifyNativeDeletionAsync(scene, scene.GetTree());
            return;
        }

        Assert(application.MissionName.Equals("L1A2_T", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(application.SceneAsset, "data/L1A2_G.psx", StringComparison.OrdinalIgnoreCase),
            "Second-mission transition proof must start from the genuine authored L1A2 scene.");
        var deadline = Stopwatch.StartNew();
        while (scene.Startup.Phase != MissionStartupPhase.Playing)
        {
            Healthy();
            Assert(!scene.Cinema.IsPlaying, "Actual L1A2 startup has no invented movie barrier.");
            await Frame();
        }
        Assert(scene.Player is not null && application.EndLevelNode == 81 && scene.Checkpoints.Current is { NodeId: 0, Name: "Re_Start_Death" },
            "The actual source L1A2 autoexec/census/death restart must finish and select its original302 end-node81 before the bounded trigger.");
        TriggerNode end = scene.Registry.Triggers.Nodes[application.EndLevelNode];
        IReadOnlyList<TriggerInstruction> commands = TriggerCommandReader.Read(end);
        Assert(end.Id == 81 && commands.Select(command => command.Opcode).SequenceEqual(new ushort[] { 305, 110, 162, 181 }) &&
            Encoding.Latin1.GetString(commands[3].Arguments.Span).TrimEnd('\0') == "L1A3_T",
            "The transition must execute unchanged original end81 and its exact L1A3_T operand, without substituting another command list.");
        GodotMissionLibraries libraries = scene.GetNode<GodotMissionLibraries>("MissionLibraries");
        GodotEnemyRuntime enemy = Descendants(scene).OfType<GodotEnemyRuntime>().Single();
        GodotPlayerCombatRuntime combat = Descendants(scene).OfType<GodotPlayerCombatRuntime>().Single();
        GodotCombatSceneOwnership ownership = combat.SceneOwnership;
        ulong[] bodyIds = Enum.GetValues<ActorUpdateGroup>().SelectMany(group => scene.Registry.SourceList(group).SourceOrder)
            .Concat(scene.Registry.SuspendedSourceList.SourceOrder).OfType<GodotObject>()
            .Concat(new GodotObject[] { scene.Player!, Descendants(scene).OfType<PlayerFollowCamera>().Single() })
            .Select(body => body.GetInstanceId()).Distinct().ToArray();
        Assert(bodyIds.Length > 2 && libraries.World is not null && libraries.ObjectLibrary is not null && enemy.Modules.Modules.SourceOrder.Count > 0,
            "The real end trigger must begin with authored native bodies/player/camera, loaded world/object banks and genuine module registrations.");
        var completed = new List<ushort>();
        scene.Mission.Trace += Observe;
        application.SetMeta(ProofState, JsonSerializer.Serialize(new Transfer(application.GetInstanceId(),
            scene.GetInstanceId(), application.Campaign.CompletionCounts.Sum(value => value), bodyIds)));
        // Deliberately bounded: trigger the genuine authored end point directly, rather than claim a full mission playthrough.
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
            Assert(completed.SequenceEqual(new ushort[] { 305, 110, 162, 181 }),
                "Each untouched end81 command must complete once and in original order before actual source-scene deletion. " +
                $"Completed=[{string.Join(',', completed)}].");
        }
        finally { scene.Mission.Trace -= Observe; }

        void Observe(MissionTrace entry)
        {
            if (entry.Kind == MissionTraceKind.CommandStarted)
                GD.Print($"Second story transition source command node{entry.NodeId}/{entry.Opcode}, player present={scene.Player is not null}, bounded end={end.Id}.");
            if (entry.NodeId != end.Id || entry.Kind != MissionTraceKind.CommandCompleted || entry.Opcode is not ushort opcode) return;
            completed.Add(opcode);
            if (opcode == 305)
                Assert(scene.SceneObjects.IsDestroyed && scene.Player is null && scene.Registry.Player is null &&
                    scene.Registry.LiveCount == 0 && scene.Registry.BaddyConstructionState.Count == 0 &&
                    scene.Registry.SuspendedSourceList.Count == 0 && bodyIds.All(identity => !GodotObject.IsInstanceIdValid(identity)) &&
                    ownership.Particles.Count == 0 && ownership.Directory.SourceBodies.Count == 0,
                    "Actual305 must retire all original player/camera/bodies, constructor leases and shared particle/body registrations before bank unload.");
            else if (opcode == 110)
                Assert(libraries.World is null && libraries.ObjectLibrary is null,
                    "Actual110 must unload the genuine unprotected world/object banks after body destruction and before module cleanup.");
            else if (opcode == 162)
                Assert(enemy.Modules.Modules.SourceOrder.Count == 0,
                    "Actual162 must invoke the real module cleanup owner and leave no loaded source module head before181 selects navigation.");
            else if (opcode == 181)
                Assert(application.MissionName.Equals("L1A3_T", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(application.SceneAsset, "data/L1A3_G.psx", StringComparison.OrdinalIgnoreCase),
                    "Actual181 must select its authored mission/environment through the genuine transition owner before SceneTree replacement.");
        }
        void Healthy()
        {
            if (scene.Failure is { } failure) throw new InvalidDataException("Actual second-mission transition failed.", failure);
            if (deadline.Elapsed > TimeSpan.FromSeconds(90)) throw new TimeoutException("Authored L1A2-to-L1A3 scene navigation did not finish.");
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
