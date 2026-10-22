using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Venom;

/// <summary>CVenom fight selection and reaction states: EnterFightMode (0x4EE460), GetWithinAttackRange (0x4EF5A0),
/// Hit (0x4EB7F0), TakeHit (0x4ED210), GetTrapped (0x4ECF40), VenomDie (0x4ED330) and the AI-inline TugWeb.</summary>
public static class VenomFight
{
    public const uint PlayerWebDome = 0x20000000;
    public const uint PlayerKnockdown = 0x800000;

    public static void EnterFightMode(VenomState state, IVenomHost host, IVenomCombatHost combat)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(combat);
        uint action = combat.PlayerActionState;
        if (unchecked((uint)(host.TimerRelated - state.Field3E0)) > 0x4B0 || action == PlayerWebDome)
        {
            state.AiState = 1;
            state.Substate = 0;
            return;
        }
        if (action == PlayerKnockdown || combat.PlayerField36C != 0 || combat.PlayerHealth <= 0)
        {
            host.Body.Velocity = default;
            state.Field218 &= 0xFFFFFFFC;
            state.AiState = 64;
            state.Substate = 0;
            return;
        }
        if (state.Substate != 0) return;
        FixedVector3 player = host.PlayerPosition;
        uint distance = unchecked((uint)FixedVectorMath.OriginalDistance(host.Body.Position, player));
        if (distance < 0x100)
        {
            host.Body.Velocity = default;
            state.Field218 = (state.Field218 & 0xFFFFFFFC) | 4;
            state.AiState = 64;
            state.Substate = 0;
            return;
        }
        bool wall = combat.PlayerField8E8 != 0 || combat.PlayerField8E9 != 0;
        if (distance >= 0x400 || combat.PlayerCrawling)
        {
            if (distance >= 0x800)
            {
                if (!host.LineOfSight(host.Body.Position, player))
                {
                    if (VenomJump.ScanNodesForJumpTarget(state, host) == 0) { state.AiState = 0x8000; state.Substate = 0; return; }
                }
                else
                {
                    if (wall) { state.AiState = 512; state.Substate = 0; return; }
                    if (VenomJump.ScanNodesForJumpTarget(state, host) == 0 || host.Random(100) >= state.Field39C)
                    { state.AiState = 512; state.Substate = 0; return; }
                }
            }
            else if (!host.LineOfSight(host.Body.Position, player))
            {
                if (VenomJump.ScanNodesForJumpTarget(state, host) == 0) { state.AiState = 0x8000; state.Substate = 0; return; }
            }
            else
            {
                if (wall) { state.AiState = 512; state.Substate = 0; return; }
                if (VenomJump.ScanNodesForJumpTarget(state, host) == 0 || host.Random(100) >= state.Field39C)
                {
                    if (host.Random(100) < 60) { state.AiState = 256; state.Substate = 0; return; }
                    state.AiState = 512;
                    state.Substate = 0;
                    return;
                }
            }
            VenomJump.CalculateJumpPositionArray(state, host, state.JumpTarget, 48);
            state.ReturnAiState = 32;
            state.ReturnSubstate = 0;
            state.AiState = 16;
            state.Substate = 0;
            return;
        }
        int playerGround = host.GroundHeight(player, 0, 0x2000, 0);
        int heightGap = unchecked(playerGround - (state.SurfaceClearance << 12) - host.Body.Position.Y);
        if (heightGap < 0) heightGap = unchecked(-heightGap);
        if (unchecked((int)((uint)heightGap & 0xFFFFF000)) > 0x80000 && host.LineOfSight(host.Body.Position, player))
        {
            state.AiState = 512;
            state.Substate = 0;
            return;
        }
        state.AiState = 128;
        state.Substate = 0;
    }

    /// <summary>CVenom::CalculateGetWithinAttackRangePoint (0x4EF440): the player's feet or one of three random points around them.</summary>
    public static uint CalculateGetWithinAttackRangePoint(VenomState state, IVenomHost host)
    {
        uint best = uint.MaxValue;
        FixedVector3 player = host.PlayerPosition;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            FixedVector3 point = player;
            if (attempt != 0)
            {
                int reach = host.Random(256) + 256;
                short yaw = unchecked((short)host.Random(4096));
                point = FixedAim.Velocity(reach, new AimAngles(0, yaw)) + player;
            }
            int ground = host.GroundHeight(point, 0, 0x2000, 0);
            if (ground == -1) continue;
            point = point with { Y = unchecked(ground - (state.SurfaceClearance << 12)) };
            if (Math.Abs(unchecked(point.Y - host.Body.Position.Y) >> 12) > 64 ||
                !host.LineOfSight(host.Body.Position, point)) continue;
            if (attempt == 0)
            {
                state.TargetPosition = point;
                return 0;
            }
            uint distance = unchecked((uint)FixedVectorMath.OriginalDistance(host.Body.Position, point));
            if (distance < best)
            {
                best = distance;
                state.TargetPosition = point;
            }
        }
        return best;
    }

    public static void GetWithinAttackRange(VenomState state, IVenomHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (state.Substate == 0)
        {
            if (CalculateGetWithinAttackRangePoint(state, host) != uint.MaxValue)
            {
                state.Field218 |= 1;
                state.Substate++;
                return;
            }
            state.AiState = 32;
            state.Substate = 0;
            return;
        }
        if (state.Substate != 1) return;
        uint distance = unchecked((uint)FixedVectorMath.OriginalDistance(host.Body.Position, host.PlayerPosition));
        if (distance < 0xC0)
        {
            state.Field218 &= ~1u;
            host.Body.Velocity = default;
            state.Substate = 0;
            state.AiState = 64;
            return;
        }
        if (distance > 0x500 || ((host.Random(4) == 0 ||
                unchecked((uint)FixedVectorMath.OriginalDistance(host.Body.Position, state.TargetPosition)) <
                unchecked((uint)(12 * host.ElapsedTicks))) &&
            CalculateGetWithinAttackRangePoint(state, host) == uint.MaxValue))
        {
            state.Field218 &= ~1u;
            host.Body.Velocity = default;
            state.AiState = 32;
            state.Substate = 0;
        }
    }

    /// <summary>CVenom::Hit (0x4EB7F0).</summary>
    public static bool Hit(VenomState state, IVenomHost host, IVenomCombatHost combat, ActorHit hit)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(combat);
        if (state.MinorRadius == 0) return false;
        if (host.LevelId == 1281)
        {
            state.Field218 |= 0x900;
            return false;
        }
        if (state.Health < 0) return false;
        if ((host.ItemFlags & 1) != 0) return false;
        if ((state.Field218 & 0x4000) != 0) return false;
        int animation = host.AnimationId, frame = host.AnimationFrame;
        if (animation == 18 && frame >= 3 && frame <= 14) return false;
        if (animation == 17 && frame >= 3 && frame <= 14) return false;
        if (animation == 34 && frame >= 9) return false;
        state.Field388 = 30;
        host.Body.Velocity = default;
        state.Field218 &= 0xFFFFFFC0;
        state.HitKind = (hit.Flags & 2) != 0 ? hit.AttackKind : 0;
        host.PlayPositionalSound(33020);
        state.Health = unchecked((short)(state.Health - hit.Damage));
        state.AccumulatedDamage = unchecked(state.AccumulatedDamage + hit.Damage);
        if (state.StretchTimer != 0)
        {
            state.StretchTimer = 0;
            combat.SetArmPose(default, default);
            host.ItemFlags &= unchecked((ushort)~4);
        }
        combat.BurstWeb(false);
        if (state.Health <= 0)
        {
            host.RunAnimation(0x28);
            state.AiState = 4096;
            state.Substate = 0;
            return true;
        }
        if (unchecked((uint)state.AccumulatedDamage) < 100u)
        {
            int window = host.DifficultyLevel == 2 ? 40 : host.DifficultyLevel is 1 or 0 ? 20 : 60;
            if (host.TimerRelated - state.LastHitTime < window && (state.Field218 & 0x180) == 0)
            {
                state.AiState = 64;
                state.Substate = 0;
                state.LastHitTime = host.TimerRelated;
                return true;
            }
        }
        else
        {
            state.AccumulatedDamage = 0;
            state.Field218 = (state.Field218 & 0xFFFFFF7F) | 0x100;
            state.AiState = 1;
            state.Substate = 0;
        }
        state.LastHitTime = host.TimerRelated;
        if ((hit.Flags & 8) != 0)
        {
            FixedVector3 position = host.Body.Position;
            AimAngles aim = FixedAim.Calculate(position, position + (hit.Direction << 12));
            int turn = aim.Yaw - host.Body.Angles.Y;
            if (turn >= -2048) { if (turn > 2048) turn -= 4096; }
            else turn += 4096;
            if (turn < -256) state.Field218 |= 8;
            else if (turn > 256) state.Field218 |= 0x10;
            byte flags = hit.Flags;
            FixedVector3 direction = hit.Direction;
            short slideDistance = hit.SlideDistance;
            ushort slideFrames = hit.SlideFrames;
            if ((flags & 2) != 0 && hit.AttackKind == 2)
            {
                flags |= 0x10;
                slideDistance = 600;
                slideFrames = 16;
                direction = FixedVectorMath.Normalize(direction);
            }
            if ((flags & 0x10) != 0)
            {
                state.SlideVelocity = direction * slideDistance / slideFrames;
                state.SlideTicks = slideFrames;
                if (slideDistance > 128)
                {
                    state.Field218 |= 0x20;
                    FixedVector3 behind = host.Body.Position + direction * 512;
                    AimAngles away = FixedAim.Calculate(host.Body.Position, behind);
                    host.Body.Angles = host.Body.Angles with { Y = unchecked((short)((away.Yaw - 2048) & 0xFFF)) };
                }
                CheckSlideParams(state, host, combat);
            }
        }
        if (state.AiState != 2048)
        {
            state.InterruptedAiState = state.AiState;
            state.InterruptedSubstate = state.Substate;
        }
        state.AiState = 2048;
        state.Substate = 0;
        return true;
    }

    /// <summary>CVenom::CheckSlideParams (0x4EBCA0): clip the slide against the world.</summary>
    public static void CheckSlideParams(VenomState state, IVenomHost host, IVenomCombatHost combat)
    {
        FixedVector3 end = host.Body.Position + state.SlideVelocity * state.SlideTicks;
        if (combat.LineToItem(host.Body.Position, end) is not { } line) return;
        if (line.Distance - 256 > 0)
            state.SlideTicks = unchecked(state.SlideTicks * (line.Distance - 256)) / line.Length;
        else
        {
            state.SlideTicks = 0;
            state.SlideVelocity = default;
        }
    }

    /// <summary>CVenom::TakeHit (0x4ED210).</summary>
    public static void TakeHit(VenomState state, IVenomHost host, IVenomCombatHost combat)
    {
        if (state.Substate == 0)
        {
            if (state.WebHeld) { combat.DeleteWeb(); state.WebHeld = false; }
            uint flags = state.Field218;
            if ((flags & 0x20) != 0) host.RunAnimation(0x16);
            else if ((flags & 8) != 0) host.RunAnimation(0x14);
            else if ((flags & 0x10) != 0) host.RunAnimation(0x13);
            else host.RunAnimation(0x15);
            state.Substate++;
        }
        else if (state.Substate != 1) return;
        int ticks = host.ElapsedTicks;
        if (state.SlideTicks < ticks)
        {
            host.Body.Position += state.SlideVelocity * state.SlideTicks;
            state.SlideTicks = 0;
        }
        else
        {
            host.Body.Position += state.SlideVelocity * ticks;
            state.SlideTicks = unchecked(state.SlideTicks - ticks);
        }
        if (!host.AnimationFinished) return;
        if (state.InterruptedAiState == 1)
        {
            state.AiState = 1;
            state.Substate = 1;
        }
        else
        {
            state.AiState = 32;
            state.Substate = 0;
            state.Field3E0 = host.TimerRelated;
        }
    }

    /// <summary>CVenom::VenomDie (0x4ED330).</summary>
    public static void VenomDie(VenomState state, IVenomHost host)
    {
        switch (state.Substate)
        {
            case 0:
                state.Field330 = 16;
                state.Field218 &= 0xFFFFFFF8;
                host.Body.Velocity = default;
                state.BodyFlags &= 0xFFEF;
                host.ItemFlags &= 0xFFBE;
                state.Field218 &= 0xFFFFFE7F;
                state.Substate++;
                break;
            case 1:
                state.Field330 = 16;
                if (host.AnimationFinished)
                {
                    host.RunAnimation(0x29);
                    state.Substate++;
                }
                break;
            case 2:
                if (!host.AnimationFinished) break;
                if (host.LevelId == 1540)
                {
                    host.PulseL6A4Node(false);
                    state.Substate++;
                }
                else host.Die();
                break;
        }
    }

    /// <summary>CVenom::GetTrapped (0x4ECF40): state 1024 while a trap web holds the boss.</summary>
    public static void GetTrapped(VenomState state, IVenomHost host, IVenomCombatHost combat)
    {
        state.Field330 = 16;
        if (state.StretchTimer != 0)
        {
            state.StretchTimer = 0;
            combat.SetArmPose(default, default);
            host.ItemFlags &= unchecked((ushort)~4);
        }
        switch (state.Substate)
        {
            case 0:
                combat.Procedures.Attach(new VenomStateSwitchMessageProcedure(state, 14, combat.SendSelfMessage));
                host.RunAnimation(0x1F);
                state.Substate++;
                break;
            case 1:
                if (host.AnimationFinished)
                {
                    host.RunAnimation(0x20);
                    state.ReturnSubstate = 0;
                    state.TrapCounter = 16;
                    state.Substate++;
                }
                break;
            case 2:
            {
                int animation = host.AnimationId;
                if ((animation != 32 && animation != 31) || host.AnimationFinished) host.RunAnimation(0x20);
                state.TrapCounter--;
                if (state.TrapCounter > 0) break;
                int hooks = combat.TrapWebHookCount();
                if (hooks < 0 || hooks == state.ReturnSubstate) state.Substate++;
                else
                {
                    state.ReturnSubstate = unchecked((short)hooks);
                    state.TrapCounter = 16;
                }
                break;
            }
            case 3:
                if (host.AnimationId == 33)
                {
                    if (!host.AnimationFinished) break;
                    state.AiState = 32;
                    state.Substate = 0;
                    if (state.ElectrifiedPresent) { combat.SetElectrified(false); state.ElectrifiedPresent = false; }
                    state.Field3E0 = host.TimerRelated;
                }
                else
                {
                    host.RunAnimation(0x21);
                    combat.BurstWeb(false);
                }
                break;
        }
    }

    /// <summary>State 0x2000, inline in CVenom::AI (the recovered CVenom::TugWeb).</summary>
    public static void TugWeb(VenomState state, IVenomHost host, IVenomCombatHost combat)
    {
        if (state.Substate == 0)
        {
            state.Field218 &= 0xFFFFFFF8;
            host.Body.Velocity = default;
            state.Substate++;
        }
        else if (state.Substate != 1) return;
        if ((state.Field218 & 0x200) == 0) return;
        state.Field218 &= ~0x200u;
        combat.BurstWeb(true);
        state.AiState = 32;
        state.Substate = 0;
    }

    /// <summary>CVenom::AI message loop: codes 5, 6, 14 and 15 outside the dying state.</summary>
    public static void ProcessMessages(VenomState state, IVenomHost host, IVenomCombatHost combat)
    {
        for (Thugs.ThugMessage? message = host.Messages.Head; message is not null; message = message.Next)
        {
            if (state.AiState != 4096)
            {
                switch (message.Code)
                {
                    case 5:
                        if (state.AiState != 1024)
                        {
                            state.Field218 &= 0xFFFFFFF8;
                            host.Body.Velocity = default;
                            state.AiState = 1024;
                            state.Substate = 0;
                        }
                        else
                        {
                            state.TrapCounter = 0;
                            state.Substate = 2;
                        }
                        break;
                    case 6:
                        state.AiState = 0x2000;
                        state.Substate = 0;
                        break;
                    case 14: combat.BurstWeb(false); break;
                    case 15: combat.BurstWeb(true); break;
                }
            }
            message.Flags |= 1;
        }
        host.Messages.Cleanup();
    }
}
