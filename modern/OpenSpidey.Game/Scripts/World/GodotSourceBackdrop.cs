using Godot;
using OpenSpidey.Core.Presentation;

namespace OpenSpidey.Game.World;

/// <summary>The actual source502 body identity rendered by its camera-centered native mesh.</summary>
public partial class GodotSourceBackdrop : MeshInstance3D
{
    internal Action? Destroyed { get; set; }
    public BackgroundState State { get; init; } = null!;
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        Action? destroy = Destroyed; Destroyed = null; destroy?.Invoke();
    }
}
