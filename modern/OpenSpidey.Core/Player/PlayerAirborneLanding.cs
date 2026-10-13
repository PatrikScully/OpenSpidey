using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public interface IPlayerAirborneLandingHost
{
    bool UseLevelSoundAliases { get; }
    bool VibrationEnabled { get; }
    int Random(int bound);
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitch);
    void HitPlayer(ActorHit hit);
    void ActuatorOn(int controller, ushort time, int argument, int strength);
    void PlayAnimation(ushort animation);
}

/// <summary>Actual CheckLanded4C24E0: original fall damage, source34C sound aliases, actuator and held-movement landing clips.</summary>
public static class PlayerAirborneLanding
{
    public static bool Check(PlayerAirborneMovementState state, IPlayerAirborneLandingHost host)
    {
        PlayerMeleeState melee = state.Melee;
        PlayerCombatState combat = melee.Combat;
        if ((combat.CollisionFlags & 2) == 0) return false;
        ushort sound = host.UseLevelSoundAliases ? unchecked((ushort)((host.Random(4) + 80) | 0x8000)) : (ushort)9;
        host.PlayPositionSound(sound, combat.Position, 0);
        bool hurt = false;
        int from = state.FallDamageStartsAt;
        if (from != 0)
        {
            int drop = unchecked(combat.Position.Y - state.FallReferenceHeight) >> 12;
            if (drop > from)
            {
                ushort damage = unchecked((ushort)(unchecked((drop - from) * combat.MaxHealth) / unchecked(state.FallDamageFullHealthAt - from)));
                host.HitPlayer(new(4, 0, 0, damage, default, 0, 0));
                if (combat.Health <= 0) return true;
                hurt = true;
            }
        }
        if (!hurt && host.VibrationEnabled) host.ActuatorOn(0, 4, 0, 1);
        bool stationary = melee.Input.Sideways == 0 && melee.Input.Forward == 0;
        ushort animation;
        if (combat.AnimationId == 232)
        {
            animation = stationary ? (ushort)237 : (ushort)236;
            if (stationary) combat.FieldE8C = 0;
        }
        else if (combat.AnimationId is 175 or 176) animation = 178;
        else if (combat.AnimationId == 225 || combat.FieldE8C != 0 && combat.AnimationId is 226 or 228 or 233 or 235)
        {
            animation = stationary ? (ushort)230 : (ushort)229;
            if (stationary) combat.FieldE8C = 0;
        }
        else { animation = 213; combat.FieldE8C = 0; }
        host.PlayAnimation(animation);
        combat.ActionState = 8;
        combat.FieldAE5 = 0;
        combat.PendingWebReleaseAnimation = 0;
        melee.Input.Mirrors[16] = melee.Input.Triggered[16] = 0;
        return true;
    }
}
