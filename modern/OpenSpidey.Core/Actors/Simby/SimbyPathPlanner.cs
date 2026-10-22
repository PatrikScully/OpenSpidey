using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Simby;

/// <summary>CSimby target planning: GetToTarget (0x4A9F00), SetUpJumpPath (0x4A9A10), RegisterTargetPos (0x4A2680),
/// the attack-slot pick (0x4A94B0) and AttemptAttackMethod (0x4A9620).</summary>
public sealed class SimbyPathPlanner(SimbyState state, SimbyWorldState world, ISimbyHost host)
{
    /// <summary>CBaddy::DistanceToPlayer (0x404170) cached in field_204 / field_208.</summary>
    public int DistanceToPlayer(int tolerance)
    {
        if (state.DistanceClock != 0 && unchecked(host.AttackClock - state.DistanceClock) <= tolerance)
            return state.CachedPlayerDistance;
        state.DistanceClock = host.AttackClock;
        state.CachedPlayerDistance = CrapXZDistance(host.Body.Position, host.PlayerPosition);
        return state.CachedPlayerDistance;
    }

    public int GetToTarget(int mode)
    {
        FixedVector3 position = host.Body.Position;
        switch (mode)
        {
            case 0:
                state.TargetPosition = position + state.UnitDirection * 1000;
                return SetUpJumpPath();
            case 1:
                return GetToTargetAhead(position);
            case 2:
                return GetToTargetAbove(position);
            default:
                throw new InvalidDataException($"CSimby::GetToTarget mode {mode} hits the original diagnostic.");
        }
    }

    private int GetToTargetAhead(FixedVector3 position)
    {
        state.TargetPosition = position + state.UnitDirection * 1200;
        FixedVector3 start = position;
        if ((state.BehaviorFlags & 0x200000) == 0)
        {
            int lift = unchecked((state.SurfaceClearance - 50) << 12);
            start = start with { Y = unchecked(start.Y + lift) };
            state.TargetPosition = state.TargetPosition with { Y = unchecked(state.TargetPosition.Y + lift) };
        }
        if (Absolute(unchecked(position.Y - state.TargetPosition.Y)) > 614400) return 0;
        ActorPathCheckResult path = host.PathCheck(start, state.TargetPosition, true, 50, default);
        if (path.Code == 0) return 1;
        FixedVector3 hit = path.HitPoint;
        if (path.Code != 2 || unchecked((uint)CrapXZDistance(position, hit)) <= 0x118
            || (state.BehaviorFlags & 0x200000) == 0 && host.PathCheck(position, hit, false, 50, default).Code != 0)
            return 0;
        state.BehaviorFlags |= 1;
        state.TargetPosition = hit - state.UnitDirection * 140;
        // word_6B4C50 is the normal the last blocked Utils_LineOfSight left in the shared SLineInfo.
        FixedVector3 away = position - (host.LastLineHitNormal << 14);
        AimAngles aim = FixedAim.Calculate(position, away);
        state.AimYaw = (aim.Yaw + host.Body.Angles.Y) >> 1;
        return 1;
    }

    private int GetToTargetAbove(FixedVector3 position)
    {
        if ((state.BehaviorFlags & 0x400000) != 0) return 0;
        FixedVector3 up = position with { Y = unchecked(position.Y - 0x5DC000) };
        state.CombatFlags |= 0x20000;
        ActorPathCheckResult climb = host.PathCheck(position, up, true, 55, state.ClimbTarget);
        state.ClimbTarget = climb.HitPoint;
        state.CombatFlags &= ~0x20000u;
        if (climb.Code != 2) return 0;
        if (unchecked(position.Y - state.ClimbTarget.Y) <= 3072000) return 0;
        FixedVector3 ceiling = state.ClimbTarget with { Y = unchecked(state.ClimbTarget.Y - (state.MinorRadius << 12)) };
        if (host.LineToItem(state.ClimbTarget, ceiling, out int hitY))
            state.ClimbTarget = state.ClimbTarget with { Y = unchecked(hitY + 20480) };
        state.CombatFlags |= 0x20000;
        state.TargetPosition = (position + state.UnitDirection * 1500) with { Y = state.ClimbTarget.Y };
        ActorPathCheckResult across = host.PathCheck(state.ClimbTarget, state.TargetPosition, true, 55, default);
        state.CombatFlags &= ~0x20000u;
        if (across.Code == 0) return RegisterTargetPos(state.TargetPosition) ? 1 : 0;
        if (across.Code != 2) return 0;
        FixedVector3 hit = across.HitPoint - state.UnitDirection * state.MinorRadius;
        if (unchecked((uint)CrapXZDistance(position, hit)) <= 0xFA) return 0;
        if (!RegisterTargetPos(hit)) return 0;
        int rise = unchecked(position.Y - state.TargetPosition.Y) >> 12;
        state.JumpSteps = rise > 50 ? rise / 50 : 1;
        if (state.JumpSteps < 14) state.JumpSteps = 14;
        state.JumpStep = 4096 / state.JumpSteps;
        state.TargetPosition = hit;
        return 1;
    }

    /// <summary>sub_4A9A10: arc over to TargetPosition, settling it on flat ground and claiming a target slot.</summary>
    public int SetUpJumpPath()
    {
        if ((state.BehaviorFlags & 0x400000) != 0 || host.HasSupportBody) return 0;
        FixedVector3 position = host.Body.Position;
        if (ActorLifecycle.Distance(position, state.TargetPosition) < 0xC8) return 0;
        if (state.Stage != 2 && Absolute(unchecked(position.Y - host.PlayerPosition.Y)) < 409600 && DistanceToPlayer(3) < 500)
            return 0;
        int ground = host.GroundHeight(state.TargetPosition, 280, 280, 0);
        if (ground == -1) return 0;
        state.TargetPosition = state.TargetPosition with { Y = unchecked(ground - (state.MinorRadius >> 1)) };
        FixedVector3 middle = (position >> 1) + (state.TargetPosition >> 1);
        middle = middle with { Y = unchecked(middle.Y - 0x118000) };
        state.JumpHeight = 280;
        if (!host.LineOfSight(position, middle, out FixedVector3 roof))
        {
            state.JumpHeight = (unchecked(position.Y - roof.Y) >> 12) - 50;
            if (state.JumpHeight < 800) return 0;
            middle = middle with { Y = unchecked(roof.Y + 204800) };
        }
        state.CombatFlags |= 0x20000;
        FixedVector3 raised = position with { Y = unchecked(position.Y - 0x30000) };
        // Object-collision rejection returns with field_2A8 bit 0x20000 still set, like the original.
        if (state.Stage != 2 && (host.CheckObjectCollision(position, middle) || host.CheckObjectCollision(middle, state.TargetPosition)))
            return 0;
        int first = host.PathCheck(raised, middle, false, 55, default).Code;
        ActorPathCheckResult second = first != 0 ? default : host.PathCheck(middle, state.TargetPosition, true, 55, default);
        state.CombatFlags &= ~0x20000u;
        if (first != 0) return 0;
        if (second.Code == 0)
        {
            int landing = host.GroundHeightWithSupport(state.TargetPosition, 280, 280, out bool support);
            if (landing == -1 || !FlatAroundTarget(landing) || support || !RegisterTargetPos(state.TargetPosition)) return 0;
            state.TargetPosition = state.TargetPosition with { Y = landing };
            return FinishJump();
        }
        if (second.Code != 2) return 0;
        FixedVector3 hit = second.HitPoint;
        if (unchecked((uint)CrapXZDistance(hit, state.TargetPosition)) < 0x1E)
            hit -= state.UnitDirection * (state.MinorRadius >> 1);
        int hitGround = host.GroundHeight(hit, 280, 280, 0);
        if (hitGround == -1 || !FlatAroundTarget(hitGround)) return 0;
        state.TargetPosition = hit;
        if (!RegisterTargetPos(state.TargetPosition)) return 0;
        state.TargetPosition = state.TargetPosition with { Y = hitGround };
        return FinishJump();
    }

    /// <summary>Probes (x+1, z+1) and (x-1, z+1) around TargetPosition within 0x2000 of the landing height.</summary>
    private bool FlatAroundTarget(int landing)
    {
        FixedVector3 probe = new(unchecked(state.TargetPosition.X + 4096), state.TargetPosition.Y,
            unchecked(state.TargetPosition.Z + 4096));
        if (Absolute(unchecked(host.GroundHeight(probe, 280, 280, 0) - landing)) > 0x2000) return false;
        probe = probe with { X = unchecked(probe.X - 0x2000) };
        return Absolute(unchecked(host.GroundHeight(probe, 280, 280, 0) - landing)) <= 0x2000;
    }

    private int FinishJump()
    {
        int distance = CrapXZDistance(host.Body.Position, state.TargetPosition);
        state.JumpSteps = distance > 24 ? distance / 24 : 1;
        if (state.JumpSteps < 14) state.JumpSteps = 14;
        state.JumpStep = 4096 / state.JumpSteps;
        return 1;
    }

    /// <summary>sub_4A2680: rejects targets within MinorRadius (X and Z) of another live Simby's slot.</summary>
    public bool RegisterTargetPos(FixedVector3 target)
    {
        for (int slot = 0; slot < world.TargetSlots.Length; slot++)
        {
            if ((world.TargetSlotMask & (1u << slot)) == 0 || state.TargetSlot == slot + 1)
            {
                if (state.TargetSlot == 0) state.TargetSlot = slot + 1;
                continue;
            }
            FixedVector3 other = world.TargetSlots[slot];
            int radius = state.MinorRadius;
            if (Absolute(unchecked(target.X - other.X)) >> 12 < radius && Absolute(unchecked(target.Z - other.Z)) >> 12 < radius)
                return false;
        }
        if (state.TargetSlot == 0) world.OverflowTargetSlot = target;
        else world.TargetSlots[state.TargetSlot - 1] = target;
        world.TargetSlotMask |= 1u << ((state.TargetSlot - 1) & 31);
        return true;
    }

    /// <summary>sub_4A94B0: releases the old attack bit and claims the free slot (heading 1365 * i) nearest the player aim.</summary>
    public bool PickAttackSlot()
    {
        world.AttackData &= ~state.AttackMask;
        state.AttackMask = 0;
        if (host.PlayerSpecialState != 0 || (world.AttackData & 7) == 7 || host.PlayerHealth <= 0) return false;
        int toPlayer = FixedAim.Calculate(host.Body.Position, host.PlayerPosition).Yaw;
        // The original keeps the winning heading, not its delta, as the next bound.
        int best = 5000, chosen = -1;
        for (int slot = 0; slot < 3; slot++)
        {
            if ((world.AttackData & (1 << slot)) != 0) continue;
            int delta = 1365 * slot - toPlayer;
            if (delta < -2048) delta += 4096;
            else if (delta > 2048) delta -= 4096;
            int magnitude = Absolute(delta);
            if (magnitude >= best) continue;
            state.AttackAngle = 1365 * slot;
            best = 1365 * slot;
            chosen = slot;
            if (magnitude < 682) break;
        }
        if (chosen == -1) return false;
        state.AttackMask |= 1 << chosen;
        world.AttackData |= 1 << chosen;
        state.AttackUnit = FixedAim.Velocity(1, new(0, unchecked((short)state.AttackAngle)));
        return true;
    }

    /// <summary>CSimby_AttemptAttackMethod (0x4A9620): mode 0 slides in on the ground, mode 1 stretches over.</summary>
    public int AttemptAttackMethod(int mode)
    {
        if (mode == 1)
        {
            state.TargetPosition = host.PlayerPosition - state.AttackUnit * (350 - state.MinorRadius);
            return SetUpJumpPath();
        }
        if (mode != 0) throw new InvalidDataException($"CSimby attack method {mode} hits the original diagnostic.");
        FixedVector3 position = host.Body.Position;
        state.TargetPosition = host.PlayerPosition - state.AttackUnit * 250;
        FixedVector3 start = position;
        if ((state.BehaviorFlags & 0x200000) == 0)
        {
            int lift = unchecked((state.SurfaceClearance - 50) << 12);
            start = start with { Y = unchecked(start.Y + lift) };
            state.TargetPosition = state.TargetPosition with { Y = unchecked(state.TargetPosition.Y + lift) };
        }
        if (!RegisterTargetPos(state.TargetPosition)) return 0;
        ActorPathCheckResult path = host.PathCheck(start, state.TargetPosition, true, 50, default);
        if (path.Code == 0) return 1;
        if (path.Code != 2 || unchecked((uint)CrapXZDistance(host.PlayerPosition, path.HitPoint)) <= 0x96
            || (state.BehaviorFlags & 0x200000) == 0 && host.PathCheck(position, path.HitPoint, false, 50, default).Code != 0)
            return 0;
        state.TargetPosition = path.HitPoint;
        return RegisterTargetPos(state.TargetPosition) ? 1 : 0;
    }

    /// <summary>Utils_CrapXZDist (0x4E61E0).</summary>
    public static int CrapXZDistance(FixedVector3 first, FixedVector3 second) =>
        ThugPerception.ApproximateHorizontalDistance(first, second);

    private static int Absolute(int value) => value < 0 ? unchecked(-value) : value;
}
