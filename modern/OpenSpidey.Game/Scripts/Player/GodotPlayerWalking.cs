using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Player;

internal readonly record struct GodotPlayerWalkingLineSnapshot(bool ItemPresent, uint? FaceFlags, int Distance);

/// <summary>Complete source16/1000/80000 over the production player and its actual physics line, surface, camera and held-object owners.</summary>
public sealed class GodotPlayerWalking : IPlayerWalkingFrameHost, IPlayerWalkingHost, IPlayerRunIntoWallHost
{
    private readonly GodotPlayer player;
    private readonly PlayerFollowCamera camera;
    private readonly GodotPlayerWebRuntime webs;
    private readonly GodotPlayerMeleeSession session;
    private bool lineHasItem;
    private uint? lineFaceFlags;
    private int lineDistance;
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Walking requires the genuine posed melee owner.");
    private GodotPlayerSwing Swing => webs.Swing ?? throw new InvalidOperationException("Walking surface transitions require the existing shared swing/camera owner.");
    private GodotPlayerAirborneAttackSelection Airborne => webs.Airborne ?? throw new InvalidOperationException("Walking attack guards require the existing shared airborne owner.");
    public PlayerSurfaceTransitionState Surface => Swing.State.Surface;
    public bool HardSurface => player.Controller.HardSurface;
    public short InputHeading => player.Controller.MovementInput.Heading;
    public short CameraHeading => camera.SourceHeading;
    internal GodotPlayerWalkingLineSnapshot CapturePrimaryLine() => new(lineHasItem, lineFaceFlags, lineDistance);
    internal void RestorePrimaryLine(GodotPlayerWalkingLineSnapshot snapshot)
    { lineHasItem = snapshot.ItemPresent; lineFaceFlags = snapshot.FaceFlags; lineDistance = snapshot.Distance; }

    public GodotPlayerWalking(GodotPlayer player, PlayerFollowCamera camera, GodotPlayerWebRuntime webs, GodotPlayerMeleeSession session)
    {
        ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(webs); ArgumentNullException.ThrowIfNull(session);
        this.player = player; this.camera = camera; this.webs = webs; this.session = session;
        if (!ReferenceEquals(Surface.Melee, Melee.State) || !ReferenceEquals(Airborne.Smash.Melee, Melee.State))
            throw new InvalidOperationException("Walking must use the existing actual player surface/smash state.");
    }

    public void FinishTerrainPhysics(PlayerTerrainStep step)
    {
        Surface.ExteriorCornerDetected = 0;
        lineHasItem = step.Sweep.BodyHit is not null;
        if (step.Sweep.BodyHit is not { } hit) return;
        // DoPhysics copies face/item/position/normal from its local sweep; primary Distance stays intact.
        Surface.LineNormal = NarrowNormal(hit.Contact.Normal);
        lineFaceFlags = hit.Contact.SurfaceFlags;
    }

    public void FinishCrawlPhysics(PlayerCrawlStep step)
    {
        Surface.ExteriorCornerDetected = step.ExteriorCorner ? (byte)1 : (byte)0;
        Surface.FenceDetected = step.Fence ? (byte)1 : (byte)0;
        lineHasItem = step.BodyHit is not null;
        if (step.SourceLineWasQueried)
        {
            // Actual InitLineInfo clears pFace and seeds Distance=INT_MAX; rejected hits still wrote both.
            lineFaceFlags = null; lineDistance = int.MaxValue;
            if (step.SourceLineQueryHit is { } hit)
            {
                Surface.LineNormal = NarrowNormal(hit.Contact.Normal);
                lineFaceFlags = hit.Contact.SurfaceFlags;
                lineDistance = hit.Contact.Distance;
            }
        }
        if (step.RecoveryCornerHit is { } recovered)
        {
            // The source recovery copies only pItem, Position and Normal from its separate ground line.
            lineHasItem = true;
            Surface.LineNormal = NarrowNormal(recovered.Contact.Normal);
        }
        if (!step.CeilingControlsChanged || step.GroundHit is not { } ground) return;
        FixedVector3 sourceLineNormal = Surface.LineNormal;
        Surface.LineNormal = NarrowNormal(ground.Contact.Normal);
        try { PlayerSurfaceEntries.HandleControls(Surface, true); }
        finally { Surface.LineNormal = sourceLineNormal; }
    }

    public void UpdateWalking(ushort completedAnimation) => PlayerWalkingBehavior.Step(Melee.State, completedAnimation, this);
    public void UpdateInterior(ushort completedAnimation) => PlayerSurfaceEntries.UpdateInterior(Surface, completedAnimation, Swing);
    public void UpdateWallCompletion() => PlayerSurfaceEntries.UpdateWallCompletion(Surface, player.Model.Playback.IsFinished, Swing);
    public short GetEffectiveHeading() => player.Controller.Orientation.GetEffectiveHeading(player.Controller.OnWall);
    public void ClearAndSmashHeldObject()
    {
        GodotManipulationActor held = session.HeldActor as GodotManipulationActor ??
            throw new InvalidOperationException("Source walking smash requires the actual held type401 CManipOb.");
        session.Release(held); Melee.Kick.HeldObject = null;
        held.Smash();
    }
    public bool CheckGroundGone() => Melee.CheckGroundGone();
    public bool CheckWebShot() => webs.CheckWebShot();
    public bool CheckJump() => Melee.CheckJump();
    public bool CheckCeilingJumpingSmashPunch() => Airborne.CheckCeilingJumpingSmashPunch();
    public bool CheckKick() => Melee.CheckKick();
    public bool CheckRunIntoWall()
    {
        PlayerCombatState combat = Melee.State.Combat;
        PlayerRunIntoWallContact? contact = null;
        if (!combat.HeldObjectPresent && (combat.CollisionFlags & 1) != 0 && lineHasItem && Surface.LineNormal.Y is >= -2600 and <= 3400)
            contact = new(Surface.LineNormal, RequireFaceFlags());
        return PlayerRunIntoWall.Check(combat, contact, this);
    }
    public bool CheckJumpingSwingWeb() => Swing.CheckJumpingSwingWeb();
    public bool CheckJumpingR1ZipWeb() => Airborne.CheckJumpingR1ZipWeb();
    public bool CheckJumpingR2ZipWeb() => Swing.CheckJumpingR2ZipWeb();
    public bool CheckInteriorSurfaceTransition()
    {
        PlayerSurfaceEntryContact? contact = null;
        if (lineHasItem && Surface.ExteriorCornerDetected == 0 && Melee.State.Combat.IsCrawling && lineDistance >= 0)
            contact = new(Surface.LineNormal, lineDistance, HardSurface ? lineFaceFlags.GetValueOrDefault() : RequireFaceFlags());
        return PlayerSurfaceEntries.CheckInterior(Surface, contact, HardSurface, Swing);
    }
    public bool CheckExteriorSurfaceTransition()
    {
        PlayerSurfaceEntryContact? contact = null;
        if (lineHasItem && Surface.ExteriorCornerDetected != 0 && Melee.State.Combat.IsCrawling)
            contact = new(Surface.LineNormal, lineDistance, HardSurface ? lineFaceFlags.GetValueOrDefault() : RequireFaceFlags());
        return PlayerSurfaceEntries.CheckExterior(Surface, contact, HardSurface, Swing);
    }
    public bool CheckFenceSurfaceTransition() => Swing.CheckFenceSurfaceTransition();
    public void PlayAnimation(ushort clip) => Melee.PlaySingleAnimation(clip, 0, -1);
    public void SelectAndRunAnimation(ushort animation)
    { Melee.SelectAndResetAnimationSounds(animation); Melee.RunAnimation(animation, 0, -1); }
    public void SwitchToStandMode() => Melee.SwitchToStandMode();
    public void LockTargetTorsoAngle() => Melee.LockTargetTorsoAngle();
    private uint RequireFaceFlags() => lineFaceFlags ?? throw new InvalidOperationException(
        "The original primary line has no pFace. Partial crawl recovery must retain that missing face rather than substitute its ground-query face.");
    private static FixedVector3 NarrowNormal(FixedVector3 normal) =>
        new(unchecked((short)normal.X), unchecked((short)normal.Y), unchecked((short)normal.Z));
}
