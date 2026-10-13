using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Source ECC/ED0/ED4 and the two5B8 additional-part handles; normal hand alternatives remain separate model masks.</summary>
public sealed class PlayerAdditionalEffectState
{
    public int SenseTicks { get; set; }
    public int SenseKind { get; set; }
    public uint SenseSoundHandle { get; set; }
    public PlayerKickHandle SenseBody { get; set; }
    public PlayerKickHandle[] BonusFists { get; } = new PlayerKickHandle[2];
}

/// <summary>The actual process-wide gWaterEffect value, shared by sense preparation and post rendering.</summary>
public sealed class PlayerWaterEffectState
{
    public int Value { get; set; }
}

public sealed record PlayerDistanceAttackPreparationState(PlayerCombatState Combat, PlayerWebAttackState WebAttack,
    PlayerAdditionalEffectState AdditionalEffects, PlayerWaterEffectState Water);

public interface IPlayerDistanceAttackPreparationHost
{
    bool AutoAimReticlePresent { get; }
    bool HeldObjectPresent { get; }
    bool SwingPresent { get; }
    bool CurrentWebPresent { get; }
    void DeleteAutoAimReticle();
    void StopSound(uint handle);
    object? RecoverAdditionalBody(ref PlayerKickHandle handle);
    void DeleteAdditionalBody(object body);
    void ClearJumpDurations();
    void DropHeldObject(FixedVector3 velocity);
    void SwingBackAndDelete();
    void DeleteCurrentWeb();
    object? RecoverDome(ref PlayerKickHandle handle);
    void BurstDome(object dome);
    bool SmokeTrailPresent(int slot);
    void FadeSmokeTrail(int slot);
    void OrientToNormal(FixedVector3 direction);
    void ClearHeadingTurn();
    void ExitLookaround();
    void SetCameraFocusHook(int hook);
    void SetFloorCamera(int duration);
    void PutCameraBehind(int duration);
}

/// <summary>Complete source4B9420 cleanup order and exact recovered-handle clearing before a Venom distance attack or checkpoint cutscene skip.</summary>
public static class PlayerDistanceAttackPreparation
{
    public static void Prepare(PlayerDistanceAttackPreparationState state, IPlayerDistanceAttackPreparationHost host,
        FixedVector3 direction)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(host);
        PlayerCombatState combat = state.Combat;
        PlayerAdditionalEffectState effects = state.AdditionalEffects;
        combat.ArenaConstraintActive = 0;
        if (host.AutoAimReticlePresent) host.DeleteAutoAimReticle();

        uint sound = effects.SenseSoundHandle;
        effects.SenseTicks = 0;
        state.Water.Value = 0;
        if (sound != 0) { host.StopSound(sound); effects.SenseSoundHandle = 0; }
        PlayerKickHandle handle = effects.SenseBody;
        object? senseBody = host.RecoverAdditionalBody(ref handle);
        effects.SenseBody = handle;
        if (senseBody is not null) { host.DeleteAdditionalBody(senseBody); effects.SenseBody = default; }

        host.ClearJumpDurations();
        if (host.HeldObjectPresent)
            host.DropHeldObject(combat.ForwardDirection * -8 + combat.UpDirection * 4);
        if (host.SwingPresent) host.SwingBackAndDelete();
        if (host.CurrentWebPresent) host.DeleteCurrentWeb();

        for (int slot = 0; slot < effects.BonusFists.Length; slot++)
        {
            handle = effects.BonusFists[slot];
            object? fist = host.RecoverAdditionalBody(ref handle);
            effects.BonusFists[slot] = handle;
            if (fist is null) continue;
            host.DeleteAdditionalBody(fist);
            effects.BonusFists[slot] = handle with { Target = null };
        }
        combat.Field5B0 = combat.Field5AC = 0;

        handle = state.WebAttack.DomeHandle;
        object? dome = host.RecoverDome(ref handle);
        state.WebAttack.DomeHandle = handle;
        if (dome is not null) { host.BurstDome(dome); state.WebAttack.DomeHandle = default; }
        for (int slot = 0; slot < 4; slot++)
            if (host.SmokeTrailPresent(slot)) host.FadeSmokeTrail(slot);

        combat.Field54C = 0;
        combat.IsCrawling = false;
        combat.Field8E9 = combat.Field8E8 = 0;
        combat.CrawlSurfaceNormal = new(0, -4096, 0);
        host.OrientToNormal(direction);
        bool lookaround = combat.LookaroundActive;
        combat.Velocity = default;
        host.ClearHeadingTurn();
        if (lookaround) host.ExitLookaround();
        host.SetCameraFocusHook(-1);
        host.SetFloorCamera(0);
        host.PutCameraBehind(0);
    }
}
