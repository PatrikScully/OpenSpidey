namespace OpenSpidey.Core.Player;

public interface IPlayerLandingRecoveryFrameHost
{
    bool CheckForwards(bool allowStart);
    void UpdateRecovery(ushort completedAnimation);
    void UpdateWindup();
}
