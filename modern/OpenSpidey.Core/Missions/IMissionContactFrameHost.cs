namespace OpenSpidey.Core.Missions;

/// <summary>Source command-point contact flags are cleared before the player and rearmed before the other actor lists.</summary>
public interface IMissionContactFrameHost
{
    void BeginContactFrame();
    void EndContactFrame();
}
