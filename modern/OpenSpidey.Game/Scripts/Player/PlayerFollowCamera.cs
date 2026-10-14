using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.World;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Player;

/// <summary>The source CCamera: one CameraAi417CB0 update per source frame, displayed through this node.</summary>
public partial class PlayerFollowCamera : Camera3D
{
    private float yaw;
    private int physicsTicks;
    private bool aiUpdated;
    private Transform3D? appliedTransform;
    private CameraAi? ai;
    public CameraShakeState ShakeState {get;}=new();
    public Func<uint>? SourceTotalTime {get;set;}
    public ScriptedCameraRig ScriptRig { get; } = new();
    public bool SourceRenderPhase { get; set; }
    public GodotPlayer Target { get; init; } = null!;
    public int FocusHookIndex { get; private set; } = -1;
    public CameraAi Ai => ai ??= new(ScriptRig, Tripod, SourceNormalPlacement, ShakeState);
    /// <summary>Source gBossRelated0x56E998 for CM_Boss3; null when no boss body is registered.</summary>
    public Func<CameraFocusTarget?>? SourceBoss { get; set; }
    /// <summary>The416B10 item-hiding phase for field F9. Null keeps CameraPlacement's NotSupported guard.</summary>
    public Action? SourceHideItems { get => Ai.HideItems; set => Ai.HideItems = value; }
    /// <summary>Source field23A after the last AI update; the orbit heading before the first one.</summary>
    public short SourceHeading => aiUpdated && PoseOwned ? ScriptRig.ModeState.ViewHeading : OrbitSourceHeading;
    public FixedVector3 SourcePosition => PoseOwned ? ScriptRig.ModeState.Position : PsxCoordinates.OriginalWorldPosition(GlobalPosition);
    public FixedJointTransform SourceViewRotation => FixedRotation.TransposeRotation(
        PoseOwned ? ScriptRig.ModeState.DisplayRotation.ToMatrix() : PsxCoordinates.OriginalRotation(GlobalBasis));
    /// <summary>Source display quaternion214, including shake.</summary>
    public FixedQuaternion SourceRotation => PoseOwned ? ScriptRig.ModeState.DisplayRotation
        : FixedQuaternion.FromMatrix(PsxCoordinates.OriginalRotation(GlobalBasis));
    private bool PoseOwned => appliedTransform == GlobalTransform;
    private static short HeadingFor(Vector3 forward) =>
        (short)((1024 - GameAngle.Atan2((int)(forward.Z * 4096), (int)(-forward.X * 4096))) & 4095);
    private short OrbitSourceHeading
    {
        get
        {
            Vector3 forward = -new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
            return HeadingFor(forward);
        }
    }

    public override void _Ready()
    {
        Current = true;
        Near = 0.05f;
        Far = 2000;
        KeepAspect = KeepAspectEnum.Height;
        ScriptRig.BindAngleLock(() => Target.Combat.State.CameraAngleLock != 0,
            locked => Target.Combat.State.CameraAngleLock = locked ? (byte)1 : (byte)0);
        Fov = (float)CameraProjection.VerticalFieldOfView(ScriptRig.Zoom.Value);
        yaw = (2048 - Target.Controller.Orientation.Heading) * (Mathf.Tau / 4096);
        ScriptRig.Heading = OrbitSourceHeading;
        InitializeSourceTracking();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            Input.MouseMode = Input.MouseModeEnum.Captured;
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
            Input.MouseMode = Input.MouseModeEnum.Visible;
        if (@event is not InputEventMouseMotion motion || Input.MouseMode != Input.MouseModeEnum.Captured) return;
        yaw -= motion.ScreenRelative.X * 0.003f;
        SynchronizeSourceHeadingFromOrbit();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (SourceRenderPhase) return;
        physicsTicks++;
        AdvanceCameraFrame(physicsTicks % 2 == 0);
    }

    /// <summary>Source camera update after Logic's pending commands/skip cleanup, before HUD/world rendering.</summary>
    public void RenderSourceFrame()
    {
        if (!SourceRenderPhase) throw new InvalidOperationException("The ordinary native physics camera cannot also run the source render phase.");
        AdvanceCameraFrame(true);
    }

    private void AdvanceCameraFrame(bool sourceUpdate)
    {
        if (sourceUpdate)
        {
            InitializeSourceTracking();
            SynchronizeFromNode();
            CameraAi camera = Ai;
            camera.Projection = SourceProjectionCache ??= new();
            camera.Update(2, sourceHost ??= new(this));
            aiUpdated = true;
            ApplySourceOrbitHeading();
        }
        Fov = (float)CameraProjection.VerticalFieldOfView(ScriptRig.Zoom.Value);
        if (sourceUpdate || !PoseOwned) ApplySourcePose(ScriptRig.ModeState.DisplayRotation);
    }

    /// <summary>Another owner moved the node; the source camera resumes from that displayed pose.</summary>
    private void SynchronizeFromNode()
    {
        if (PoseOwned) return;
        CameraModeState state = ScriptRig.ModeState;
        state.Position = PsxCoordinates.OriginalWorldPosition(GlobalPosition);
        state.CurrentRotation = state.DisplayRotation = FixedQuaternion.FromMatrix(PsxCoordinates.OriginalRotation(GlobalBasis));
        appliedTransform = GlobalTransform;
    }

    private void ApplySourcePose(FixedQuaternion rotation)
    {
        GlobalPosition = PsxCoordinates.FixedWorldPosition(ScriptRig.ModeState.Position);
        // Camera3D requires a pure rotation; the fixed-point rounding stays in the source state.
        GlobalBasis = PsxCoordinates.CharacterTransform(rotation.ToMatrix()).Basis.Orthonormalized();
        appliedTransform = GlobalTransform;
    }

    public void Shake(FixedVector3 position,int magnitude)
    {
        if(SourceTotalTime is null)throw new InvalidOperationException("Camera shake requires the owning session's source total-time clock.");
        ShakeState.Shake(position,magnitude);
    }

    public void PutBehind(int duration) => SourcePutBehind(duration);

    public void SetFocusHook(int sourceIndex) => SetSourceFocusHook(sourceIndex);
}
