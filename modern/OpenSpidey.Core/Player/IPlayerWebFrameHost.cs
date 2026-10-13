namespace OpenSpidey.Core.Player;

/// <summary>Native posed web release and throw actions using the player's shared combat state.</summary>
public interface IPlayerWebFrameHost
{
    bool CheckWebShot();
    void UpdateWindup();
    void UpdateFiring();
    void UpdateAttach();
    void UpdateThrow();
    void UpdateDome(ushort completedAnimation);
}
