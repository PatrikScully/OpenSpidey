using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Exercises the actual retry button and SceneTree reload with application-owned checkpoint state.</summary>
internal static class CheckpointReloadImportChecks
{
    private const string ExpectedName = "checkpoint_reload_expected";
    private const string OriginalApplication = "checkpoint_reload_application";

    public static async Task VerifyAsync(LevelSession scene, GameSessionState application,
        MissionCheckpointSession checkpoints, GodotLevelLifecycle lifecycle, GodotPlayer player)
    {
        if (application.HasMeta(ExpectedName))
        {
            string expected = application.GetMeta(ExpectedName).AsString();
            ulong identity = application.GetMeta(OriginalApplication).AsUInt64();
            RestartPoint selected = checkpoints.Current ?? throw new InvalidDataException("Reload lost the current checkpoint.");
            if (application.GetInstanceId() != identity || selected.Name != expected || checkpoints.Save.RestartPointName != expected ||
                player.Controller.Position != new OpenSpidey.Core.Mathematics.FixedVector3(selected.Position.X,
                    selected.Position.Y, selected.Position.Z) << 12)
                throw new InvalidDataException("The actual scene reload did not retain its application owner, saved checkpoint and authored spawn.");
            application.RemoveMeta(ExpectedName);
            application.RemoveMeta(OriginalApplication);
            GD.Print("PASS: actual checkpoint selection, retry UI button, full SceneTree reload, persistent application save and authored checkpoint spawn.");
            scene.GetTree().Quit();
            return;
        }

        RestartPoint initial = checkpoints.Current ?? throw new InvalidDataException("Checkpoint smoke requires an initialized level.");
        if (!ReferenceEquals(scene.GetTree().Root.GetNode<GameSessionState>("GameSession"), application))
            throw new InvalidDataException("Checkpoint smoke requires the actual application autoload owner.");
        CheckpointSelection changed = checkpoints.Select(initial.Name.Equals("Re_Start_Death", StringComparison.OrdinalIgnoreCase)
            ? "Re_Restart01" : "Re_Start_Death");
        if (!changed.SaveRequired || checkpoints.Save.RestartPointName != changed.Point.Name)
            throw new InvalidDataException("Source checkpoint selection did not update the shared restart-name field.");
        application.SetMeta(ExpectedName, changed.Point.Name);
        application.SetMeta(OriginalApplication, application.GetInstanceId());
        lifecycle.RequestExit((int)LevelExitReason.PlayerDied);
        await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.ProcessFrame);
        await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.ProcessFrame);
        Button retry = Descendants(lifecycle).OfType<Button>().Single(button => button.Text == "Restart checkpoint");
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
