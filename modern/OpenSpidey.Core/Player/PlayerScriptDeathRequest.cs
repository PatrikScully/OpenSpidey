namespace OpenSpidey.Core.Player;

public interface IPlayerScriptDeathHost
{
    int LevelId { get; }
    void SetLevelStatus(int value);
    void CallTrainingDeathModule();
}

/// <summary>Whole SwitchToDeathMode(true) early-return path; it leaves every player body/resource field untouched.</summary>
public static class PlayerScriptDeathRequest
{
    public static void Request(IPlayerScriptDeathHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        uint group = unchecked((uint)host.LevelId) >> 8;
        if (group is >= 9 and <= 0x17) host.CallTrainingDeathModule();
        else host.SetLevelStatus(2);
    }
}
