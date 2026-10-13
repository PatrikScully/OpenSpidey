using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

/// <summary>Actual PriorToVenomDistanceAttack resources: source reticle/additional parts, held401, swing/web/dome and all four smoke owners.</summary>
public sealed class GodotPlayerDistanceAttackPreparation : IPlayerDistanceAttackPreparationHost
{
    private readonly GodotPlayer player;
    private readonly GodotPlayerCombatRuntime combat;
    private readonly GodotPlayerWebRuntime webs;
    private readonly PlayerFollowCamera camera;
    private readonly GodotPlayerAdditionalBodies additional;
    private readonly GodotPlayerHandTrails hands;
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Distance-attack cleanup requires the original player resource owner.");
    public PlayerDistanceAttackPreparationState State { get; }
    public int PreparationCount { get; private set; }

    public GodotPlayerDistanceAttackPreparation(GodotPlayer player, GodotPlayerCombatRuntime combat,
        GodotPlayerWebRuntime webs, PlayerFollowCamera camera, GodotPlayerAdditionalBodies additional,
        GodotPlayerHandTrails hands, PlayerWaterEffectState water)
    {
        ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(combat); ArgumentNullException.ThrowIfNull(webs);
        ArgumentNullException.ThrowIfNull(camera); ArgumentNullException.ThrowIfNull(additional); ArgumentNullException.ThrowIfNull(hands);
        ArgumentNullException.ThrowIfNull(water);
        this.player = player; this.combat = combat; this.webs = webs; this.camera = camera; this.additional = additional; this.hands = hands;
        if (webs.Swing is null || webs.Smash is null || webs.Domes is null || webs.Lookaround is null || webs.Attack is null)
            throw new InvalidOperationException("Distance-attack cleanup requires genuine swing, foot trails, dome and lookaround owners.");
        if (!ReferenceEquals(camera.Target, player) || !ReferenceEquals(hands.Player, player) || !ReferenceEquals(Melee.State.Combat, player.Combat.State))
            throw new InvalidOperationException("Distance-attack cleanup must share the actual player's combat state.");
        State = new(player.Combat.State, webs.Attack!.State, additional.State, water);
    }
    public void Prepare(FixedVector3 direction)
    {
        player.Controller.CaptureCombatState(player.Combat.State.ElapsedTicks);
        PlayerDistanceAttackPreparation.Prepare(State, this, direction);
        player.Controller.ApplyCombatState(); player.RefreshCombatTransform(); PreparationCount++;
    }
    public bool AutoAimReticlePresent => combat.AutoAimReticlePresent;
    public bool HeldObjectPresent => Melee.Kick.HeldObject is not null;
    public bool SwingPresent => webs.Swing!.SwingPresent;
    public bool CurrentWebPresent => webs.Actions.State.CurrentStrand is not null;
    public void DeleteAutoAimReticle() => combat.DeleteAutoAimReticle();
    public void StopSound(uint handle) => player.Combat.StopSound(unchecked((int)handle));
    public object? RecoverAdditionalBody(ref PlayerKickHandle handle) => additional.Recover(ref handle);
    public void DeleteAdditionalBody(object body) => additional.Delete(body as GodotPlayerAdditionalBody ??
        throw new InvalidOperationException("Additional-body cleanup received another source family."));
    public void ClearJumpDurations() => player.Controller.ClearJumpDurations();
    public void DropHeldObject(FixedVector3 velocity) => (Melee.Interactions ??
        throw new InvalidOperationException("Held401 cleanup requires its genuine pickup/drop interaction owner.")).DropHeldObject(velocity);
    public void SwingBackAndDelete() => webs.Swing!.ReleaseCurrent(true);
    public void DeleteCurrentWeb()
    {
        WebStrandOwner strand = webs.Actions.State.CurrentStrand ?? throw new InvalidOperationException("Current-web cleanup requires its sourceE6C owner.");
        strand.Dispose(); webs.Actions.State.CurrentStrand = null;
    }
    public object? RecoverDome(ref PlayerKickHandle handle) => webs.Domes!.Recover(ref handle);
    public void BurstDome(object dome) => webs.Domes!.BurstSourceDome(dome as WebDome ??
        throw new InvalidOperationException("Dome cleanup received another source family."));
    public bool SmokeTrailPresent(int slot) => slot < 2 ? webs.Smash!.CurrentTrails[slot] is not null : hands.Present(slot - 2);
    public void FadeSmokeTrail(int slot)
    {
        if (slot < 2) webs.Smash!.FadeSmokeTrail(slot);
        else hands.Fade(slot - 2);
    }
    public void OrientToNormal(FixedVector3 direction) => Melee.OrientToNormal(true, direction);
    public void ClearHeadingTurn() { Melee.State.HeadingDelta = 0; player.Controller.ClearSourceHeadingTurn(); }
    public void ExitLookaround() => player.Combat.ExitLookaroundMode();
    public void SetCameraFocusHook(int hook) => camera.SetFocusHook(hook);
    public void SetFloorCamera(int duration) => camera.SetFloorCamera(duration);
    public void PutCameraBehind(int duration) => camera.PutBehind(duration);
}
