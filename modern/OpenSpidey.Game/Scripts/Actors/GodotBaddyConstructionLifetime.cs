using Godot;

namespace OpenSpidey.Game.Actors;

/// <summary>Native Free/teardown observer; tree exit and RemoveChild do not represent destruction.</summary>
internal partial class GodotBaddyConstructionLifetime : Node
{
    internal Action? ReleaseConstruction { get; set; }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        Action? release = ReleaseConstruction;
        ReleaseConstruction = null;
        release?.Invoke();
    }
}
