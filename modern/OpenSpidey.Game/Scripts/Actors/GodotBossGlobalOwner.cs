using Godot;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Actors;

/// <summary>Source gBossRelated 0x56E998 shared by CSimby (cleared in 0x4A7AE0) and other boss bodies.</summary>
public partial class GodotBossGlobalOwner : Node
{
    private Func<CameraFocusTarget?>? previous;
    public CameraFocusTarget? GlobalBoss { get; private set; }

    public static GodotBossGlobalOwner Create() => new() { Name = "SourceBossGlobal" };

    /// <summary>Chains after an existing SourceBoss publisher so Scorpion and Simby can share the camera slot.</summary>
    public void BindCamera(PlayerFollowCamera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        previous = camera.SourceBoss;
        camera.SourceBoss = () => GlobalBoss ?? previous?.Invoke();
    }

    public void Set(object? identity, Func<FixedVector3>? readPosition)
    {
        if (identity is null || readPosition is null) { GlobalBoss = null; return; }
        GlobalBoss = ReferenceEquals(GlobalBoss?.Identity, identity) ? GlobalBoss : new(identity, readPosition);
    }

    public void ClearIf(object identity)
    {
        if (ReferenceEquals(GlobalBoss?.Identity, identity)) GlobalBoss = null;
    }
}
