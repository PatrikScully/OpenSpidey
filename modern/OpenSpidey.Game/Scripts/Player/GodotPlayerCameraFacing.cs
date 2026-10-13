using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Player;

/// <summary>Original camera-facing finalizer with genuine source camera transitions and E70 generation recovery.</summary>
public sealed class GodotPlayerCameraFacing : IPlayerCameraFacingHost, IPlayerCameraFacingFrameHost,
    IPlayerTorsoAimFinalizerHost, IPlayerTorsoAimFinalizerFrameHost
{
    private readonly GodotPlayer player;
    private readonly PlayerFollowCamera camera;
    private readonly GodotPlayerMeleeDirectory directory;
    public PlayerCameraFacingState State { get; }
    public short CameraHeading => unchecked((short)camera.HeadingTransition.PublishedHeading);
    public short CameraReferenceHeading => camera.HeadingTransition.Heading;
    public short InputHeading => player.Controller.MovementInput.Heading;
    internal int TorsoUpdates { get; private set; }
    internal uint LastTorsoAction { get; private set; }
    internal uint LastTorsoClock { get; private set; }

    public GodotPlayerCameraFacing(GodotPlayer player, PlayerFollowCamera camera, GodotPlayerMeleeDirectory directory,
        PlayerSurfaceTransitionState surface, PlayerLookaroundState lookaround)
    {
        this.player = player; this.camera = camera; this.directory = directory;
        State = new((player.Melee ?? throw new InvalidOperationException("Camera facing requires the shared source input owner.")).State, surface, lookaround);
    }
    public short UpdateCameraFacing()
    {
        PlayerCameraFacing.Update(State, this);
        return CameraHeading;
    }
    public void UpdateTorsoAim(short inputHeading, short cameraHeading)
    {
        TorsoUpdates++;
        LastTorsoAction = State.Melee.Combat.ActionState;
        LastTorsoClock = player.Combat.Timer;
        PlayerTorsoAimFinalizer.Update(State.Melee, inputHeading, cameraHeading, this);
    }
    public void SetTargetTorsoAngle(short wanted, bool fast) => player.Melee!.SetTargetTorsoAngle(wanted, fast);
    public void LockTargetTorsoAngle() => player.Melee!.LockTargetTorsoAngle();
    public short GetEffectiveHeading() => player.Controller.Orientation.GetEffectiveHeading(player.Controller.OnWall);
    /// <summary>Actual CPlayer.SetFocusLockTarget uses Mem_MakeHandle, including its null clear.</summary>
    public void SetFocusLockTarget(GodotScriptActor? target) => State.CameraTarget = directory.MakeHandle(target);
    public FixedVector3? RecoverCameraTarget(ref PlayerKickHandle handle) => directory.Recover(ref handle)?.SourcePosition;
    public void PutCameraBehind(int duration) => camera.PutBehind(duration);
    public void SetCameraAngle(short heading, ushort duration) => camera.SetCameraAngle(heading, duration);
}
