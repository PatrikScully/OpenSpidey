using Godot;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Actors;

/// <summary>Source CameraList presence is independent of player construction; only the actual current native camera receives4298.</summary>
public sealed class GodotActorCameraShakeHost : IActorCameraShakeHost
{
    private readonly Func<PlayerFollowCamera?> sourceCamera;
    public GodotActorCameraShakeHost(Func<PlayerFollowCamera?> sourceCamera) =>
        this.sourceCamera = sourceCamera ?? throw new ArgumentNullException(nameof(sourceCamera));
    public event Action<FixedVector3, int>? SourceShakeApplied;
    public bool CameraPresent => Current is not null;
    private PlayerFollowCamera? Current => sourceCamera() is { } camera && GodotObject.IsInstanceValid(camera) &&
        !camera.IsQueuedForDeletion() ? camera : null;
    public void ShakeCamera(FixedVector3 position, int sourceMode)
    {
        (Current ?? throw new InvalidOperationException("The source4298 camera disappeared during its native command phase.")).Shake(position, sourceMode);
        SourceShakeApplied?.Invoke(position, sourceMode);
    }
}
