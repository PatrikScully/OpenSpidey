namespace OpenSpidey.Core.Player;

public interface IPlayerCameraStartupHost
{
    bool HasCamera { get; }
    int CameraMode { get; }
    void SetFloorParameters();
    void PutCameraBehind();
    void SetCameraStartPosition();
}

/// <summary>One-time player53C check at the beginning of CPlayer::AI, before manual skip and private webs.</summary>
public sealed class PlayerCameraStartup(byte initialized = 0)
{
    public byte Initialized { get; private set; } = initialized;
    public void Advance(IPlayerCameraStartupHost host)
    {
        if (Initialized != 0 || !host.HasCamera) return;
        if (host.CameraMode == 3) host.SetFloorParameters();
        host.PutCameraBehind();
        host.SetCameraStartPosition();
        Initialized = 1;
    }
}
