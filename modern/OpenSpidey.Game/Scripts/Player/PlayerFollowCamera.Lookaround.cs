using Godot;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Player;

public partial class PlayerFollowCamera
{
    public CameraModeState LookaroundMode => ScriptRig.ModeState;
    public bool LookaroundModeEnabled => LookaroundMode.Mode == 7;
    public ushort SourceControlMode => ScriptRig.Mode;

    public void PushLookaroundMode()
    {
        LookaroundMode.ViewHeading = SourceHeading;
        LookaroundMode.Mode = SourceControlMode;
        LookaroundMode.Position = SourcePosition;
        LookaroundMode.CurrentRotation = ScriptRig.Enabled && ScriptRig.Mode == 5 ? ScriptRig.FixedOrientation.Value :
            FixedQuaternion.FromMatrix(PsxCoordinates.OriginalRotation(shakeApplied ? unshakenBasis : GlobalBasis));
        LookaroundMode.DisplayRotation = SourceRotation;
        LookaroundMode.PushMode();
    }
    public void SetFrontLookaroundMode()
    {
        LookaroundMode.SetMode(7);
    }
    public void PopLookaroundMode()
    {
        LookaroundMode.PopMode();
        ScriptRig.SetMode(checked((ushort)LookaroundMode.Mode));
        if (LookaroundMode.Mode is 4 or 5 or 6)
        {
            GlobalPosition = PsxCoordinates.FixedWorldPosition(LookaroundMode.Position);
            GlobalBasis = PsxCoordinates.CharacterTransform(LookaroundMode.CurrentRotation.ToMatrix()).Basis.Orthonormalized();
        }
    }
    public void SetSourceCameraMode(ushort mode)
    {
        // Source SetMode writes the current mode without modifying its saved PushMode slot.
        ScriptRig.SetMode(mode);
    }
    public void SetLookaroundDesiredRotation(FixedQuaternion rotation) => LookaroundMode.DesiredRotation = rotation;
    public void SetLookaroundPosition(FixedVector3 position)
    {
        LookaroundMode.Position = position;
        // SetupLookaroundCamera writes camera.mPos directly before the camera's own mode7 update.
        GlobalPosition = PsxCoordinates.FixedWorldPosition(position);
    }

    /// <summary>Call from the camera's existing source/render phase after timers advance and before ordinary-mode dispatch.</summary>
    private bool ApplySourceLookaroundPose(bool sourceUpdate)
    {
        if (!LookaroundModeEnabled) return false;
        if (sourceUpdate)
        {
            // The native camera's genuine default focus is its player; CCamera::AI updates field144 before CalcAim.
            LookaroundMode.FocusPosition = Target.Controller.Position;
            LookaroundMode.MoveFrontToDesiredPosition(false);
            LookaroundMode.CalculateAimAngles();
        }
        GlobalPosition = PsxCoordinates.FixedWorldPosition(LookaroundMode.Position);
        GlobalBasis = PsxCoordinates.CharacterTransform(LookaroundMode.DisplayRotation.ToMatrix()).Basis.Orthonormalized();
        ApplyShake(sourceUpdate);
        if (sourceUpdate) LookaroundMode.LoadViewHeading((shakeApplied ? shakeDisplay : LookaroundMode.DisplayRotation).ToMatrix());
        return true;
    }
}
