using Godot;

namespace OpenSpidey.Game.Actors;

/// <summary>Observes actual native destruction, including bodies freed before ever entering the tree.</summary>
internal partial class GodotNativeDestructionObserver : Node
{
    internal Action? Destroyed { get; set; }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        Action? callback = Destroyed;
        Destroyed = null;
        callback?.Invoke();
    }
}
