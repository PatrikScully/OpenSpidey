namespace OpenSpidey.Core.Player;

public interface IPlayerLookaroundFrameHost
{
    PlayerButtonInput ReadControls(PlayerMovementInputState movement, PlayerButtonInput buttons, byte groundContactGraceUpdates);
    void FinishFrame(int elapsedTicks);
}
