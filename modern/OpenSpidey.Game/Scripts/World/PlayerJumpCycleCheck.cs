using Godot;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal sealed class PlayerJumpCycleCheck(GodotPlayer player, Action<bool, string> assert)
{
    private int stage;
    private int lastUpdate;
    public bool Finished { get; private set; }

    public void Advance()
    {
        if (Finished) return;
        if (player.SourceUpdates != lastUpdate)
        {
            player.Model.VerifyCurrentPose();
            if (Mathf.Abs(player.Transform.Basis.Determinant() - 1) > 0.01f)
                throw new InvalidDataException("Scheduled jumping lost a complete body basis.");
            lastUpdate = player.SourceUpdates;
        }
        if (stage == 0)
        {
            if (!player.Controller.IsGrounded) return;
            int update = player.SourceUpdates;
            player.SampleButtons = () => new(PlayerButton.Jump,
                player.SourceUpdates == update ? PlayerButton.Jump : PlayerButton.None);
            stage = 1;
            return;
        }
        if (stage == 1)
        {
            if (!player.Controller.OnCeiling) return;
            assert(player.Controller.Position.Y == -(104 << 12) && player.Model.ClipId is 227 or 234,
                "The original player reaches native ceiling support through held jumping.");
            player.SampleButtons = () => default;
            stage = 2;
            return;
        }
        if (stage == 2)
        {
            if (player.Model.ClipId != 19) return;
            int update = player.SourceUpdates;
            player.SampleButtons = () => player.SourceUpdates == update ? new(PlayerButton.Jump, PlayerButton.Jump) : default;
            stage = 3;
            return;
        }
        if (player.Controller.IsCrawling || player.Controller.Action != PlayerAction.Idle || !player.Controller.IsGrounded) return;
        assert(player.Controller.Position.Y == -(96 << 12) && player.Controller.Orientation.Basis.M11 == 4096,
            "The original actor releases ceiling support and lands with complete floor orientation.");
        player.Model.VerifyPlayback();
        Finished = true;
        GD.Print($"Verified original jump actor: {player.SourceUpdates} source updates, jump/ceiling/release/landing and live skin poses.");
    }
}
