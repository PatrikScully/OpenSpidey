using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Simby;

public interface ISimbyHost
{
    ActorBodyMotion Body { get; }
    ActorProcedureState Actor { get; }
    ActorProcedureList Procedures { get; }
    ushort ItemFlags { get; set; }
    int AnimationId { get; }
    bool AnimationFinished { get; }
    int ElapsedTicks { get; }
    int AttackClock { get; }
    FixedVector3 PlayerPosition { get; }
    short PlayerHealth { get; }
    byte PlayerSpecialState { get; }
    byte PlayerInputLock { get; }
    bool HasMessages { get; }
    bool HasProcedures { get; }
    bool HasSupportBody { get; }
    /// <summary>Words at 0x5521B8 / 0x5521BC used by MoveFreely idle latch (513, 0).</summary>
    int IdleSetupLow { get; }
    int IdleSetupHigh { get; }
    /// <summary>CBaddy field_1F4 / ActorWaypointRoute.CurrentNode.</summary>
    int CurrentWaypoint { get; }
    /// <summary>Live CBaddy field_2F0 mirrored from the waypoint route.</summary>
    ushort WaypointFlags { get; set; }
    int PerimeterDistance(int mode);
    int GroundHeight(FixedVector3 position, int above, int below, int flags);
    /// <summary>Utils_GetGroundHeightEx style query used by DropDown (writes support into field_2A4).</summary>
    int GroundHeightWithSupport(FixedVector3 position, int above, int below, out bool hasSupport);
    /// <summary>CBaddy::SetHeight; non-zero means the body stayed on a surface.</summary>
    int SetHeight(bool initial, int above, int below);
    FixedVector3 WaypointPosition(int node);
    void RunAnimation(int animation);
    void CycleAnimation(int animation);
    void Neutralize();
    void BuildTransform();
    void ProcessMessages();
    void ProcessProcedures();
    void DoPhysics(int mode);
    void RunTimer(ref int timer);
    bool TryAdvanceWaypoint();
    /// <summary>CSimby::GetToTarget (0x4A9F00).</summary>
    int GetToTarget(int mode);
    void SpawnEmergeEffect();
    void PlayPositionalSound(int id);
    int Random(int bound);
    void EnsureSlimeQuad();
    void DestroySlimeQuad();
    void UpdateSlimeQuad(bool invertedClearance);
    bool LineOfSightToPlayer();
    bool RegisterTargetFromPlayer();
    /// <summary>CBaddy::BumpedIntoSpidey (0x404470).</summary>
    bool BumpedIntoSpidey(int radius);
}

/// <summary>CSimby::AI (0x4AE3D0) with Initialize, Emerge and MoveFreely leaves.</summary>
public sealed class SimbyBrain(SimbyState state, SimbyWorldState world, SimbyStateFlags flags, ISimbyHost host)
{
    public void Advance()
    {
        if (state.LevelId == 0x1104 && host.PerimeterDistance(2) > 350)
            state.Field324 = unchecked(state.Field324 + host.ElapsedTicks);

        if (state.FlashFrames != 0)
        {
            ushort item = host.ItemFlags;
            SimbyFlash.Update(state, ref item);
            host.ItemFlags = item;
        }

        UpdateSlimePresence();

        if ((state.BehaviorFlags & 0x80) != 0)
            host.Body.Angles = host.Body.Angles with { X = unchecked((short)(host.Body.Angles.X - 2048)) };

        int alert = state.AlertModeTimer;
        host.RunTimer(ref alert);
        state.AlertModeTimer = alert;

        ushort flag = flags.Check(state.Stage);
        if ((state.BehaviorFlags & 0x8000) != 0 && (flag & 0x4000) == 0)
        {
            state.Stage = 23;
            state.Substate = 0;
            state.BehaviorFlags &= ~0x8000u;
            flag = flags.Check(state.Stage);
        }
        if (state.Health <= 0 && (flag & 0x100) == 0)
        {
            state.Stage = 22;
            state.Substate = 0;
            flag = flags.Check(state.Stage);
        }
        if ((flag & 0x20) != 0)
        {
            state.CombatFlags |= 0x40000;
            if (host.SetHeight(state.CeilingProbeLatch == 0, 100, 150) == 0)
            {
                state.Stage = 8;
                state.Substate = 0;
            }
            state.CeilingProbeLatch = 1;
            flag = flags.Check(state.Stage);
        }
        else state.CeilingProbeLatch = 0;

        if ((state.BehaviorFlags & 0x70000) != 0 && (flag & 0x8000) == 0)
            throw new NotSupportedException(
                "CSimby::AI behavior 0x70000 cleanup (0x4AAAE0 / tether release at 0x4AE628) is not ported yet.");

        if (state.AttackMask != 0)
        {
            if ((flag & 0x80) == 0) SimbyReactions.ClearAttackData(state, world);
            else if (state.AlertModeTimer < 2400) state.AlertModeTimer = 2400;
        }

        if ((flag & 4) == 0) host.DoPhysics(0);
        // Original runs AIProcList::Execute then CleanUpAIProcList(0); Neutralize is only inside leaves.
        if (host.HasProcedures) host.ProcessProcedures();
        if (host.HasMessages) host.ProcessMessages();

        // AI zeroes field_3A0 every frame before the state switch; leaves OR display bits back in.
        state.Field3A0 = 0;

        if (state.Substate == 0 || (flag & 0x8000) != 0)
        {
            if ((flag & 0x800) != 0 && state.Health > 0) state.CombatFlags |= 0x10;
            else state.CombatFlags &= ~0x10u;
        }
        if ((flag & 0x10) != 0) state.CombatFlags |= 0x10000;
        else state.CombatFlags &= ~0x10000u;

        if (state.Stage == 25)
        {
            state.CombatFlags |= 0x10;
            state.Stage = 4;
            state.Substate = 0;
        }
        else if (state.Health > 0 && host.PlayerHealth > 0 && host.PlayerSpecialState == 0)
        {
            flag = flags.Check(state.Stage);
            if ((flag & 0x40) != 0) DetermineFightState();
            if ((host.AttackClock & 3) == 0 && (flag & 0x400) != 0
                && Absolute(host.Body.Position.Y - host.PlayerPosition.Y) < 409600
                && host.PerimeterDistance(4) < 350)
            {
                state.Stage = 17;
                state.Substate = 0;
            }
        }

        flag = flags.Check(state.Stage);
        if ((flag & 0x2000) != 0) PlayGruntSound();

        switch (state.Stage)
        {
            case 0: Initialize(); break;
            case 1: Emerge(); break;
            case 2:
                if ((state.BehaviorFlags & 0x10000) != 0) goto AfterState;
                FollowWaypoints();
                break;
            case 4:
                if ((state.BehaviorFlags & 0x10000) != 0) goto AfterState;
                MoveFreely();
                break;
            case 5:
                if ((state.BehaviorFlags & 0x10000) != 0) goto AfterState;
                throw new NotSupportedException("CSimby::WalkToTarget (0x4AA4D0) is not ported yet.");
            case 6:
            case 16:
            case 24:
                throw new NotSupportedException("CSimby::SlinkToTarget (0x4AABF0) is not ported yet.");
            case 7:
                if ((state.BehaviorFlags & 0x10000) != 0) goto AfterState;
                throw new NotSupportedException("CSimby::ClimbToTarget (0x4AB570) is not ported yet.");
            case 8:
            case 20:
                DropDown();
                break;
            case 9:
                throw new NotSupportedException("CSimby::MoveInForAttack (0x4A9830) is not ported yet.");
            case 10:
                throw new NotSupportedException("CSimby::TakeHit (0x4ADB00) is not ported yet.");
            case 11:
                throw new NotSupportedException("CSimby::GetLaunched (0x4A7BD0) is not ported yet.");
            case 12:
            case 13:
                throw new NotSupportedException("CSimby::Trapped (0x4A87B0) is not ported yet.");
            case 14:
                if (state.AlertModeTimer < 2400) state.AlertModeTimer = 2400;
                state.CombatFlags |= 0x20;
                if ((state.CombatFlags & 0x40) != 0)
                    throw new NotSupportedException("CSimby::GettingGrabbed (0x4A7FA0) is not ported yet.");
                state.Stage = 4;
                state.Substate = 0;
                break;
            case 17:
                throw new NotSupportedException("CSimby::AttackPlayer (0x4AD520) is not ported yet.");
            case 18:
                if ((state.BehaviorFlags & 0x10000) != 0) goto AfterState;
                throw new NotSupportedException("CSimby::Shoot (0x4ACFB0) is not ported yet.");
            case 19:
                throw new NotSupportedException("CSimby::DieWhileFalling (0x4ADD50) is not ported yet.");
            case 21:
                throw new NotSupportedException("CSimby::SlideToAttack (0x4AA770) is not ported yet.");
            case 22:
                throw new NotSupportedException("CSimby::DieSimby (0x4ADEA0) is not ported yet.");
            case 23:
                throw new NotSupportedException("CSimby::DieFireyDeath (0x4ADC30) is not ported yet.");
            default:
                throw new NotSupportedException(
                    $"CSimby::AI state {state.Stage} (substate {state.Substate}) is not ported yet; original dispatch 0x4AE3D0.");
        }

    AfterState:
        if ((state.Field3A0 & 0x10) != 0) host.ItemFlags |= 1;
        else host.ItemFlags = unchecked((ushort)(host.ItemFlags & ~1));

        if ((state.BehaviorFlags & 0x80) != 0)
            host.Body.Angles = host.Body.Angles with { X = unchecked((short)(host.Body.Angles.X + 2048)) };

        if (state.SlimeHandle != 0)
            host.UpdateSlimeQuad((state.BehaviorFlags & 0x80) != 0);

        if ((host.ItemFlags & 4) == 0 || (state.BehaviorFlags & 0x2000) != 0)
            host.BuildTransform();
        else
            throw new NotSupportedException(
                "CSimby::AI stretched-pose display (ReadLinks 0x5554A0 path at 0x4AF00A) is not ported yet.");
    }

    private void UpdateSlimePresence()
    {
        int absY = Absolute(host.Body.Position.Y - host.PlayerPosition.Y);
        bool near = (short)host.ItemFlags >= 0 || (absY <= 0x578000 && state.Field324 <= 0x9C4);
        if (!near)
        {
            if (state.SlimeHandle != 0)
            {
                host.DestroySlimeQuad();
                state.SlimeHandle = 0;
            }
            state.SlimeRespawnTimer = 30;
            return;
        }
        if (state.SlimeRespawnTimer == 0) return;
        int timer = state.SlimeRespawnTimer;
        host.RunTimer(ref timer);
        state.SlimeRespawnTimer = timer;
        if (state.SlimeRespawnTimer == 0 && state.SlimeHandle == 0)
            host.EnsureSlimeQuad();
    }

    private void PlayGruntSound()
    {
        int timer = state.AlertNoiseTimer;
        host.RunTimer(ref timer);
        state.AlertNoiseTimer = timer;
        if (state.AlertNoiseTimer != 0) return;
        state.AlertNoiseTimer = host.Random(180) + 180;
        int pick = host.Random(3);
        int sound = pick switch { 0 => 349, 1 => 350, 2 => 351, _ => 349 };
        host.PlayPositionalSound(sound | 0x8000);
    }

    /// <summary>CSimby::DetermineFightState (0x4AE060).</summary>
    private void DetermineFightState()
    {
        int distance = host.PerimeterDistance(2);
        state.FightProbeThreshold = distance < 500 ? 5 : distance < 1500 ? 10 : 20;
        state.FightProbeAccumulator = unchecked(state.FightProbeAccumulator + host.ElapsedTicks);
        if (state.FightProbeAccumulator < state.FightProbeThreshold) return;
        state.FightProbeAccumulator = 0;
        if (!host.LineOfSightToPlayer()) return;
        if (!CheckSightCone() && !host.BumpedIntoSpidey(110)) return;
        if (state.AlertModeTimer < 2400) state.AlertModeTimer = 2400;
        if (state.Stage == 2)
        {
            host.PlayPositionalSound(0x8154);
            state.Stage = 9;
            state.Substate = 0;
        }
    }

    /// <summary>CBaddy::CheckSightCone (0x404510) with Simby field_3C0/3BC/3B8/30C.</summary>
    private bool CheckSightCone()
    {
        if (state.Field3BC == 0) return false;
        if (Absolute(host.PlayerPosition.Y - host.Body.Position.Y) > (state.Field3BC << 12)) return false;
        int distance = host.PerimeterDistance(2);
        if (distance < state.Field30C) return true;
        if (distance > state.Field3B8) return false;
        int delta = FixedAim.Calculate(host.Body.Position, host.PlayerPosition).Yaw - host.Body.Angles.Y;
        if (delta < -2048) delta += 4096;
        else if (delta > 2048) delta -= 4096;
        return Absolute(delta) <= (state.Field3C0 >> 1);
    }

    /// <summary>CSimby::DropDown (0x4AC180). Long-fall matt spawn (sub1/2) stays NotSupported.</summary>
    private void DropDown()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                if (state.SlimeHandle != 0)
                {
                    host.DestroySlimeQuad();
                    state.SlimeHandle = 0;
                }
                if (unchecked((byte)(state.SetupHigh >> 8)) != 0)
                {
                    state.SetupLow = host.IdleSetupLow;
                    state.SetupHigh = host.IdleSetupHigh;
                }
                if ((state.BehaviorFlags & 0x80) != 0)
                {
                    state.BehaviorFlags &= ~0x80u;
                    host.RunAnimation(52);
                    host.PlayPositionalSound((345 - (host.Random(2) != 0 ? 1 : 0)) | 0x8000);
                    state.Substate = 15;
                    break;
                }
                state.Field3A0 |= 2;
                {
                    int ground = host.GroundHeightWithSupport(host.Body.Position, 0, 2000, out bool hasSupport);
                    if (ground == -1)
                    {
                        state.Stage = 19;
                        state.Substate = 0;
                        break;
                    }
                    if (hasSupport)
                    {
                        host.Body.Velocity = host.Body.Velocity with { Y = 122880 };
                        state.BehaviorFlags &= ~0x4000u;
                        state.Substate = 5;
                        break;
                    }
                    state.TargetPosition = host.Body.Position;
                    int landY = unchecked(ground - (state.SurfaceClearance << 12));
                    state.TargetPosition = state.TargetPosition with { Y = landY };
                    if (state.Stage == 20 || Absolute(landY - host.Body.Position.Y) > 81920)
                    {
                        host.CycleAnimation(36);
                        host.Body.Velocity = host.Body.Velocity with { Y = 122880 };
                        state.Substate++;
                    }
                    else
                    {
                        host.CycleAnimation(unchecked((byte)state.SetupHigh));
                        host.SetHeight(true, 5, 150);
                        state.Substate = 3;
                    }
                }
                break;
            case 1:
            {
                // Fall toward TargetPosition.Y; sym_base matt / CAIProc_StateSwitchSendMessage(17) stay deferred.
                int vel = host.Body.Velocity.Y;
                int nextY = unchecked(host.Body.Position.Y + vel);
                host.Body.Velocity = host.Body.Velocity with { Y = unchecked(vel + 122880) };
                state.Field3A0 |= 2;
                host.Body.Position = host.Body.Position with { Y = nextY };
                if (nextY > state.TargetPosition.Y)
                {
                    host.Body.Position = new FixedVector3(
                        state.TargetPosition.X, state.TargetPosition.Y, state.TargetPosition.Z);
                    host.RunAnimation(38);
                    state.Substate++;
                }
                break;
            }
            case 2:
                state.Field3A0 |= 2;
                if (host.AnimationFinished)
                {
                    host.SetHeight(true, 100, 150);
                    state.Substate++;
                }
                break;
            case 3:
                state.DropSampleY = host.Body.Position.Y;
                host.SetHeight(false, 100, 600);
                if (state.SlimeHandle == 0) host.EnsureSlimeQuad();
                // Support-body band (field_2A4+100) stays for a later host; settle on stable Y.
                if (Absolute(state.DropSampleY - host.Body.Position.Y) < 4096)
                {
                    state.Substate = 0;
                    state.Stage = 4;
                }
                break;
            case 5:
            {
                int ground = host.GroundHeightWithSupport(host.Body.Position, 0, 2000, out _);
                if (ground == -1)
                {
                    state.Stage = 19;
                    state.Substate = 0;
                    break;
                }
                int landY = unchecked(ground - (state.SurfaceClearance << 12));
                if (host.AnimationId != 36 && Absolute(landY - host.Body.Position.Y) > 81920)
                {
                    host.CycleAnimation(36);
                    state.BehaviorFlags |= 0x4000;
                }
                int vel = host.Body.Velocity.Y;
                int nextY = unchecked(host.Body.Position.Y + vel);
                host.Body.Velocity = host.Body.Velocity with { Y = unchecked(vel + 122880) };
                state.Field3A0 |= 2;
                host.Body.Position = host.Body.Position with { Y = nextY };
                if (nextY > landY)
                {
                    host.Body.Position = host.Body.Position with { Y = landY };
                    if ((state.BehaviorFlags & 0x4000) != 0) host.RunAnimation(38);
                    state.Substate++;
                }
                break;
            }
            case 6:
                host.SetHeight(false, 5, 150);
                if ((state.BehaviorFlags & 0x4000) == 0 || host.AnimationFinished)
                {
                    if (state.SlimeHandle == 0) host.EnsureSlimeQuad();
                    state.Stage = 4;
                    state.Substate = 0;
                }
                break;
            case 15:
                if (host.AnimationId >= 10) state.Field3A0 |= 2;
                if (host.AnimationFinished) state.Substate = 0;
                break;
            default:
                throw new InvalidDataException($"Unknown CSimby DropDown substate {state.Substate}.");
        }
    }

    /// <summary>CSimby::FollowWaypoints (0x4A9240).</summary>
    private void FollowWaypoints()
    {
        RunAppropriateAnim();
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.Field310 = 0;
                state.CombatFlags |= 0x10000000;
                state.RouteFlags = host.WaypointFlags;
                if ((state.RouteFlags & 0x20) != 0)
                {
                    state.TargetPosition = host.WaypointPosition(host.CurrentWaypoint);
                    // print_if_false(SetUpJumpPath()!=0, "Unable to set up stretch to script point!");
                    _ = SimbyGetToTarget.TrySetupJumpPath(state, host);
                    host.WaypointFlags = unchecked((ushort)(host.WaypointFlags & ~0x20));
                    state.RouteFlags = host.WaypointFlags;
                    state.Stage = 24;
                    state.Substate = 0;
                    break;
                }
                host.Procedures.AttachMove(new ActorMoveToProcedure(
                    host.Actor, host.WaypointPosition(host.CurrentWaypoint), 200, 40, 500, 1));
                state.Substate++;
                host.PlayPositionalSound((358 - (host.Random(2) != 0 ? 1 : 0)) | 0x8000);
                break;
            case 1:
                if ((host.Actor.CompletionFlags & 1) == 0) break;
                host.Actor.CompletionFlags &= ~1u;
                if (state.Field1F0 != 0) state.Field1F0--;
                else if (!host.TryAdvanceWaypoint())
                {
                    state.AimYaw = host.Body.Angles.Y;
                    state.Stage = 4;
                    state.Substate = 0;
                    break;
                }
                state.RouteFlags = host.WaypointFlags;
                if ((state.RouteFlags & 2) != 0)
                {
                    state.Field1F8 = 60;
                    host.CycleAnimation(unchecked((byte)state.SetupHigh));
                    state.Substate++;
                }
                else state.Substate = 0;
                break;
            case 2:
            {
                int timer = state.Field1F8;
                host.RunTimer(ref timer);
                state.Field1F8 = timer;
                if (state.Field1F8 == 0) state.Substate = 0;
                break;
            }
            default:
                throw new InvalidDataException($"Unknown CSimby FollowWaypoints substate {state.Substate}.");
        }
    }

    /// <summary>CBaddy::RunAppropriateAnim (0x404B60) using Simby setup words.</summary>
    private void RunAppropriateAnim()
    {
        uint movement = host.Body.MovementFlags;
        byte first = unchecked((byte)state.SetupLow);
        byte second = unchecked((byte)(state.SetupLow >> 8));
        if ((movement & 0x40000) != 0)
        {
            if (first != second && host.AnimationId != second && host.AnimationId != first)
            {
                host.RunAnimation(first);
                return;
            }
            if (first == second || host.AnimationId != first || host.AnimationFinished)
                host.CycleAnimation(second);
            return;
        }
        if ((movement & 0x10000) != 0)
        {
            byte clip = unchecked((byte)(state.SetupLow >> 16));
            if (host.AnimationId != clip || host.AnimationFinished) host.RunAnimation(clip);
            return;
        }
        if ((movement & 0x20000) != 0)
        {
            byte clip = unchecked((byte)(state.SetupLow >> 24));
            if (host.AnimationId != clip || host.AnimationFinished) host.RunAnimation(clip);
            return;
        }
        host.CycleAnimation(unchecked((byte)state.SetupHigh));
    }

    private void Initialize()
    {
        switch (state.Substate)
        {
            case 0:
            {
                int ground = host.GroundHeight(host.Body.Position, 300, 300, 0);
                if (ground == -1)
                {
                    state.Stage = 8;
                    state.Substate = 0;
                    break;
                }
                host.SetHeight(true, 100, 150);
                int height = unchecked(ground - (state.SurfaceClearance << 12));
                host.Body.Position = host.Body.Position with { Y = height };
                state.ShadowPosition = host.Body.Position with { Y = height };
                state.ShadowGroundY = ground;
                state.Substate++;
                host.RunAnimation(unchecked((byte)state.SetupHigh));
                if ((state.BehaviorFlags & 0x100000) != 0) state.Field3A0 |= 0x10;
                break;
            }
            case 1:
                if ((state.BehaviorFlags & 0x100000) != 0)
                {
                    state.Stage = 1;
                    state.Substate = 0;
                    state.Field3A0 |= 0x10;
                    break;
                }
                state.SlimeRespawnTimer = 120;
                if (state.Field1F0 != 0 || host.TryAdvanceWaypoint())
                {
                    state.Stage = 2;
                    state.Substate = 0;
                }
                else
                {
                    state.Stage = 4;
                    state.Substate = 0;
                }
                break;
            default:
                throw new InvalidDataException($"Unknown CSimby Initialize substate {state.Substate}.");
        }
    }

    private void Emerge()
    {
        switch (state.Substate)
        {
            case 0:
                state.Field3A0 |= 0x10;
                host.SpawnEmergeEffect();
                state.Substate++;
                host.CycleAnimation(0);
                host.PlayPositionalSound(0x8167);
                break;
            case 1:
                if ((state.BehaviorFlags & 0x800) != 0) break;
                state.BehaviorFlags &= ~0x100000u;
                if (host.PerimeterDistance(0) <= state.Field3BC)
                {
                    state.SetupLow = host.IdleSetupLow;
                    state.SetupHigh = host.IdleSetupHigh;
                    host.RunAnimation(unchecked((byte)state.SetupHigh));
                }
                else host.RunAnimation(4);
                state.Substate++;
                break;
            case 2:
                state.Field3A0 |= 8;
                state.CombatFlags |= 0x10;
                if (!host.AnimationFinished && host.AnimationId != unchecked((byte)state.SetupHigh)) break;
                if (state.SlimeHandle == 0) host.EnsureSlimeQuad();
                if (state.Field1F0 != 0 || host.TryAdvanceWaypoint())
                {
                    state.Stage = 2;
                    state.Substate = 0;
                }
                else
                {
                    state.Stage = 4;
                    state.Substate = 0;
                }
                break;
            default:
                throw new InvalidDataException($"Unknown CSimby Emerge substate {state.Substate}.");
        }
    }

    /// <summary>CSimby::MoveFreely (0x4AC980).</summary>
    private void MoveFreely()
    {
        int sub = state.Substate;
        if (sub > 10)
        {
            if (sub != 666) throw new InvalidDataException($"Unknown CSimby MoveFreely substate {state.Substate}.");
            int timer = state.Field1F8;
            host.RunTimer(ref timer);
            state.Field1F8 = timer;
            if (state.Field1F8 == 0) state.Substate = 0;
            return;
        }
        if (sub == 10)
        {
            if (host.AnimationFinished) state.Substate = 0;
            return;
        }

        switch (sub)
        {
            case 0:
                host.Neutralize();
                if (state.Field324 > 600)
                {
                    state.Stage = 18;
                    state.Substate = 0;
                    return;
                }
                if (host.PlayerSpecialState == 0 && host.PlayerInputLock != 0
                    && host.PerimeterDistance(2) < 1500
                    && host.LineOfSightToPlayer()
                    && host.RegisterTargetFromPlayer())
                {
                    state.Stage = 18;
                    state.Substate = 0;
                    return;
                }
                MoveFreelyIdleOrSeek();
                return;
            case 1:
                if (host.GetToTarget(0) == 0) state.Substate++;
                else { state.Stage = 6; state.Substate = 0; }
                return;
            case 2:
            case 5:
                if (host.GetToTarget(1) == 0) state.Substate++;
                else { state.Stage = 5; state.Substate = 0; }
                return;
            case 3:
                if (host.GetToTarget(2) != 0)
                {
                    state.Stage = 7;
                    state.Substate = 0;
                    return;
                }
                state.Field230 = (state.BehaviorFlags & 0x400000) != 0 ? 30 : 15;
                host.CycleAnimation(unchecked((byte)state.SetupHigh));
                state.Substate++;
                return;
            case 4:
            {
                int timer = state.Field230;
                if (timer == 0)
                {
                    if ((state.BehaviorFlags & 1) == 0)
                    {
                        if (state.AlertModeTimer != 0) state.BehaviorFlags |= 2;
                        state.AimYaw = unchecked((state.AimYaw + 455) & 0xFFF);
                    }
                    state.Substate = 0;
                }
                host.RunTimer(ref timer);
                state.Field230 = timer;
                return;
            }
            case 6:
                if (host.GetToTarget(0) != 0) { state.Stage = 6; state.Substate = 0; }
                else state.Substate = 3;
                return;
            default:
                throw new InvalidDataException($"Unknown CSimby MoveFreely substate {state.Substate}.");
        }
    }

    private void MoveFreelyIdleOrSeek()
    {
        if ((state.BehaviorFlags & 0x200000) != 0 && !host.LineOfSightToPlayer())
        {
            state.Substate = 666;
            state.Field1F8 = 100;
            host.CycleAnimation(unchecked((byte)state.SetupHigh));
            return;
        }
        if (host.PlayerHealth <= 0) state.AlertModeTimer = 0;

        byte setupHighByte = unchecked((byte)(state.SetupHigh >> 8));
        if (state.AlertModeTimer != 0)
        {
            state.CombatFlags |= 0x20;
            state.Field310 = 100;
            if (setupHighByte != 0)
            {
                LatchIdleAnim();
                return;
            }
        }
        else
        {
            state.CombatFlags &= ~0x20u;
            state.Field310 = 0;
            if (setupHighByte != 0)
            {
                LatchIdleAnim();
                return;
            }
        }

        if ((state.BehaviorFlags & 8) != 0
            || Absolute(host.Body.Position.Y - host.PlayerPosition.Y) >= 409600
            || host.PlayerSpecialState != 0)
        {
            SeekWithoutCombat();
            return;
        }
        if (state.AlertModeTimer != 0 && host.PerimeterDistance(0) < 350)
        {
            state.Stage = 17;
            state.Substate = 0;
            return;
        }
        if (state.AlertModeTimer != 0 && host.PerimeterDistance(0) < 1500)
        {
            state.Stage = 9;
            state.Substate = 0;
            return;
        }
        SeekWithoutCombat();
    }

    private void LatchIdleAnim()
    {
        host.RunAnimation(6);
        state.SetupLow = host.IdleSetupLow;
        state.SetupHigh = host.IdleSetupHigh;
        state.Substate = 10;
    }

    private void SeekWithoutCombat()
    {
        state.BehaviorFlags &= unchecked(~8u);
        if (host.PlayerSpecialState == 0 && state.AlertModeTimer != 0 && (state.BehaviorFlags & 2) == 0)
            state.AimYaw = FixedAim.Calculate(host.Body.Position, host.PlayerPosition).Yaw & 0xFFF;
        state.BehaviorFlags &= unchecked(~3u);
        host.CycleAnimation(unchecked((byte)state.SetupHigh));
        // Utils_GetVecFromMagDir(this+872, 1, {0, AimYaw, 0}).
        state.UnitDirection = FixedAim.Velocity(1, new(0, unchecked((short)state.AimYaw)));
        if (host.Random(4) != 0) state.Substate = 5;
        else state.Substate++;
    }

    private static int Absolute(int value) => value < 0 ? unchecked(-value) : value;
}

/// <summary>CSimby::GetToTarget (0x4A9F00) early exits that do not need PathCheck.</summary>
public static class SimbyGetToTarget
{
    public static int TryEarlyExit(SimbyState state, ISimbyHost host, int mode)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (mode == 0)
        {
            // Scale unit by 1000, add to position, then SetUpJumpPath (0x4A9A10).
            FixedVector3 scaled = state.UnitDirection * 1000;
            state.TargetPosition = host.Body.Position + scaled;
            return TrySetupJumpPath(state, host);
        }
        if (mode == 2 && (state.BehaviorFlags & 0x400000) != 0) return 0;
        throw new NotSupportedException($"CSimby::GetToTarget mode {mode} (0x4A9F00) is not ported yet.");
    }

    /// <summary>sub_4A9A10 early exits; PathCheck success paths stay NotSupported.</summary>
    public static int TrySetupJumpPath(SimbyState state, ISimbyHost host)
    {
        if ((state.BehaviorFlags & 0x400000) != 0) return 0;
        if (host.HasSupportBody) return 0;
        if (unchecked((uint)FixedAim.Distance(host.Body.Position, state.TargetPosition)) < 0xC8) return 0;
        if (state.Stage != 2)
        {
            if (Absolute(host.Body.Position.Y - host.PlayerPosition.Y) < 409600 && host.PerimeterDistance(3) < 500)
                return 0;
        }
        int ground = host.GroundHeight(state.TargetPosition, 280, 280, 0);
        if (ground == -1) return 0;
        throw new NotSupportedException(
            "CSimby GetToTarget/SetUpJumpPath PathCheck (0x403310) and RegisterTargetPos (0x4A2680) are not ported yet.");
    }

    private static int Absolute(int value) => value < 0 ? unchecked(-value) : value;
}
