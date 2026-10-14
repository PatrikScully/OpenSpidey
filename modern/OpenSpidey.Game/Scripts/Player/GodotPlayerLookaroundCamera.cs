using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

/// <summary>Actual source entry/exit and camera-placement leaves over the posed player, source mode7 and imported world geometry.</summary>
public sealed class GodotPlayerLookaroundCamera : IPlayerLookaroundInputHost, IPlayerLookaroundTransitionHost,
    IPlayerLookaroundCameraSetupHost, IPlayerLookaroundExitHost
{
    private readonly GodotPlayer player;
    private readonly PlayerFollowCamera camera;
    private readonly GodotActorWorldQuery world;
    private readonly Action<bool> setTargetVisible;
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Native lookaround camera requires the genuine posed melee player.");
    public GodotPlayerLookaroundAim Aim { get; }
    public PlayerLookaroundTransitionState State { get; }
    public short EffectiveHeading => player.Controller.Orientation.GetEffectiveHeading(player.Controller.OnWall);
    public FixedQuaternion CameraRotation => camera.SourceRotation;
    public FixedVector3 CameraPosition => camera.SourcePosition;

    public GodotPlayerLookaroundCamera(GodotPlayer player, GodotPlayerLookaroundAim aim,
        PlayerFollowCamera camera, GodotActorWorldQuery world, Action<bool> setTargetVisible)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        Aim = aim ?? throw new ArgumentNullException(nameof(aim));
        this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.setTargetVisible = setTargetVisible ?? throw new ArgumentNullException(nameof(setTargetVisible));
        State = new(aim.State);
    }
    public void EnterLookaroundMode()
    {
        PlayerCombatState combat = player.Combat.State;
        PlayerLookaroundTransition.Enter(State, player.Controller.Orientation.Basis, combat.UpDirection,
            combat.Field8E8 != 0, combat.Field8E9 != 0, this);
        Melee.Kick.SurfaceTransition = Aim.State.Active ? (byte)1 : (byte)0;
    }
    public void ExitLookaroundMode()
    {
        PlayerLookaroundCameraSetup.Exit(State, this);
        Melee.Kick.SurfaceTransition = Aim.State.Active ? (byte)1 : (byte)0;
        ApplyJoints();
    }
    public PlayerLookaroundCameraSetupResult UpdateCameraPlacement(int elapsedTicks)
    {
        var neck = player.Model.GetSourceJoint(1); var head = player.Model.GetSourceJoint(3);
        Aim.State.NeckPitch = neck.RotationX; Aim.State.NeckYaw = neck.RotationY;
        Aim.State.HeadPitch = head.RotationX; Aim.State.HeadYaw = head.RotationY;
        PlayerLookaroundCameraSetupResult result = PlayerLookaroundCameraSetup.Step(State, player.Controller.Position,
            player.Controller.Orientation.Basis, player.Combat.State.Field8E9 != 0, elapsedTicks, this);
        Melee.Kick.SurfaceTransition = Aim.State.Active ? (byte)1 : (byte)0;
        ApplyJoints();
        return result;
    }
    private void ApplyJoints()
    {
        var neck = player.Model.GetSourceJoint(1); var head = player.Model.GetSourceJoint(3);
        player.Model.SetSourceJoint(1, neck with { RotationX = Aim.State.NeckPitch, RotationY = Aim.State.NeckYaw });
        player.Model.SetSourceJoint(3, head with { RotationX = Aim.State.HeadPitch, RotationY = Aim.State.HeadYaw });
    }
    public FixedVector3 GetPlayerHookPosition(int hook) => Melee.GetPlayerHookPosition(hook);
    public void PushCameraMode() => camera.PushLookaroundMode();
    public void SetCameraMode(ushort mode)
    {
        if (mode != 7) throw new InvalidOperationException("Native lookaround entry must set actual source front mode7.");
        camera.SetFrontLookaroundMode();
    }
    public FixedQuaternion[] AllocateTransitionPath(int count) => new FixedQuaternion[count];
    public void DeleteTransitionPath(FixedQuaternion[] path) { } // Managed array retirement has no native allocator side effect.
    public void PopCameraMode() => camera.PopLookaroundMode();
    public void PutCameraBehind(int argument) => player.Combat.PutCameraBehind(argument);
    public void SetTargetVisible(bool visible) => setTargetVisible(visible);
    public void SetCinematicScreen(int value)
    {
        camera.ScriptRig.CinematicScreen.WideScreenValue = value;
        camera.ScriptRig.CinematicScreen.WideScreenShadow = value;
    }
    public void SetDesiredCameraRotation(FixedQuaternion rotation) => camera.SetLookaroundDesiredRotation(rotation);
    public void SetCameraPosition(FixedVector3 position) => camera.SetLookaroundPosition(position);
    public void SetTargetTorsoAngle(short wanted, bool fast) => Melee.SetTargetTorsoAngle(wanted, fast);
    public PlayerLookaroundCameraTrace TraceWorld(FixedVector3 start, FixedVector3 end)
    {
        GodotActorWorldHit? hit = world.Trace(start, end, includeEnvironmentalActors: true);
        int length = PlayerLookaroundCameraSetup.SourceLineLength(start, end);
        int distance = hit is null ? int.MaxValue : PlayerLookaroundCameraSetup.SourceLineLength(start, hit.Position);
        return new(hit is not null, distance, length, hit?.Position ?? default);
    }
}
