namespace OpenSpidey.Core.Player;

/// <summary>Direct player-owned swing phases, independent of source world actor lists.</summary>
public interface IPlayerSwingFrameHost
{
    void PreparePlayerFrame();
    PlayerSwingPhysicsResult? StepPhysics();
    void UpdateWindup();
    void UpdateRelease();
    void UpdateSwinging();
    void UpdateSurfaceTransition(ushort completedAnimation);
}
