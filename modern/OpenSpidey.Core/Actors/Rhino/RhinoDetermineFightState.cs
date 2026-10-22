using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Rhino;

public sealed partial class RhinoBrain
{
    /// <summary>CRhino::DetermineFightState (0x481300). Public for original-exe fixture replay.</summary>
    public bool DetermineFightState(int argument)
    {
        int original = state.State;
        if (Abs(unchecked(host.Body.Position.Y - host.PlayerPosition.Y)) > 0x64000)
        {
            if (state.Field324 == 0) state.Field324 = Difficulty(400, 200, 120, 120);
            return false;
        }

        if (!LineOfSightCheck(host.PlayerPosition, 1))
        {
            if (state.Field1F0 != 0) return false;
            FixedVector3 point = ActorPathController.GetWaypointNearTarget(host.PlayerPosition, 0x12C000, state.Field21D);
            state.Field21D = unchecked((byte)(state.Field21D + 1));
            if (unchecked((uint)ThugPerception.ApproximateHorizontalDistance(host.Body.Position, point)) < 0x104) return false;
            if (host.PathCheck(host.Body.Position, point, false, 100).Code != 0) return false;
            state.Field1F0 = 1;
            SetState(1);
            state.PathPoints[1] = point;
            state.CombatFlags |= 0x10;
            return true;
        }

        if ((state.CombatFlags & 0x10) != 0)
        {
            PlayXAPlease(0x12, 1, 1);
            state.CombatFlags &= ~0x10u;
        }

        if (unchecked(host.AttackClock - state.Field358) < 150 && host.PlayerIsCrawling) SetState(0xD);
        else
        {
            uint distance = host.PlayerDistance;
            if (distance < 0x1388)
            {
                int action = host.PlayerActionState;
                if ((action & 0x80) != 0) return false;
                if ((action & 0x800000) != 0)
                {
                    if (distance < 0xC8) return false;
                    state.State = 5;
                }
                else if (distance < 0xC8) state.State = 6;
                else if (distance < 0x1F4) state.State = 5;
                else state.State = argument != 0 ? 8 : 7;
                state.Substate = 0;
            }
        }

        if (original == state.State) return false;
        host.SendSignal();
        return true;
    }

    /// <summary>CRhino::LineOfSightCheck (0x4823C0): two rays offset 32 units either side, passing through chunk0 items.</summary>
    public bool LineOfSightCheck(FixedVector3 target, int passThroughChunks)
    {
        FixedVector3 direction = FixedVectorMath.Normalize((target - host.Body.Position) >> 12);
        bool blocked = false;
        for (int pass = 0; pass < 2; pass++)
        {
            FixedVector3 offset = pass == 0
                ? new(unchecked(32 * direction.Z), 0, unchecked(-32 * direction.X))
                : new(unchecked(-32 * direction.Z), 0, unchecked(32 * direction.X));
            FixedVector3 start = host.Body.Position + offset;
            FixedVector3 end = target + offset;
            while (true)
            {
                blocked = host.TryLineToItem(start, end, out uint checksum, out FixedVector3 hit);
                if (!blocked) break;
                if (passThroughChunks == 0 || !Contains(host.WallChunks.Chunk0, checksum)) return false;
                start = hit + direction * 32;
            }
        }
        return !blocked;
    }

    /// <summary>CBaddy::DistanceToPlayer (0x404170): cached within one attack-clock tick when the bound is 0.</summary>
    private int DistanceToPlayer(int maximumAge)
    {
        if (state.CachedPlayerDistanceClock != 0 && unchecked(host.AttackClock - state.CachedPlayerDistanceClock) <= maximumAge)
            return state.CachedPlayerDistance;
        state.CachedPlayerDistanceClock = host.AttackClock;
        state.CachedPlayerDistance = ThugPerception.ApproximateHorizontalDistance(host.Body.Position, host.PlayerPosition);
        return state.CachedPlayerDistance;
    }

    /// <summary>CRhino::Hit (0x47EE70); returns the original's non-zero "hit taken" result.</summary>
    public int Hit(ActorHit hit)
    {
        if (state.Health <= 0) return 0;
        state.Field3D0 = 30;
        state.Field35C = 0;
        int kind = (hit.Flags & 2) != 0 ? hit.AttackKind : 0;
        state.LastHitKind = kind;
        ushort damage = hit.Damage;
        if (kind is 23 or 6) damage = unchecked((ushort)((2000 * damage) >> 12));
        else if (state.State == 12)
        {
            FixedVector3 direction = FixedVectorMath.Normalize((host.PlayerPosition - host.Body.Position) >> 12);
            host.HitPlayer(new ActorHit(14, 0, 26, 15, direction));
            return 0;
        }

        state.Health = unchecked((short)(state.Health - ((hit.Flags & 4) != 0 ? damage : 0)));
        if (state.Health <= 0)
        {
            SetState(21);
            return 1;
        }

        host.PlayPositionalSound(((host.AttackClock & 1) == 0 ? 1 : 0) | 0x8044);
        if (state.State == 10)
        {
            if (state.Substate == 2 && state.Field230 > 20)
            {
                PlaySingleAnim(23);
                state.Substate = 2;
                PlayXAPlease(20, 2, 2);
                state.CombatFlags |= 8;
            }
        }
        else if ((flags.Check(state.State) & 8) == 0)
        {
            SetState(14);
            host.Neutralize();
            if ((hit.Flags & 0x18) != 0)
            {
                short distance = hit.SlideDistance;
                if (distance <= 128) SlideFromHit(distance, hit.SlideFrames, hit.Direction);
                else SlideFromHit((2252 * distance) >> 12, ((2252 * hit.SlideFrames) >> 12) + 1, hit.Direction);
            }
            else if (state.LastHitKind is 2 or 6 or 23)
            {
                FixedVector3 away = host.Body.Position - host.PlayerPosition;
                FixedVector3 flat = new FixedVector3(away.X, 0, away.Z) >> 12;
                SlideFromHit(5, 5, FixedVectorMath.Normalize(flat));
            }
        }
        return 1;
    }

    /// <summary>CRhino::SlideFromHit (0x47F190): launches state 15 along the clear part of the slide.</summary>
    private void SlideFromHit(int distance, int frames, FixedVector3 direction)
    {
        FixedVector3 offset = direction * distance;
        FixedVector3 target = host.Body.Position + offset;
        ActorPathCheckResult path = host.PathCheck(host.Body.Position, target, true, 55);
        if (path.Code == 0)
        {
            state.Field1F8 = frames;
            host.Body.Velocity = offset / frames;
            SetState(15);
            return;
        }
        if (path.Code != 2 || (direction.X | direction.Z) == 0) return;
        FixedVector3 travel = path.HitPoint - host.Body.Position;
        int steps = Abs(direction.X) <= Abs(direction.Z)
            ? unchecked(frames * travel.Z) / offset.Z
            : unchecked(frames * travel.X) / offset.X;
        state.Field1F8 = steps;
        if (steps <= 1) return;
        host.Body.Velocity = travel / (steps + 1);
        SetState(15);
    }
}
