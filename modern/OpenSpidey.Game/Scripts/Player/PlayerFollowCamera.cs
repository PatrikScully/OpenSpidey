using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.World;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Player;

/// <summary>A usable orbit camera while original mission camera behaviors are ported.</summary>
public partial class PlayerFollowCamera : Camera3D
{
    private float yaw;
    private float pitch = 0.25f;
    private int physicsTicks;
    private bool shakeSampleValid;
    private bool shakeApplied;
    private FixedQuaternion shakeDisplay;
    private Basis unshakenBasis;
    private short unshakenHeading;
    public CameraShakeState ShakeState {get;}=new();
    public Func<uint>? SourceTotalTime {get;set;}
    public ScriptedCameraRig ScriptRig { get; } = new();
    public bool SourceRenderPhase { get; set; }
    public GodotPlayer Target { get; init; } = null!;
    public bool TerrainObstructed { get; private set; }
    public int FocusHookIndex { get; private set; } = -1;
    public short SourceHeading
    {
        get
        {
            if (LookaroundModeEnabled) return LookaroundMode.ViewHeading;
            if (!ScriptRig.Enabled) return OrbitSourceHeading;
            if (ScriptRig.Mode is 4 or 5) return shakeApplied ? unshakenHeading : HeadingFor(-GlobalBasis.Z);
            return ScriptRig.Heading;
        }
    }
    public FixedVector3 SourcePosition => PsxCoordinates.OriginalWorldPosition(GlobalPosition);
    public FixedJointTransform SourceViewRotation => FixedRotation.TransposeRotation(
        shakeApplied ? shakeDisplay.ToMatrix() : LookaroundModeEnabled ? LookaroundMode.DisplayRotation.ToMatrix()
            : ScriptRig.Enabled && ScriptRig.Mode == 5 ? ScriptRig.FixedOrientation.Value.ToMatrix()
            : PsxCoordinates.OriginalRotation(GlobalBasis));
    public FixedQuaternion SourceRotation => shakeApplied ? shakeDisplay : LookaroundModeEnabled ? LookaroundMode.DisplayRotation
        : ScriptRig.Enabled && ScriptRig.Mode == 5
        ? ScriptRig.FixedOrientation.Value : FixedQuaternion.FromMatrix(PsxCoordinates.OriginalRotation(GlobalBasis));
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
        pitch = Mathf.Clamp(pitch + motion.ScreenRelative.Y * 0.003f, -0.2f, 1.2f);
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
        TerrainObstructed = false;
        if(shakeApplied)GlobalBasis=unshakenBasis;
        shakeApplied = false;
        if (sourceUpdate)
        {
            ScriptRig.Advance(2);
            AdvanceSourceTracking(2);
            UpdateNormalProjectionCache();
        }
        Fov = (float)CameraProjection.VerticalFieldOfView(ScriptRig.Zoom.Value);
        if (ApplySourceLookaroundPose(sourceUpdate)) return;
        if (ScriptRig.Enabled)
        {
            ScriptedCameraPose pose = ScriptRig.DesiredPose(SourceTripodPosition);
            Vector3 sourceFocus = PsxCoordinates.FixedWorldPosition(pose.Focus);
            Vector3 desired = PsxCoordinates.FixedWorldPosition(pose.Position);
            GlobalPosition = ScriptRig.Mode is 4 or 5 ? desired : AvoidTerrain(sourceFocus, desired);
            if (ScriptRig.Mode == 5)
            {
                // Camera3D requires a pure rotation; retain fixed-point rounding in the core state.
                GlobalBasis = PsxCoordinates.CharacterTransform(ScriptRig.FixedOrientation.Value.ToMatrix()).Basis.Orthonormalized();
                ApplyShake(sourceUpdate);
                return;
            }
            Vector3 up = -PsxCoordinates.FixedWorldPosition(pose.Down).Normalized();
            if (GlobalPosition.DistanceSquaredTo(sourceFocus) > 0.0001f) LookAt(sourceFocus, up);
            ApplyShake(sourceUpdate);
            return;
        }
        Vector3 focus = PsxCoordinates.FixedWorldPosition(SourceTripodPosition) + Vector3.Up * 0.7f;
        Vector3 offset = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch),
            Mathf.Cos(yaw) * Mathf.Cos(pitch)) * 4;
        Vector3 destination = focus + offset;
        GlobalPosition = AvoidTerrain(focus, destination);
        LookAt(focus);
        ApplyShake(sourceUpdate);
    }

    public void Shake(FixedVector3 position,int magnitude)
    {
        if(SourceTotalTime is null)throw new InvalidOperationException("Camera shake requires the owning session's source total-time clock.");
        ShakeState.Shake(position,magnitude);
        shakeSampleValid=false;
    }
    private void ApplyShake(bool sourceUpdate)
    {
        unshakenBasis=GlobalBasis;
        unshakenHeading=HeadingFor(-GlobalBasis.Z);
        if(sourceUpdate && !ShakeState.Active)shakeSampleValid=false;
        if(ShakeState.Active && (sourceUpdate || !shakeSampleValid))
        {
            uint time=(SourceTotalTime ?? throw new InvalidOperationException("Camera shake lost its owning source clock."))();
            FixedQuaternion baseline=LookaroundModeEnabled ? LookaroundMode.DisplayRotation
                : ScriptRig.Enabled && ScriptRig.Mode==5 ? ScriptRig.FixedOrientation.Value
                : FixedQuaternion.FromMatrix(PsxCoordinates.OriginalRotation(GlobalBasis));
            shakeDisplay=sourceUpdate?ShakeState.Advance(baseline,time):ShakeState.Evaluate(baseline,time);
            shakeSampleValid=true;
        }
        if(!shakeSampleValid)return;
        // Display-only orientation: original camera position/collision and desired script quaternion stay intact.
        GlobalBasis=PsxCoordinates.CharacterTransform(shakeDisplay.ToMatrix()).Basis.Orthonormalized();
        shakeApplied=true;
    }

    public void PutBehind(int duration) => SourcePutBehind(duration);

    public void SetFocusHook(int sourceIndex) => SetSourceFocusHook(sourceIndex);

    private Vector3 AvoidTerrain(Vector3 focus, Vector3 destination)
    {
        Vector3 offset = destination - focus;
        if (offset.LengthSquared() < 0.0001f) return focus + Vector3.Back * 0.01f;
        using var query = PhysicsRayQueryParameters3D.Create(focus, destination, CollisionLayers.CameraTerrain);
        query.HitBackFaces = false;
        query.CollideWithAreas = false;
        using Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count != 0)
        {
            TerrainObstructed = true;
            destination = hit["position"].AsVector3() - offset.Normalized() * 0.15f;
        }
        if (destination.DistanceSquaredTo(focus) < 0.0001f) destination = focus + offset.Normalized() * 0.01f;
        return destination;
    }
}
