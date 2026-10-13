using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

public sealed partial class GodotPlayerSwing : IPlayerSwingSelectionHost
{
    private PlayerSwingSelectionState? selection;
    private SwingZipHost? swingZip;
    public PlayerSwingSelectionState Selection => selection ?? throw new InvalidOperationException("Swing-button selection requires the genuine shared camera/airborne owners.");
    public GodotPlayerCameraFacing? CameraFacing { get; private set; }
    public bool PracticeMode => PracticeDifficulty != 0;
    public byte AirborneReleaseLatch => State.Targets.Transition.Angles.JumpReleased;
    public byte SwingHeldLatch => State.Targets.Transition.Angles.SwingHeldLatch;

    public void ConfigureSelection(GodotPlayerAirborneAttackSelection airborne, GodotPlayerMeleeDirectory directory)
    {
        if (selection is not null) throw new InvalidOperationException("The player already owns its swing fan/camera selector.");
        selection = new(Melee.State, State.Availability, State.Targets);
        swingZip = new(this, airborne);
        CameraFacing = new(player, camera, directory, State.Surface, State.Targets.Transition.Angles);
    }

    public bool CheckJumpingSwingWeb()
    {
        bool accepted = PlayerSwingSelection.Check(Selection, this);
        fire.ExplicitAimPosition = State.Targets.TargetPosition;
        fire.ExplicitAimSurfaceNormal = State.Targets.SurfaceNormal;
        return accepted;
    }

    public bool CheckJumpingR2ZipWeb()
    {
        SwingZipHost host = swingZip ?? throw new InvalidOperationException("Swing-button zip requires the existing actual airborne web owner.");
        PlayerStraightZipState zip = host.Airborne.Zip;
        zip.TargetPosition = fire.ExplicitAimPosition; zip.SurfaceNormal = fire.ExplicitAimSurfaceNormal;
        bool accepted = PlayerSwingZipSelection.Check(zip, host);
        if (accepted) { fire.ExplicitAimPosition = zip.TargetPosition; fire.ExplicitAimSurfaceNormal = zip.SurfaceNormal; }
        return accepted;
    }

    public PlayerSwingProbeContact? TraceSwingWorld(FixedVector3 start, FixedVector3 end)
    {
        if (world.Trace(start, end, includeEnvironmentalActors: true) is not { } hit) return null;
        var normal = GodotImpactWebNativeHost.Normal(hit);
        return new(hit.Position, new(normal.X, normal.Y, normal.Z), hit.Triangle.PackedMetadata,
            PlayerLookaroundCameraSetup.SourceLineLength(start, hit.Position));
    }

    public bool CheckSwingAvailability(PlayerSwingProbeContact contact)
    {
        PlayerSwingWebAvailabilityState shared = State.Availability;
        shared.TargetPosition = State.Targets.TargetPosition; shared.PlaneNormal = State.Targets.SurfaceNormal;
        PlayerCombatState combat = Melee.State.Combat;
        bool accepted = PlayerSwingWebAvailability.Check(shared, new(combat.Position, contact.Position,
            unchecked((short)contact.Normal.Y), contact.Distance, contact.FaceFlags, combat.ActionState, combat.Field8E9 != 0));
        State.Targets.TargetPosition = shared.TargetPosition; State.Targets.SurfaceNormal = shared.PlaneNormal;
        return accepted;
    }
    public void SetSwingCameraResetLatch(byte value) => Melee.State.Combat.SwingCameraResetLatch = value;

    private sealed class SwingZipHost(GodotPlayerSwing owner, GodotPlayerAirborneAttackSelection airborne) : IPlayerSwingZipSelectionHost
    {
        public GodotPlayerAirborneAttackSelection Airborne { get; } = airborne;
        public bool PracticeMode => owner.PracticeMode;
        public byte AirborneReleaseLatch => owner.AirborneReleaseLatch;
        public byte SwingHeldLatch => owner.SwingHeldLatch;
        public bool SwingPresent => owner.SwingPresent;
        public int Random(int bound) => owner.Random(bound);
        public PlayerStraightZipContact? TraceWorld(FixedVector3 start, FixedVector3 end) => Airborne.TraceWorld(start, end);
        public bool CheckZipAvailability(PlayerStraightZipContact contact, int range) => Airborne.CheckZipAvailability(contact, range);
        public void ReleaseSwing(bool createSwingBack) => Airborne.ReleaseSwing(createSwingBack);
        public void SetCameraFocusHook(int hook) => Airborne.SetCameraFocusHook(hook);
        public WebStrandOwner CreateCurrentStrand() => Airborne.CreateCurrentStrand();
        public int FireWeb(PlayerFireWebRequest request) => Airborne.FireWeb(request);
        public void SelectAndRunAnimation(ushort animation, int firstFrame) => Airborne.SelectAndRunAnimation(animation, firstFrame);
        public void BeginZip(PlayerStraightZipContact contact) => Airborne.BeginZip(contact);
    }
}
