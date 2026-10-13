using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public interface IPlayerSmashAttackHost
{
    uint Clock { get; }
    bool FirstTrailPresent { get; }
    void CreateSmashAttackTrails();
    void FadeSmashAttackTrails();
    void OrientToNormal(bool targetPresent, FixedVector3 target);
    void PlaySingleAnimation(ushort animation);
    void SwitchToStandMode();
}

/// <summary>Actual source action1000000, from aimed flight through collision recovery and landing animation follow-on.</summary>
public static class PlayerSmashAttackBehavior
{
    public static void Step(PlayerSmashAttackState state, bool animationFinished, IPlayerSmashAttackHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        combat.FieldEA6 = 0;
        if (combat.Field8DC != 0)
        {
            if (combat.AnimationId != 0) return;
            combat.Field8DC = 0; state.FieldAE4 = 1; combat.FieldAE5 = 0;
            host.SwitchToStandMode(); return;
        }
        if (combat.AnimationId is 129 or 133 && animationFinished && !host.FirstTrailPresent) host.CreateSmashAttackTrails();
        if ((combat.LastHitAction & 0x1000000) != 0 && unchecked(host.Clock - combat.LastHitClock) < 6)
        {
            combat.CrawlSurfaceNormal = new(0, -4096, 0);
            host.OrientToNormal(false, default);
            host.PlaySingleAnimation(175); host.FadeSmashAttackTrails();
            combat.Velocity = default; state.FieldE8C = 0; combat.ActionState = 4; return;
        }
        FixedVector3 delta = (state.TargetPosition - combat.Position) >> 12;
        if ((combat.CollisionFlags & 2) == 0 && delta.Y > 0)
        {
            FixedVector3 up = FixedVectorMath.Normalize(new(unchecked(-delta.X), 0, unchecked(-delta.Z)));
            FixedVector3 normal = FixedVectorMath.Normalize(delta);
            combat.CrawlSurfaceNormal = new(unchecked((short)-normal.X), unchecked((short)-normal.Y), unchecked((short)-normal.Z));
            host.OrientToNormal(true, up);
            combat.Velocity = state.Field8D8 != 0 ? new(0, 0x30000, 0) : normal * 96;
            return;
        }
        combat.CrawlSurfaceNormal = new(0, -4096, 0);
        host.OrientToNormal(false, default);
        combat.Velocity = default;
        host.FadeSmashAttackTrails();
        host.PlaySingleAnimation(combat.AnimationId == 129 ? (ushort)130 : (ushort)134);
        if (unchecked(state.EntryClock - state.PreviousEntryClock) < 120) { combat.Field8DC = 666; return; }
        combat.Field8DC = 0; state.FieldAE4 = 1; combat.FieldAE5 = 0;
        host.SwitchToStandMode();
    }
}

public readonly record struct PlayerSmashSphereContact(IPlayerMeleeTarget Target, FixedVector3 Position);
public interface IPlayerSmashContactHost
{
    PlayerSmashSphereContact? TraceBaddies(FixedVector3 start, FixedVector3 end, object? ignoredTarget);
    int GetDamage(int baseDamage);
    void HitTarget(IPlayerMeleeTarget target, ActorHit hit);
    void PlayPositionSound(ushort sound, FixedVector3 position);
}

/// <summary>Actual action1000000 slice of resolveActorContacts after movement, including source type305/315 pass-through.</summary>
public static class PlayerSmashAttackContacts
{
    public static void Resolve(PlayerSmashAttackState state, FixedVector3 previousPosition, IPlayerSmashContactHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        if (previousPosition == combat.Position || (combat.ActionState & 0x1000000) == 0 || state.Field8D8 != 0) return;
        FixedVector3 start = previousPosition with { Y = combat.Position.Y };
        FixedVector3 end = combat.Position + (combat.Position - previousPosition);
        object? ignored = null;
        while (host.TraceBaddies(start, end, ignored) is { } contact)
        {
            int kind = contact.Target.Type - 304;
            if ((uint)kind > 20 || kind is 4 or 5 or 7 or 12 or 14 or 15 or 17 or 18 or 19) return;
            if (kind is 1 or 11) { start = contact.Position; ignored = contact.Target.Identity; continue; }
            FixedVector3 direction = FixedVectorMath.Normalize(new(unchecked(-combat.ForwardDirection.X), 0, unchecked(-combat.ForwardDirection.Z)));
            host.HitTarget(contact.Target, new(30, 0, 2, unchecked((ushort)host.GetDamage(20)), direction, 512, 15));
            host.PlayPositionSound(16, combat.Position);
            combat.Field534 = 360; combat.Field52C = unchecked((combat.Field528 + 11) << 10);
            combat.Velocity = combat.Velocity with { X = 0, Z = 0 };
            state.Field8D8 = 1;
            return;
        }
    }
}
