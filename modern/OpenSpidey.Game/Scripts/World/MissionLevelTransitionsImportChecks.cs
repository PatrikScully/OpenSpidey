using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Uses the real mission host, actual gameplay teardown, retry button and full SceneTree reload.</summary>
internal static class MissionLevelTransitionsImportChecks
{
    private const string ProofState = "mission_transition_proof";
    private sealed record ReloadState(ulong ApplicationId, ulong SceneId, string MissionName, string SceneAsset,
        string RestartName, PlayerCarriedSaveHistory History);

    public static async Task VerifyAsync(LevelSession scene, GameSessionState application,
        GodotMissionLevelTransitions transitions, GodotLevelLifecycle lifecycle,
        MissionCheckpointSession checkpoints, GodotPlayer player, PkrArchive archive)
    {
        if (application.HasMeta(ProofState))
        {
            ReloadState state = JsonSerializer.Deserialize<ReloadState>(application.GetMeta(ProofState).AsString())
                ?? throw new InvalidDataException("Mission transition proof state was lost.");
            RestartPoint restart = checkpoints.Current ?? throw new InvalidDataException("Retry lost the actual authored restart.");
            if (application.GetInstanceId() != state.ApplicationId || scene.GetInstanceId() == state.SceneId ||
                application.MissionName != state.MissionName || application.SceneAsset != state.SceneAsset ||
                application.CarriedHistory != state.History || checkpoints.Save.RestartPointName != state.RestartName ||
                restart.Name != state.RestartName || lifecycle.ExitReason != LevelExitReason.Running ||
                application.ExitReason != LevelExitReason.Running || transitions.SceneTeardownCompleted ||
                player.Controller.Position != new OpenSpidey.Core.Mathematics.FixedVector3(
                    restart.Position.X, restart.Position.Y, restart.Position.Z) << 12)
                throw new InvalidDataException("Mission 181 retry did not retain its exact mission, checkpoint and carried-save owner across actual native scene replacement.");
            application.RemoveMeta(ProofState);
            GD.Print("PASS: genuine mission181 status9, source302 ushort owner/reset, actual gameplay teardown, once-only retry button, SceneTree reload and persistent raw carried-save history.");
            scene.GetTree().Quit();
            return;
        }

        if (!ReferenceEquals(scene.GetTree().Root.GetNode<GameSessionState>("GameSession"), application))
            throw new InvalidDataException("Mission transition proof requires the actual application autoload.");
        RestartPoint initial = checkpoints.Current ?? throw new InvalidDataException("Mission transition proof requires an initialized source restart.");
        checkpoints.Select(initial.Name.Equals("Re_Start_Death", StringComparison.OrdinalIgnoreCase)
            ? "Re_Restart01" : "Re_Start_Death");
        RestartPoint changed = checkpoints.Current!;

        var node = new TriggerNode(0, 6, 0, ReadOnlyMemory<byte>.Empty);
        var endNode = new LevelTransitionCommandHandler(MissionOpcode.EndLevelNode, transitions);
        foreach (ushort id in new ushort[] { 0, 32768, 65535, 224 })
        {
            byte[] value = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(value, id);
            endNode.Execute(new(node, 0), new(302, 0, value));
            if (application.EndLevelNode != id) throw new InvalidDataException("Source302 did not retain its unsigned node value.");
        }
        application.ResetEndLevelNode();
        if (application.EndLevelNode != ushort.MaxValue) throw new InvalidDataException("Source autoexec reset did not restore EndLevelNode FFFF.");
        foreach (string mission in new[] { "L1A1_T", "L1A2_T", "L1A2a_T", "L1A3_T", "L1A4_T" })
        {
            string asset = GodotMissionLevelTransitions.ResolveAuthoredGeometry(archive, mission);
            // These identities are independently authored by each original mission's command128.
            if (!asset.Equals("data/" + mission[..^2] + "_G.psx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Authored target {mission} resolved the wrong real geometry {asset}.");
        }

        PlayerCarriedSaveHistory history = application.CarriedHistory;
        string target = string.Create(application.MissionName.Length, application.MissionName, (buffer, source) =>
        {
            for (int index = 0; index < source.Length; index++)
                buffer[index] = source[index] is >= 'A' and <= 'Z' ? (char)(source[index] + 32) : source[index];
        });
        string expectedAsset = GodotMissionLevelTransitions.ResolveAuthoredGeometry(archive, target);
        application.SetMeta(ProofState, JsonSerializer.Serialize(new ReloadState(application.GetInstanceId(), scene.GetInstanceId(),
            target, expectedAsset, changed.Name, history)));
        new LevelTransitionCommandHandler(MissionOpcode.LoadMission, transitions).Execute(new(node, 0),
            new(181, 0, Encoding.Latin1.GetBytes(target + '\0')));
        if (lifecycle.ExitReason != LevelExitReason.RetryRequested || application.ExitReason != LevelExitReason.RetryRequested ||
            application.MissionName != target || application.SceneAsset != expectedAsset ||
            application.CarriedHistory != history || !transitions.SceneTeardownCompleted)
            throw new InvalidDataException("Original same-mission 181 ordering or status9 carried-state retention failed before native navigation.");
        transitions.KillAllSceneObjects();
        if (application.CarriedHistory != history) throw new InvalidDataException("Repeated scene teardown recaptured a different carried snapshot.");
        await application.ToSignal(application.GetTree(), SceneTree.SignalName.ProcessFrame);
        await application.ToSignal(application.GetTree(), SceneTree.SignalName.ProcessFrame);
        if (transitions.HasPlayer) throw new InvalidDataException("The actual mission teardown left its live player allocated.");
        Button retry = Descendants(lifecycle).OfType<Button>().Single(button => button.Text == "Restart checkpoint");
        // Multiple queued presses still perform one navigation and one carried-state teardown.
        retry.EmitSignal(BaseButton.SignalName.Pressed);
        retry.EmitSignal(BaseButton.SignalName.Pressed);
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
