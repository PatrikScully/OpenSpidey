using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal static class InputSuppressionImportChecks
{
    public static async Task VerifyAsync(Node owner, GodotPlayer player)
    {
        var position = player.Controller.Position;
        var movement = player.SampleInput;
        var buttons = player.SampleButtons;
        try
        {
            var command = new TriggerNode(0, 4, 0, new byte[] { 4, 0, 163, 0, 4, 0, 255, 255 });
            new PlayerInputCommandHandler(duration => player.Controller.SetIgnoreInputTimer(duration))
                .Execute(new(command, 0), TriggerCommandReader.Read(command).Single());
            player.SampleInput = () => new(true, new(-127, 0), false, false, false, false, false, false);
            player.SampleButtons = () => new(PlayerButton.Jump, PlayerButton.Jump);
            await StepAsync(owner, player);
            if (player.Controller.InputSuppression.RemainingTicks != 2 || player.Controller.Action != PlayerAction.Idle ||
                player.Controller.Position != position || player.Controller.Velocity != default)
                throw new InvalidDataException("Input lock failed to suppress physical movement/jump in Godot.");
            await StepAsync(owner, player);
            if (player.Controller.InputSuppression.RemainingTicks != 0 || player.Controller.Action != PlayerAction.GroundMovement)
                throw new InvalidDataException("Exact-zero input expiry did not admit analog movement while swallowing jump.");
            player.Controller.SetIgnoreInputTimer(8);
            player.Scripts!.Start(PlayerScriptProgram.Read(new byte[] { 0, 0, 9, 0, 4, 0, 0, 0, 255, 0 }));
            for (int step = 0; step < 5; step++)
            {
                await StepAsync(owner, player);
                if (player.Controller.InputSuppression.RemainingTicks != 8)
                    throw new InvalidDataException("Native player script consumed the physical-input timer.");
            }
            if (player.Scripts.Active) throw new InvalidDataException("The native input-lock wait script did not finish.");
        }
        finally
        {
            player.SetPhysicsProcess(false);
            player.SampleInput = movement;
            player.SampleButtons = buttons;
            player.Controller.SetIgnoreInputTimer(0);
            player.Controller.SetScriptPosition(position);
            player.Controller.SetScriptVelocity(default);
            player.Controller.Stand();
        }
        GD.Print("Verified Godot input suppression: mission duration, physical controls, exact-zero expiry and paused timer during scripted input.");
    }

    private static async Task StepAsync(Node owner, GodotPlayer player)
    {
        int before = player.SourceUpdates;
        player.SetPhysicsProcess(true);
        for (int frame = 0; frame < 4 && player.SourceUpdates == before; frame++)
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        player.SetPhysicsProcess(false);
        if (player.SourceUpdates != before + 1) throw new InvalidDataException("Input-lock check did not advance exactly one source update.");
    }
}
