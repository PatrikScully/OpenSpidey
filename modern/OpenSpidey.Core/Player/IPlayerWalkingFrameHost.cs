namespace OpenSpidey.Core.Player;

public interface IPlayerWalkingFrameHost
{
    void FinishCrawlPhysics(PlayerCrawlStep step);
    void FinishTerrainPhysics(PlayerTerrainStep step);
    void UpdateWalking(ushort completedAnimation);
    void UpdateInterior(ushort completedAnimation);
    void UpdateWallCompletion();
}
