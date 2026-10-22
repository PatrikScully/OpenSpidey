using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Simby;

public sealed partial class SimbyBrain
{
    /// <summary>dword_5555F0: XA slots for the fire-death quips (bank 59).</summary>
    private static readonly int[] FireQuipSlots = [5, 6, 7, 8, 10, 14];

    /// <summary>CSimby::Hit (0x4A8AB0); returns the original's non-zero "hit taken" result.</summary>
    public int Hit(ActorHit hit)
    {
        if ((flags.Check(state.Stage) & 8) != 0 && (state.Field3A0 & 8) == 0) return 0;
        if (state.Health <= 0) return 0;
        int kind = (hit.Flags & 2) != 0 ? hit.AttackKind : 0;
        state.LastHitKind = kind;
        if (state.AlertModeTimer < 2400) state.AlertModeTimer = 2400;
        ushort damage = hit.Damage;
        switch (kind)
        {
            case 7:
                PlayFireQuip();
                state.Stage = 23;
                state.Substate = 0;
                return 1;
            case 23:
                damage = unchecked((ushort)((2000 * damage) >> 12));
                break;
            case 6:
                PlayImpactQuip();
                damage = unchecked((ushort)((1200 * damage) >> 12));
                break;
        }
        state.Health = unchecked((short)(state.Health - ((hit.Flags & 4) != 0 ? damage : 0)));
        if (state.Health <= 0)
        {
            state.Stage = state.LastHitKind == 22 ? 23 : 22;
            state.Substate = 0;
            return 0;
        }
        host.PlayPositionalSound((353 - (host.Random(2) != 0 ? 1 : 0)) | 0x8000);
        if (state.LastHitKind == 3)
        {
            ClearJointTranslations();
            host.SendSelfMessage(20);
            return 0;
        }
        state.BehaviorFlags &= 0xFFFFFF8F;
        if ((hit.Flags & 8) != 0)
        {
            FixedVector3 position = host.Body.Position;
            int delta = FixedAim.Calculate(position, position + (hit.Direction << 12)).Yaw - host.Body.Angles.Y;
            if (delta < -2048) delta += 4096;
            else if (delta > 2048) delta -= 4096;
            if (delta < -455) state.BehaviorFlags |= 0x10;
            else if (delta > 455) state.BehaviorFlags |= 0x20;
        }
        state.Stage = 10;
        state.Substate = 0;
        host.Neutralize();
        if ((hit.Flags & 0x18) != 0)
        {
            short distance = hit.SlideDistance;
            if (state.LastHitKind == 2 && host.PlayerAnimationId == 124) distance = unchecked((short)((8000 * distance) >> 12));
            SlideFromHit(distance, hit.SlideFrames, hit.Direction);
        }
        else if (state.LastHitKind is 2 or 6 or 23)
        {
            FixedVector3 away = host.Body.Position - host.PlayerPosition;
            away = new FixedVector3(away.X, 0, away.Z) >> 12;
            SlideFromHit(10, 15, FixedVectorMath.Normalize(away) * 10);
        }
        return 1;
    }

    private void PlayFireQuip()
    {
        int slot = host.Random(6);
        // The original tests 1 << slot (never zero) instead of the mask bit, so it only speaks once every bit is set.
        if (world.CountResetFlag != 1023) return;
        int quip = FireQuipSlots[slot];
        if (host.PlayAndAttachPlayerXA(59, quip, 100)) world.CountResetFlag |= (1 << slot) + 1023;
    }

    private void PlayImpactQuip()
    {
        if ((world.CountResetFlag & 6) == 6) return;
        int pick = host.Random(2);
        int mask = pick == 0 ? 2 : 4;
        if ((world.CountResetFlag & mask) != 0 || !host.PlayerCanTalk() || !host.PlayAndAttachPlayerXA(47, pick == 0 ? 5 : 6, 100))
            return;
        world.CountResetFlag |= mask;
    }

    /// <summary>CSimby::SlideFromHit (0x4A8FA0): sets a GetLaunched velocity toward a claimed target slot.</summary>
    private void SlideFromHit(int distance, int frames, FixedVector3 direction)
    {
        FixedVector3 flat = direction with { Y = 0 };
        FixedVector3 offset = flat * distance;
        FixedVector3 target = host.Body.Position + offset;
        if (!planner.RegisterTargetPos(target))
        {
            offset >>= 1;
            target = host.Body.Position + offset;
            if (!planner.RegisterTargetPos(target)) return;
            frames >>= 1;
            distance >>= 1;
        }
        ActorPathCheckResult path = host.PathCheck(host.Body.Position, target, true, 55, default);
        if (path.Code == 2)
        {
            FixedVector3 travel = path.HitPoint - host.Body.Position;
            if ((flat.X | flat.Z) == 0) return;
            int steps = Absolute(flat.X) <= Absolute(flat.Z) ? unchecked(frames * travel.Z) / offset.Z : unchecked(frames * travel.X) / offset.X;
            state.Field1F8 = steps;
            if (steps <= 1) return;
            host.Body.Velocity = travel / (steps + 1);
            int spread = unchecked(Absolute(travel.X) + Absolute(travel.Z)) & unchecked((int)0xFFFFE000);
            if (spread > 1392640) state.BehaviorFlags |= 0x40;
            state.Stage = 11;
            state.Substate = 0;
            return;
        }
        if (unchecked((uint)path.Code) > 1) return;
        state.Field1F8 = frames;
        host.Body.Velocity = flat * (distance / frames);
        if (distance > 170) state.BehaviorFlags |= 0x40;
        state.Stage = 11;
        state.Substate = 0;
    }
}
