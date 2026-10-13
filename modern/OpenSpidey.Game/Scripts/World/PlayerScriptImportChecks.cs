using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Player.Scripting;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal static class PlayerScriptImportChecks
{
    public static async Task VerifyAsync(Node owner, PkrArchive archive, GodotPlayer player, PlayerFollowCamera camera)
    {
        GodotPlayerScripts scripts = player.Scripts ?? throw new InvalidDataException("Player has no script host.");
        var savedPosition = player.Controller.Position;
        var physicalMovement = player.SampleInput;
        var physicalButtons = player.SampleButtons;
        TriggerFile file = TriggerFile.Read(archive.Read("data/L1A1_T.trg"));
        var handlers = CameraCommandHandler.CreateHandlers(camera.ScriptRig, () => default)
            .Append(new PlayerScriptCommandHandler(scripts.Start));
        var mission = new MissionRuntime(file, new UnexpectedActorFactory(), handlers);
        mission.ReceivePulse(77);
        if (mission.ExecutePending() != 1 || !scripts.Active)
            throw new InvalidDataException("Original player script did not start through its mission command point.");
        player.SampleInput = () => new(true, new(-127, 0), false, false, false, false, false, false);
        player.SampleButtons = () => new(PlayerButton.Jump, PlayerButton.Jump);
        bool teleported = false, observedAnimation = false;
        int updates = player.SourceUpdates;
        player.SetPhysicsProcess(true);
        try
        {
            for (int frame = 0; frame < 130 && scripts.Active; frame++)
            {
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                teleported |= player.Controller.Position.X == scripts.TriggerPosition(75).X &&
                    player.Controller.Position.Z == scripts.TriggerPosition(75).Z;
                observedAnimation |= player.Model.ClipId == 176;
                if (scripts.LastInput is { } input && PlayerScriptControlMapper.Map(input).Buttons != default)
                    throw new InvalidDataException("Physical jump input leaked into the scripted pad.");
            }
            player.SetPhysicsProcess(false);
            if (scripts.Active || !teleported || !observedAnimation || scripts.Runtime?.PendingCommands != 0 ||
                scripts.LastAnimationPolicy != ScriptAnimationPolicy.ResetSoundScript || player.SourceUpdates - updates != 55 ||
                scripts.LastInput?.Axes != default(PlayerAxes))
                throw new InvalidDataException($"Original player script failed: teleport={teleported}, animation={observedAnimation}, " +
                    $"active={scripts.Active}, updates={player.SourceUpdates - updates}, pending={scripts.Runtime?.PendingCommands}.");
            player.Model.VerifyPlayback();
        }
        finally
        {
            player.SampleInput = physicalMovement;
            player.SampleButtons = physicalButtons;
            player.Controller.SetScriptPosition(savedPosition);
            player.Controller.SetScriptVelocity(default);
            player.Controller.Stand();
        }
        player.SetPhysicsProcess(true);
        for (int frame = 0; frame < 3; frame++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        player.SetPhysicsProcess(false);
        GD.Print("Verified Godot player script: original mission node 77, teleport, animation 176, physical input override and timed completion.");
    }

    private sealed class UnexpectedActorFactory : IMissionActorFactory
    {
        public void Spawn(TriggerNode node) => throw new InvalidOperationException($"Player script check unexpectedly spawned node {node.Id}.");
    }
}
