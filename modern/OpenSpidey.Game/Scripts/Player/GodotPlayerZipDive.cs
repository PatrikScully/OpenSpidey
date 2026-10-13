using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

/// <summary>Actual weak baddy handles, Grab/Hit slots, player movement, source animations and animated attachment hooks.</summary>
public sealed class GodotPlayerZipDive(GodotPlayer player, GodotPlayerMelee melee, IGodotPlayerMeleeServices services) : IPlayerZipDiveHost, IPlayerGrabHost
{
    public uint Clock => services.Clock;
    public int Difficulty => services.Difficulty;
    public void Update(ushort completedAnimation)
    {
        bool kick = melee.State.Combat.ActionState == 0x08000000;
        PlayerZipDive.Update(melee.State, completedAnimation, this);
        if (kick) player.Controller.SetZipDiveAirControlSuppression(melee.State.ZipDiveAirControlSuppression);
    }
    public PlayerZipDiveTarget? RecoverTarget(ref PlayerKickHandle handle) => services.RecoverZipDiveTarget(ref handle);
    public bool GrabTarget(PlayerZipDiveTarget target, FixedVector3 destination) => services.GrabZipDiveTarget(target, destination);
    public void ClearGrabFlag(PlayerZipDiveTarget target) => services.ClearZipDiveGrabFlag(target);
    public bool CheckJump() => melee.CheckJump();
    public void CheckForwards() => player.Controller.ZipDiveCheckForwards(player.CameraHeading?.Invoke() ?? player.Controller.Orientation.Heading, melee.Kick.SurfaceTransition);
    public void LockTargetTorsoAngle() => melee.LockTargetTorsoAngle();
    public void SwitchToStandMode() => melee.SwitchToStandMode();
    public void PlayAnimation(ushort animation) => melee.PlaySingleAnimation(animation, 0, -1);
    public int GetDamage(int damage) => melee.Controller.GetDamage(damage);
    public void HitTarget(PlayerZipDiveTarget target, ActorHit hit) => services.HitZipDiveTarget(target, hit);
    public void CreateCombatImpact(FixedVector3 position, int kind) => services.CreateCombatImpactEffect(position, kind);
    public void PlayPositionalSound(ushort sound, FixedVector3 position, int pitch) => melee.PlayPositionSound(sound, position, pitch);

    public bool GrabUpdate(ref FixedVector3 outputPosition, ref short outputYaw)
    {
        var combat = melee.State.Combat;
        var state = new PlayerGrabState { ActionState = combat.ActionState, TargetHandle = melee.State.ZipDiveHandle,
            Position = combat.Position, ForwardDirection = combat.ForwardDirection, UpDirection = combat.UpDirection, Yaw = player.Controller.Orientation.Heading };
        bool updated = PlayerGrabUpdate.Update(state, this, ref outputPosition, ref outputYaw);
        combat.Position = state.Position; player.Controller.SetScriptPosition(state.Position);
        // Source AI changes live mPos here; the already-built source pose/cache remains intact until the next player phase.
        return updated;
    }
    PlayerGrabTarget? IPlayerGrabHost.RecoverTarget(object? handle)
    {
        if (handle is not PlayerKickHandle typed) throw new InvalidDataException("GrabUpdate requires the actual typed ZipDive handle.");
        PlayerZipDiveTarget? target = services.RecoverZipDiveTarget(ref typed);
        melee.State.ZipDiveHandle = typed;
        return target is null ? null : new(target.Identity, target.Type);
    }
    public FixedVector3 GetDynamicHookPosition(PlayerGrabTarget target, ActorHook hook) =>
        services.GetZipDiveTargetHook(new(target.Identity, default, target.Type, 0), hook);
    public void ReportUnknownTarget(ushort type) => throw new NotSupportedException($"Original player GrabUpdate has no attachment mapping for target type{type}.");
}
