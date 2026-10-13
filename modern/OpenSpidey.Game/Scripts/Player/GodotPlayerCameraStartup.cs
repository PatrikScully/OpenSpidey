using Godot;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

/// <summary>The real camera provider may be absent at player construction; source53C remains zero until it exists.</summary>
public sealed class GodotPlayerCameraStartup(GodotPlayer player, Func<PlayerFollowCamera?> camera) : IPlayerCameraStartupHost
{
    public PlayerCameraStartup State { get; } = new();
    private PlayerFollowCamera? Camera => camera() is { } value && GodotObject.IsInstanceValid(value) ? value : null;
    private PlayerFollowCamera RequireCamera()
    {
        PlayerFollowCamera value = Camera ?? throw new InvalidOperationException("The source camera was destroyed during player initialization.");
        if (!ReferenceEquals(value.Target, player)) throw new InvalidOperationException("Player camera initialization requires the actual camera tripod player.");
        return value;
    }
    public bool HasCamera => Camera is not null;
    public int CameraMode => RequireCamera().SourceControlMode;
    public void Advance() => State.Advance(this);
    public void SetFloorParameters() => RequireCamera().SetFloorCamera(0);
    public void PutCameraBehind() => RequireCamera().PutBehind(0);
    public void SetCameraStartPosition() => RequireCamera().SetStartPosition();
}
