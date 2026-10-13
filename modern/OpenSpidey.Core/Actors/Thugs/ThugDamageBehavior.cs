using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public interface IThugDamageHost : IThugAttackHost
{
    int PlayerAnimationId { get; }
    ThugState? FindClosest(int actorType);
    void SendMessage(ThugState recipient, int code);
    void SendDeathPulse();
    void ClearAttackOwnership();
    int PathCheckWithHit(FixedVector3 from, FixedVector3 to, int clearance, out FixedVector3 hit);
}

/// <summary>Recovered enemy damage, directional reactions, recovery and hit-driven sliding.</summary>
public static class ThugDamageBehavior
{
    public static bool Hit(ThugState state, ActorProcedureState actor, ActorProcedureList procedures,
        ThugStateFlags flags, IThugDamageHost host, ActorHit hit, Func<int, int> random)
    {
        if (state.State == 26)
        {
            if (state.Substate == 8)
            {
                PainSound();
                host.PlaySound(0x8019);
            }
            return false;
        }
        if ((hit.Flags & 4) != 0) state.Health = unchecked((short)(state.Health - hit.Damage));
        if (state.Health <= 0) host.SendDeathPulse();
        int attack = (hit.Flags & 2) != 0 ? hit.AttackKind : 0;
        state.AlertTicks = 600;
        if ((hit.Flags & 4) == 0 || state.Health > 0 || (hit.Flags & 8) != 0) PainSound();
        else if (state.State != 26)
        {
            host.PlaySound(0x8019);
            host.PlaySound(unchecked((ushort)((random(3) + 21) | 0x8000)));
        }
        if (attack == 3 && state.Health > 0)
        {
            host.SendMessage(state, 20);
            host.CycleAnimation(state.Type == 304 ? (ushort)38 : (ushort)33);
            host.PlaySound(0x801C);
            return false;
        }
        if (attack == 29 && state.State == 20 && state.Health > 0) return false;
        ThugAnimationBehavior.SetMode(state, 3, false, host);
        state.ReactionContext = attack;
        ThugState? buddy = host.FindClosest(304) ?? host.FindClosest(312);
        if (buddy is not null && buddy.AlertTicks == 0) host.SendMessage(buddy, 7);
        state.CombatFlags &= ~0x30000u;
        if ((flags.Check(state.State) & 0x10) != 0 || (state.Health > 0 && (state.ActionFlags & 1) != 0)) return false;
        host.Neutralize();
        state.State = state.Health > 0 ? 12 : 26;
        state.Substate = 0;
        state.CombatFlags &= ~0x30000u;
        if ((hit.Flags & 8) != 0) SetHitDirection(state, actor, hit.Direction);
        if ((hit.Flags & 0x18) != 0)
        {
            SlideFromHit(state, actor, procedures, host, hit.SlideDistance, hit.SlideFrames, hit.Direction, random);
            host.RunAnimation(state.Type == 304 ? (ushort)24 : (ushort)14);
        }
        return true;

        void PainSound() => host.PlaySound(unchecked((ushort)((random(3) + 25) | 0x8000)));
    }

    public static void SetHitDirection(ThugState state, ActorProcedureState actor, FixedVector3 direction)
    {
        int yaw = FixedAim.Calculate(actor.Body.Position + direction * 300, actor.Body.Position).Yaw;
        int delta = NormalizeDelta(yaw - actor.Body.Angles.Y);
        if (delta < -455) state.CombatFlags |= 0x20000;
        else if (delta > 455) state.CombatFlags |= 0x10000;
    }

    public static void TakeHit(ThugState state, ActorProcedureState actor, ActorProcedureList procedures, IThugDamageHost host)
    {
        switch (state.Substate)
        {
            case 0:
                state.BodyFlags &= 0xFFEF;
                state.TurnRate = 0;
                host.ClearAttackOwnership();
                if (state.ReactionContext == 1)
                {
                    state.MinorRadius = 0;
                    host.RunAnimation(state.Type == 304 ? (ushort)14 : (ushort)19);
                    state.Substate = 2;
                }
                else
                {
                    procedures.Attach(ActorLookAtProcedure.ForBody(actor, host.RecoverPlayerBody, 0, 80, 200));
                    RunHitAnimation(state, host);
                    state.Substate = 3;
                }
                break;
            case 1:
                if (host.AnimationFinished != 0)
                {
                    state.ReactionContext = 0;
                    state.State = 28;
                    state.Substate = 0;
                }
                break;
            case 2:
                if (host.AnimationFinished != 0)
                {
                    state.MinorRadius = state.Type == 304 ? 96 : 150;
                    host.RunAnimation(state.Type == 304 ? (ushort)15 : (ushort)20);
                    state.Substate = 1;
                }
                break;
            case 3:
                if (host.AnimationFinished != 0)
                {
                    state.State = 28;
                    state.Substate = 0;
                }
                break;
            default: throw new InvalidOperationException($"Unknown thug damage-recovery substate {state.Substate}.");
        }
    }

    public static void RunHitAnimation(ThugState state, IThugGuardHost host)
    {
        ushort clip;
        if ((state.CombatFlags & 0x10000) != 0) clip = state.Type == 304 ? (ushort)43 : (ushort)38;
        else if ((state.CombatFlags & 0x20000) != 0) clip = state.Type == 304 ? (ushort)42 : (ushort)37;
        else clip = state.Type == 304 ? (ushort)25 : (ushort)18;
        host.RunAnimation(clip);
    }

    public static void SlideFromHit(ThugState state, ActorProcedureState actor, ActorProcedureList procedures,
        IThugDamageHost host, int distance, int frames, FixedVector3 direction, Func<int, int> random)
    {
        // The source diagnoses zero frames but then divides by zero. Keep the hit reaction without an invalid slide.
        if (frames == 0) return;
        actor.BehaviorFlags &= ~0x10u;
        state.ActionCounter = frames;
        FixedVector3 velocity = new(unchecked(direction.X * distance), 0, unchecked(direction.Z * distance));
        FixedVector3 position = actor.Body.Position;
        FixedVector3 start = position with { Y = unchecked(position.Y + ((state.SurfaceClearance - 20) << 12)) };
        FixedVector3 target = position + velocity;
        velocity /= frames;
        int path = host.PathCheckWithHit(start, target, 55, out FixedVector3 hit);
        if (path == 2)
        {
            int x = ThugPerception.SourceAbs(unchecked(hit.X - start.X));
            int z = ThugPerception.SourceAbs(unchecked(hit.Z - start.Z));
            state.ActionCounter = x > z ? x / ThugPerception.SourceAbs(velocity.X) : z / ThugPerception.SourceAbs(velocity.Z);
            if (state.ActionCounter < 4) return;
            target = position + velocity * state.ActionCounter;
        }
        else if (path == 4) return;
        if (host.AddPointToPath(position, 0) && host.AddPointToPath(target, 0)) actor.BehaviorFlags &= ~0x10000000u;
        else state.Health = 0;
        host.Neutralize();
        if (state.Health <= 0 && state.ActionCounter >= 8)
        {
            if ((state.CombatFlags & 0x30000) != 0)
            {
                state.CombatFlags |= 0x40000;
                int angle = NormalizeDelta(FixedAim.Calculate(target, position).Yaw - actor.Body.Angles.Y);
                state.SlidePitchRate = 0;
                state.SlideYawRate = (state.CombatFlags & 0x10000) != 0 ?
                    unchecked(angle - 4096) / state.ActionCounter : unchecked(angle + 4096) / state.ActionCounter;
            }
            else if (host.PlayerAnimationId is 106 or 113 or 284)
            {
                state.CombatFlags |= 0x40000;
                state.ActionCounter = unchecked(state.ActionCounter * 2);
                velocity /= 2;
                state.SlidePitchRate = 4096 / state.ActionCounter;
                state.SlideYawRate = 0;
                FixedVector3 look;
                if (random(2) != 0)
                {
                    state.SlidePitchRate = unchecked(-state.SlidePitchRate);
                    look = position - velocity * unchecked(2 * state.ActionCounter);
                }
                else look = position + velocity * unchecked(2 * state.ActionCounter);
                procedures.Attach(ActorLookAtProcedure.ForPoint(actor, look, 0, 80, 200));
            }
        }
        state.State = 17;
        state.Substate = 0;
        actor.Body.Velocity = velocity;
    }

    private static int NormalizeDelta(int delta)
    {
        if (delta < -2048) return delta + 4096;
        if (delta > 2048) return delta - 4096;
        return delta;
    }
}
