using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Gameplay;

namespace OpenSpidey.Game.World;

/// <summary>Runs untouched L1A1 end224 through movie12, completion and actual navigation into L1A2.</summary>
internal static class StoryMissionTransitionImportChecks
{
    private const string ProofState = "story_end_transition_proof";
    private sealed record Transfer(ulong Application, ulong Scene, int CompletionCount);

    public static async Task VerifyAsync(StoryLevelSession scene)
    {
        GameSessionState application = scene.Application;
        if (application.HasMeta(ProofState))
        {
            Transfer transfer = JsonSerializer.Deserialize<Transfer>(application.GetMeta(ProofState).AsString())
                ?? throw new InvalidDataException("The authored transition lost its application proof state.");
            Assert(application.GetInstanceId() == transfer.Application && scene.GetInstanceId() != transfer.Scene &&
                application.MissionName.Equals("L1A2_T", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(application.SceneAsset, "data/L1A2_G.psx", StringComparison.OrdinalIgnoreCase) &&
                scene.Checkpoints.Save.RestartPointName.Length == 0 &&
                !GodotObject.IsInstanceIdValid(transfer.Scene),
                "Source181 must replace the real scene and retain its application, selected mission and cleared restart. " +
                $"Application={application.GetInstanceId()}/{transfer.Application}, scene={scene.GetInstanceId()}/{transfer.Scene}, " +
                $"mission={application.MissionName}, asset={application.SceneAsset}, restart='{scene.Checkpoints.Save.RestartPointName}', oldSceneValid={GodotObject.IsInstanceIdValid(transfer.Scene)}.");
            await StoryLevelTwoSessionImportChecks.VerifyAsync(scene);
            Assert(application.Campaign.CompletionCounts.Sum(value => value) == transfer.CompletionCount + 1,
                "The genuine completion owner must increment exactly one original campaign counter across navigation.");
            application.RemoveMeta(ProofState);
            GD.Print("PASS: original L1A1 end224,305/110/162, real movie12,181/completion and actual SceneTree navigation into authored L1A2 startup.");
            await StoryLevelSessionImportChecks.VerifyNativeDeletionAsync(scene, scene.GetTree());
            return;
        }

        Assert(application.MissionName == "L1A1_T", "The authored transition proof must start at the genuine first mission.");
        var deadline = Stopwatch.StartNew();
        bool introObserved = false;
        while (scene.Startup.Phase != MissionStartupPhase.Playing)
        {
            Healthy();
            if (scene.Cinema.IsPlaying)
            {
                introObserved = true;
                if (!scene.Cinema.ShowingCover && scene.Cinema.CanSkipMovie) scene.Cinema.RequestSkip();
            }
            await Frame();
        }
        Assert(introObserved && scene.Player is not null && application.EndLevelNode == 224,
            "Real autoexec, intro and death restart must finish before the bounded end-command trigger.");
        TriggerNode end = scene.Registry.Triggers.Nodes[application.EndLevelNode];
        IReadOnlyList<TriggerInstruction> commands = TriggerCommandReader.Read(end);
        Assert(commands.Select(command => command.Opcode).SequenceEqual(new ushort[] { 305, 110, 162, 190, 181 }) &&
            System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(commands[3].Arguments.Span) == 12 &&
            Encoding.Latin1.GetString(commands[4].Arguments.Span).TrimEnd('\0') == "L1A2_T",
            "The transition runs the unchanged original end224 command list and exact movie/mission operands.");
        GodotMissionLibraries libraries = scene.GetNode<GodotMissionLibraries>("MissionLibraries");
        var trace = new List<MissionTrace>();
        scene.Mission.Trace += Observe;
        application.SetMeta(ProofState, JsonSerializer.Serialize(new Transfer(application.GetInstanceId(),
            scene.GetInstanceId(), application.Campaign.CompletionCounts.Sum(value => value))));
        scene.Mission.Queue(end.Id);
        bool outroObserved = false;
        try
        {
            while (GodotObject.IsInstanceValid(scene))
            {
                Healthy();
                if (scene.Cinema.IsPlaying)
                {
                    outroObserved = true;
                    Assert(scene.SceneObjects.IsDestroyed && scene.Player is null && scene.Registry.LiveCount == 0 &&
                        libraries.World is null && libraries.ObjectLibrary is null && scene.Mission.IsBlocked,
                        "Actual305/110/162 must destroy source bodies and world banks before the real outro holds181.");
                    if (!scene.Cinema.ShowingCover && scene.Cinema.CanSkipMovie) scene.Cinema.RequestSkip();
                }
                GodotSaveConfirmation? confirmation = Descendants(scene).OfType<GodotSaveConfirmation>().SingleOrDefault();
                if (confirmation is { InputReady: true, Dialog.Visible: true })
                    confirmation.Dialog.GetCancelButton().EmitSignal(BaseButton.SignalName.Pressed);
                await Frame();
            }
            Assert(outroObserved && trace.Where(entry => entry.NodeId == end.Id && entry.Kind == MissionTraceKind.CommandCompleted)
                .Select(entry => entry.Opcode).SequenceEqual(commands.Select(command => (ushort?)command.Opcode)),
                "The authored outro and each exact end command must complete once before the scene is replaced.");
        }
        finally
        {
            // The managed mission trace remains readable after its actual native scene owner is deleted.
            scene.Mission.Trace -= Observe;
        }

        void Observe(MissionTrace entry)
        {
            trace.Add(entry);
            if (entry.Kind == MissionTraceKind.CommandStarted)
                GD.Print($"Story transition source command node{entry.NodeId}/{entry.Opcode}, camera/player present={scene.Player is not null}, end={end.Id}.");
        }
        void Healthy()
        {
            if (scene.Failure is { } failure) throw new InvalidDataException("Authored mission transition failed.", failure);
            if (deadline.Elapsed > TimeSpan.FromSeconds(90)) throw new TimeoutException("Authored mission navigation did not finish.");
        }
        async Task Frame() => await application.ToSignal(application.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Assert(bool condition, string evidence)
    {
        if (!condition) throw new InvalidDataException(evidence);
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
