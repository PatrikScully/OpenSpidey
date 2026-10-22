using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
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
    FixedVector3 PlayerPosition { get; set; }
    short PlayerHealth { get; }
    byte PlayerSpecialState { get; }
    byte PlayerInputLock { get; }
    bool HasProcedures { get; }
    bool HasSupportBody { get; }
    /// <summary>Words at 0x5521B8 / 0x5521BC used by MoveFreely idle latch (513, 0).</summary>
    int IdleSetupLow { get; }
    int IdleSetupHigh { get; }
    /// <summary>Words at 0x5521C8 / 0x5521CC loaded once the Simby hangs from a ceiling.</summary>
    int CeilingSetupLow { get; }
    int CeilingSetupHigh { get; }
    /// <summary>CBaddy field_1F4 / ActorWaypointRoute.CurrentNode.</summary>
    int CurrentWaypoint { get; }
    /// <summary>Live CBaddy field_2F0 mirrored from the waypoint route.</summary>
    ushort WaypointFlags { get; set; }
    int GroundHeight(FixedVector3 position, int above, int below, int flags);
    /// <summary>Utils_GetGroundHeightEx style query used by DropDown (writes support into field_2A4).</summary>
    int GroundHeightWithSupport(FixedVector3 position, int above, int below, out bool hasSupport);
    /// <summary>Utils_GetGroundHeight with field_2A4 as the support out-parameter (HasSupportBody afterwards).</summary>
    int UpdateGroundSupport(FixedVector3 position, int above, int below);
    /// <summary>Support body CBody::mVel.vy (field_2A4 + 0x64).</summary>
    int SupportVelocityY { get; }
    /// <summary>CBaddy::SetHeight; non-zero means the body stayed on a surface.</summary>
    int SetHeight(bool initial, int above, int below);
    FixedVector3 WaypointPosition(int node);
    void RunAnimation(int animation);
    void CycleAnimation(int animation);
    void Neutralize();
    void BuildTransform();
    void ProcessProcedures();
    void DoPhysics(int mode);
    void RunTimer(ref int timer);
    bool TryAdvanceWaypoint();
    void SpawnEmergeEffect();
    void PlayPositionalSound(int id);
    int Random(int bound);
    /// <summary>new CSimbySlimeBase(position, angles, flags) with byte 0x3A = 1; scaleUp sets field_A4 = 32.</summary>
    void CreateSlimeQuad(FixedVector3 position, int flags, bool scaleUp);
    void DestroySlimeQuad();
    /// <summary>CSimbySlimeBase_SetPos (0x4A4ED0) plus CQuadBit::SetTint on the base and its three quads.</summary>
    void UpdateSlimeQuad(FixedVector3 position, bool grounded, byte red, byte green, byte blue);
    /// <summary>Utils_LineOfSight(from, to, &amp;hit, 0); true when clear.</summary>
    bool LineOfSight(FixedVector3 from, FixedVector3 to, out FixedVector3 hit);
    /// <summary>word_6B4C50: hit normal left in the shared Utils SLineInfo by the last blocked Utils_LineOfSight.</summary>
    FixedVector3 LastLineHitNormal { get; }
    /// <summary>M3dColij_InitLineInfo + M3dZone_LineToItem(mode 1) on a private SLineInfo.</summary>
    bool LineToItem(FixedVector3 start, FixedVector3 end, out int hitY);
    /// <summary>Utils_CheckObjectCollision(from, to, MechList, this) != null.</summary>
    bool CheckObjectCollision(FixedVector3 from, FixedVector3 to);
    /// <summary>CBaddy::PathCheck (0x403310) reading SimbyState.CombatFlags as field_2A8.</summary>
    ActorPathCheckResult PathCheck(FixedVector3 from, FixedVector3 to, bool wantHitPoint, int groundRange, FixedVector3 initialHit);
    /// <summary>CBaddy::BumpedIntoSpidey (0x404470).</summary>
    bool BumpedIntoSpidey(int radius);
    /// <summary>CBody::mPlayerDist (0xE4), refreshed by the actor list update.</summary>
    uint PlayerDistance { get; }
    ushort PlayerMinorRadius { get; }
    /// <summary>CSuper::mFrame (0x128).</summary>
    int AnimationFrame { get; }
    /// <summary>CBaddy field_212 ground-query interval.</summary>
    byte HeightQueryInterval { set; }
    /// <summary>CItem transform rotation at 0x164, as last built.</summary>
    FixedJointTransform TransformBasis { get; }
    /// <summary>new CAIProc_MoveTo(this, {target, speed, turnRate, tolerance}, mask).</summary>
    void AttachMoveTo(FixedVector3 target, int speed, int turnRate, int tolerance, uint mask);
    /// <summary>new CAIProc_LookAt(this, MechList, null, mask, rate, tolerance).</summary>
    void AttachLookAtPlayer(uint mask, int rate, int tolerance);
    void AttachLookAtPoint(FixedVector3 point, uint mask, int rate, int tolerance);
    void AttachLookAtYaw(int yaw, uint mask, int rate, int tolerance);
    /// <summary>M3dUtils_GetDynamicHookPosition (0x453EE0) on the current pose.</summary>
    FixedVector3 DynamicHookPosition(ActorHook hook);
    /// <summary>CSuper::ApplyPose(0x5554A0) with SimbyState.Joints as the link corrections.</summary>
    void ApplyPose();
    /// <summary>new CAIProc_MonitorAttack(this, startFrame, hookMask, frames, completionMask).</summary>
    void AttachAttackMonitor(int startFrame, uint hookMask, int frames, uint completionMask);
    /// <summary>new CAIProc_StateSwitchSendMessage(this, message).</summary>
    void AttachStateSwitchMessage(int message);
    /// <summary>Mem_RecoverPointer(field_104) trap web is live and its burn counter exceeds 40.</summary>
    bool TrapWebBurning();
    /// <summary>CTrapWebEffect::Burst on the recovered field_104 web.</summary>
    void BurstTrapWeb();
    bool PlayerCanTalk();
    /// <summary>Redbook_XAPlayPos at the player, then CBody::AttachXA on the player.</summary>
    bool PlayAndAttachPlayerXA(int bank, int slot, int volume);
    /// <summary>new CSimbyShot(position) (0x4A5FD0).</summary>
    void SpawnShot(FixedVector3 position);
    /// <summary>CSimbySlimeBase scale-down-and-die request (fields 0xA4/0x9C/0x3A).</summary>
    void ReleaseSlimeQuad();
    /// <summary>CPlayer::NotifyKill(mType).</summary>
    void NotifyPlayerKill();
    /// <summary>CBaddy::Die(0) (0x404320).</summary>
    void Die();
    /// <summary>Simby_SplattyDeath (0x4A7050).</summary>
    void SplattyDeath();
    /// <summary>new CSimbyFireDeath(this) (0x4A3640).</summary>
    void SpawnFireDeath();
    /// <summary>A "sym_base" CSimbyBase item; following bases track field_808 scale until released.</summary>
    int SpawnSymBase(FixedVector3 position, ActorAngles angles, bool following);
    /// <summary>Clears field_808/field_804 so the sym_base shrinks out on its own.</summary>
    void ReleaseSymBase(int handle);
    /// <summary>Deletes a following sym_base outright (its destructor).</summary>
    void DeleteSymBase(int handle);
    void DeleteRibbon(int handle);
    /// <summary>Sets field_804 = 1 (follow the owner handle) and field_808 = scale.</summary>
    void StartSymBaseFollowing(int handle, int scale);
    /// <summary>Sets field_808, the scale a following sym_base eases toward.</summary>
    void SetSymBaseTarget(int handle, int scale);
    /// <summary>Clears field_804 only, keeping field_808.</summary>
    void StopSymBaseFollowing(int handle);
    void SetSymBaseItemScale(int handle, short scale);
    void SetSymBaseAngleX(int handle, short angle);
    /// <summary>CSimpleTexturedRibbon(faces) with SetTexture(gTextures+20), SetOpaque, SetRGB and SetWidth.</summary>
    int CreateRibbon(int faces, byte red, byte green, byte blue, int width);
    void SetRibbonPoint(int handle, int index, FixedVector3 position);
    void SetRibbonPointY(int handle, int index, int y);
    /// <summary>CSimpleTexturedRibbon::SetWidthi.</summary>
    void SetRibbonWidth(int handle, int index, int width);
    /// <summary>Frame count of a clip in this model's animation table (dword_6B245C).</summary>
    int ClipFrameCount(int clip);
    /// <summary>Web_GetGroundY (0x4F54A0).</summary>
    int WebGroundY(FixedVector3 position);
    /// <summary>new CSimbyDrop(position, velocity, groundY, flags) (0x4A57E0).</summary>
    void SpawnDrop(FixedVector3 position, FixedVector3 velocity, int groundY, int flags);
    ThugMessage? FirstMessage { get; }
    /// <summary>Player CSuper anim id (0x12A), CPlayer field_E1C action state and heading.</summary>
    int PlayerAnimationId { get; }
    int PlayerActionState { get; set; }
    short PlayerYaw { get; }
    FixedVector3 PlayerVelocity { set; }
    /// <summary>CPlayer_GrabUpdate (0x4BB810) moving the held Simby; false once the grab breaks.</summary>
    bool GrabUpdate(ref FixedVector3 position, ref short yaw);
    /// <summary>Swaps CPlayer field_554 (AI function); the first swap saves the player's own AI.</summary>
    void SetPlayerAi(SimbyPlayerAi ai);
    void RestorePlayerAi();
    void HitPlayer(ActorHit hit);
    void PlayPlayerSingleAnimation(int animation);
    /// <summary>CPlayer::CreateCombatImpactEffect.</summary>
    void CreatePlayerImpactEffect(FixedVector3 position, int flags);
    /// <summary>new CMessage(this, this, code, 0).</summary>
    void SendSelfMessage(int code);
    /// <summary>CBaddy::CleanUpMessages(0, 0).</summary>
    void CleanUpMessages();
}

/// <summary>CSimby::AI (0x4AE3D0) with Initialize, Emerge and MoveFreely leaves.</summary>
public sealed partial class SimbyBrain(SimbyState state, SimbyWorldState world, SimbyStateFlags flags, ISimbyHost host)
{
    private readonly SimbyPathPlanner planner = new(state, world, host);
    public SimbyPathPlanner Planner => planner;

    public void Advance()
    {
        if (state.LevelId == 0x1104 && planner.DistanceToPlayer(2) > 350)
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

        if ((state.BehaviorFlags & 0x70000) != 0 && (flag & 0x8000) == 0) CleanUpSlink();

        if (state.AttackMask != 0)
        {
            if ((flag & 0x80) == 0) SimbyReactions.ClearAttackData(state, world);
            else if (state.AlertModeTimer < 2400) state.AlertModeTimer = 2400;
        }

        if ((flag & 4) == 0) host.DoPhysics(0);
        // Original runs AIProcList::Execute then CleanUpAIProcList(0); Neutralize is only inside leaves.
        if (host.HasProcedures) host.ProcessProcedures();
        ProcessMessages(flags);

        // AI zeroes field_3A0 every frame before the state switch; leaves OR display bits back in.
        state.Field3A0 = 0;

        // Messages may have switched state; the original re-reads CheckStateFlags for each test below.
        flag = flags.Check(state.Stage);
        if (state.Substate == 0 || (flag & 0x8000) != 0)
        {
            if ((flag & 0x800) != 0 && state.Health > 0) state.BodyFlags |= 0x10;
            else state.BodyFlags &= ~0x10u;
        }
        if ((flag & 0x10) != 0) state.CombatFlags |= 0x10000;
        else state.CombatFlags &= ~0x10000u;

        if (state.Stage == 25)
        {
            state.BodyFlags |= 0x10;
            state.Stage = 4;
            state.Substate = 0;
        }
        else if (state.Health > 0 && host.PlayerHealth > 0 && host.PlayerSpecialState == 0)
        {
            flag = flags.Check(state.Stage);
            if ((flag & 0x40) != 0) DetermineFightState();
            if ((host.AttackClock & 3) == 0 && (flag & 0x400) != 0
                && Absolute(host.Body.Position.Y - host.PlayerPosition.Y) < 409600
                && planner.DistanceToPlayer(4) < 350)
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
                if ((state.BehaviorFlags & 0x10000) != 0) return;
                FollowWaypoints();
                break;
            case 4:
                if ((state.BehaviorFlags & 0x10000) != 0) return;
                MoveFreely();
                break;
            case 5:
                if ((state.BehaviorFlags & 0x10000) != 0) return;
                WalkToTarget();
                break;
            case 6:
            case 16:
            case 24:
                SlinkToTarget();
                break;
            case 7:
                if ((state.BehaviorFlags & 0x10000) != 0) return;
                ClimbToTarget();
                break;
            case 8:
            case 20:
                DropDown();
                break;
            case 9:
                MoveInForAttack();
                break;
            case 10:
                TakeHit();
                state.AttackRelatedSnapshot = host.AttackClock;
                break;
            case 11:
                GetLaunched();
                break;
            case 12:
            case 13:
                Trapped();
                break;
            case 14:
                if (state.AlertModeTimer < 2400) state.AlertModeTimer = 2400;
                state.CombatFlags |= 0x20;
                if ((state.CombatFlags & 0x40) != 0)
                {
                    GettingGrabbed();
                    break;
                }
                state.Stage = 4;
                state.Substate = 0;
                break;
            case 17:
                AttackPlayer();
                break;
            case 18:
                if ((state.BehaviorFlags & 0x10000) != 0) return;
                Shoot();
                break;
            case 19:
                DieWhileFalling();
                break;
            case 21:
                SlideToAttack();
                break;
            case 22:
                DieSimby();
                break;
            case 23:
                DieFireyDeath();
                break;
            default:
                throw new InvalidDataException(
                    $"Unknown CSimby AI state {state.Stage} (substate {state.Substate}); original dispatch 0x4AE3D0 only prints a diagnostic.");
        }

        ReleaseHeldPlayer();
        if ((state.Field3A0 & 0x10) != 0) host.ItemFlags |= 1;
        else host.ItemFlags = unchecked((ushort)(host.ItemFlags & ~1));
        if ((state.Field3A0 & 2) != 0) state.Field194 = (state.Field194 & ~0x8000u) | 7;
        else state.Field194 = (state.Field194 & ~7u) | 0x8000;

        if ((state.BehaviorFlags & 0x80) != 0)
            host.Body.Angles = host.Body.Angles with { X = unchecked((short)(host.Body.Angles.X + 2048)) };

        if (state.SlimeHandle != 0) UpdateSlime((state.BehaviorFlags & 0x80) != 0);

        if ((host.ItemFlags & 4) == 0 || (state.BehaviorFlags & 0x2000) != 0)
            host.BuildTransform();
        else
            DisplayStretchedPose();
    }

    /// <summary>CSimby::AI message loop (0x4AE7xx) followed by CleanUpMessages(0, 0).</summary>
    private void ProcessMessages(SimbyStateFlags flags)
    {
        ThugMessage? message = host.FirstMessage;
        if (message is null) return;
        for (; message is not null; message = message.Next)
        {
            if ((flags.Check(state.Stage) & 0x200) != 0 && message.Code < 11)
            {
                message.Flags |= 1;
                continue;
            }
            switch (message.Code)
            {
                case 5:
                    if ((flags.Check(state.Stage) & 0x10) != 0) continue;
                    if (state.Stage != 12)
                    {
                        host.Neutralize();
                        state.Stage = 12;
                        state.Substate = 0;
                    }
                    break;
                case 0xA:
                    if (state.AlertModeTimer < 2400) state.AlertModeTimer = 2400;
                    if ((flags.Check(state.Stage) & 0xC0) == 0x40)
                    {
                        state.TargetPosition = message.Vector0;
                        state.Stage = 4;
                        state.Substate = 0;
                    }
                    break;
                case 0xE:
                    host.BurstTrapWeb();
                    break;
                case 0x11:
                    if (state.SymBase != 0) host.ReleaseSymBase(state.SymBase);
                    state.SymBase = 0;
                    if (state.ClimbSymBase != 0) host.ReleaseSymBase(state.ClimbSymBase);
                    state.ClimbSymBase = 0;
                    if (state.Ribbon != 0)
                    {
                        host.DeleteRibbon(state.Ribbon);
                        state.Ribbon = 0;
                    }
                    break;
                case 0x14:
                {
                    if (state.Stage == 22 || message.Age++ >= 8)
                    {
                        state.Joints[10] = state.Joints[10] with { RotationZ = 0 };
                        state.Joints[7] = state.Joints[7] with { RotationZ = 0 };
                        host.ItemFlags = unchecked((ushort)(host.ItemFlags & ~4));
                        state.BehaviorFlags &= ~0x2000u;
                        break;
                    }
                    host.ItemFlags |= 4;
                    state.BehaviorFlags |= 0x2000;
                    RawJointPose upper = state.Joints[10], lower = state.Joints[7];
                    if (message.Age >= 4)
                    {
                        state.Joints[10] = upper with { RotationZ = unchecked((short)(upper.RotationZ >> 1)) };
                        state.Joints[7] = lower with { RotationZ = unchecked((short)(lower.RotationZ >> 1)) };
                    }
                    else
                    {
                        state.Joints[10] = upper with { RotationZ = unchecked((short)(upper.RotationZ + (800 >> message.Age))) };
                        state.Joints[7] = lower with { RotationZ = unchecked((short)(lower.RotationZ - (800 >> message.Age))) };
                    }
                    host.ApplyPose();
                    continue;
                }
            }
            message.Flags |= 1;
        }
        host.CleanUpMessages();
    }

    private void UpdateSlimePresence()
    {
        int absY = Absolute(host.Body.Position.Y - host.PlayerPosition.Y);
        bool near = (short)host.ItemFlags >= 0 || (absY <= 0x578000 && host.PlayerDistance <= 0x9C4);
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
        if (state.SlimeRespawnTimer == 0 && state.SlimeHandle == 0) CreateStandingSlime();
    }

    /// <summary>CSimby::AI tail: slime under (or above, when inverted) the body, tinted like the Simby.</summary>
    private void UpdateSlime(bool inverted)
    {
        int offset = inverted ? unchecked(-4096 * state.SurfaceClearance) : state.SurfaceClearance << 12;
        if ((host.ItemFlags & 0x200) != 0) offset = unchecked(state.Scale * (offset >> 12));
        FixedVector3 position = host.Body.Position with { Y = unchecked(offset + host.Body.Position.Y) };
        bool grounded = !host.HasSupportBody && (flags.Check(state.Stage) & 0x1000) != 0;
        if ((host.ItemFlags & 0x400) == 0)
        {
            host.UpdateSlimeQuad(position, grounded, 0x80, 0x80, 0x80);
            return;
        }
        host.UpdateSlimeQuad(position, grounded, Bright(state.Rgb), Bright(state.Rgb >> 8), Bright(state.Rgb >> 16));

        static byte Bright(int channel)
        {
            int value = 4 * (channel & 0xFF);
            return value > 0xFF ? (byte)0xFF : (byte)value;
        }
    }

    /// <summary>CSimbySlimeBase(pos + clearance, angles, 0x100) as created by AI and Emerge.</summary>
    private void CreateStandingSlime()
    {
        host.CreateSlimeQuad(host.Body.Position with { Y = unchecked(host.Body.Position.Y + (state.SurfaceClearance << 12)) }, 0x100, false);
        state.SlimeHandle = 1;
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
        int distance = planner.DistanceToPlayer(2);
        state.FightProbeThreshold = distance < 500 ? 5 : distance < 1500 ? 10 : 20;
        state.FightProbeAccumulator = unchecked(state.FightProbeAccumulator + host.ElapsedTicks);
        if (state.FightProbeAccumulator < state.FightProbeThreshold) return;
        state.FightProbeAccumulator = 0;
        if (!host.LineOfSight(host.Body.Position, host.PlayerPosition, out _)) return;
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
        int distance = planner.DistanceToPlayer(2);
        if (distance < state.Field30C) return true;
        if (distance > state.Field3B8) return false;
        int delta = FixedAim.Calculate(host.Body.Position, host.PlayerPosition).Yaw - host.Body.Angles.Y;
        if (delta < -2048) delta += 4096;
        else if (delta > 2048) delta -= 4096;
        return Absolute(delta) <= (state.Field3C0 >> 1);
    }

    /// <summary>CSimby::DropDown (0x4AC180).</summary>
    public void DropDown()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                ReleaseSlime();
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
                    int ground = host.UpdateGroundSupport(host.Body.Position, 0, 2000);
                    if (ground == -1)
                    {
                        state.Stage = 19;
                        state.Substate = 0;
                        break;
                    }
                    if (host.HasSupportBody)
                    {
                        state.Field230 = 122880;
                        state.BehaviorFlags &= ~0x4000u;
                        state.Substate = 5;
                        break;
                    }
                    int landY = unchecked(ground - (state.SurfaceClearance << 12));
                    state.ClimbTarget = host.Body.Position with { Y = landY };
                    if (state.Stage == 20 || Absolute(unchecked(landY - host.Body.Position.Y)) > 81920)
                    {
                        host.CycleAnimation(36);
                        state.Field230 = 122880;
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
                int speed = state.Field230;
                int y = unchecked(host.Body.Position.Y + speed);
                state.Field230 = unchecked(speed + 122880);
                state.Field3A0 |= 2;
                host.Body.Position = host.Body.Position with { Y = y };
                if (y <= state.ClimbTarget.Y) break;
                host.Body.Position = state.ClimbTarget;
                host.AttachStateSwitchMessage(17);
                if (state.SymBase == 0)
                {
                    state.SymBase = SpawnSymBase(host.Body.Position);
                    host.StartSymBaseFollowing(state.SymBase, 300);
                }
                host.RunAnimation(38);
                state.ClipScaleRate = unchecked((int)(0x1000u / (ushort)host.ClipFrameCount(host.AnimationId)));
                state.Substate++;
                break;
            }
            case 2:
                if (state.SymBase != 0) host.SetSymBaseTarget(state.SymBase, unchecked(state.ClipScaleRate * host.AnimationFrame));
                if (host.AnimationFinished)
                {
                    // The original clears the base's scale without a null check here.
                    if (state.SymBase == 0) throw new InvalidOperationException("CSimby::DropDown substate 2 requires its landing sym_base.");
                    host.SetSymBaseTarget(state.SymBase, 0);
                    state.SymBase = 0;
                    host.SetHeight(true, 100, 150);
                    state.Substate++;
                }
                else state.Field3A0 |= 2;
                break;
            case 3:
            {
                state.DropSampleY = host.Body.Position.Y;
                host.SetHeight(false, 100, 600);
                if (state.SlimeHandle == 0)
                {
                    host.CreateSlimeQuad(host.Body.Position with { Y = unchecked((state.SurfaceClearance << 12) + host.Body.Position.Y) }, 0, true);
                    state.SlimeHandle = 1;
                }
                int moved = unchecked(state.DropSampleY - host.Body.Position.Y);
                if (host.HasSupportBody)
                {
                    int support = host.SupportVelocityY;
                    int sign = support >> 31;
                    int band = unchecked(((10 - sign) << 12) + ((support << 12) ^ ((support >> 19) & unchecked((int)0xFFFFF000))));
                    if (Absolute(moved) > band) break;
                }
                else if (Absolute(moved) >= 4096) break;
                state.Substate = 0;
                state.Stage = 4;
                break;
            }
            case 5:
            {
                int ground = host.UpdateGroundSupport(host.Body.Position, 0, 2000);
                if (ground == -1)
                {
                    state.Stage = 19;
                    state.Substate = 0;
                    break;
                }
                int landY = unchecked(ground - (state.SurfaceClearance << 12));
                if (unchecked((short)host.AnimationId) != 36 && Absolute(unchecked(landY - host.Body.Position.Y)) > 81920)
                {
                    host.CycleAnimation(36);
                    state.BehaviorFlags |= 0x4000;
                }
                int speed = state.Field230;
                int y = unchecked(speed + host.Body.Position.Y);
                state.Field230 = unchecked(speed + 122880);
                host.Body.Position = host.Body.Position with { Y = y };
                state.Field3A0 |= 2;
                if (y > landY)
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
                    if (state.SlimeHandle == 0)
                    {
                        host.CreateSlimeQuad(host.Body.Position, 0, true);
                        state.SlimeHandle = 1;
                    }
                    state.Stage = 4;
                    state.Substate = 0;
                }
                break;
            case 15:
                if (host.AnimationFrame >= 10) state.Field3A0 |= 2;
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
                    _ = planner.SetUpJumpPath();
                    host.WaypointFlags = unchecked((ushort)(host.WaypointFlags & ~0x20));
                    state.RouteFlags = host.WaypointFlags;
                    state.Stage = 24;
                    state.Substate = 0;
                    break;
                }
                host.AttachMoveTo(host.WaypointPosition(host.CurrentWaypoint), 200, 40, 500, 1);
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
                int ground = host.UpdateGroundSupport(host.Body.Position, 300, 300);
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
                if (planner.DistanceToPlayer(0) <= state.Field3BC)
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
                state.BodyFlags |= 0x10;
                if (!host.AnimationFinished && host.AnimationId != unchecked((byte)state.SetupHigh)) break;
                if (state.SlimeHandle == 0) CreateStandingSlime();
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

    /// <summary>CSimby::MoveInForAttack (0x4A9830).</summary>
    public void MoveInForAttack()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                if (unchecked((byte)(state.SetupHigh >> 8)) != 0)
                {
                    LatchIdleAnim();
                    break;
                }
                if (planner.PickAttackSlot()) state.Substate++;
                else
                {
                    state.Stage = 4;
                    state.Substate = 0;
                }
                break;
            case 1:
                if (ActorLifecycle.Distance(host.Body.Position, state.TargetPosition) >= 0x32 && planner.AttemptAttackMethod(0) != 0)
                {
                    state.Stage = 21;
                    state.Substate = 0;
                }
                else state.Substate++;
                break;
            case 2:
                if (planner.AttemptAttackMethod(1) != 0)
                {
                    short yaw = FixedAim.Calculate(host.Body.Position, state.TargetPosition).Yaw;
                    state.AimYaw = yaw;
                    state.UnitDirection = FixedAim.Velocity(1, new(0, yaw));
                    state.Stage = 16;
                    state.Substate = 0;
                }
                else
                {
                    state.Stage = 4;
                    state.Substate = 0;
                    state.BehaviorFlags |= 8;
                }
                break;
            case 10:
                if (host.AnimationFinished) state.Substate = 0;
                break;
            default:
                throw new InvalidDataException($"Unknown CSimby MoveInForAttack substate {state.Substate}.");
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
                    && planner.DistanceToPlayer(2) < 1500
                    && host.LineOfSight(host.Body.Position, host.PlayerPosition, out _)
                    && planner.RegisterTargetPos(host.Body.Position))
                {
                    state.Stage = 18;
                    state.Substate = 0;
                    return;
                }
                MoveFreelyIdleOrSeek();
                return;
            case 1:
                if (planner.GetToTarget(0) == 0) state.Substate++;
                else { state.Stage = 6; state.Substate = 0; }
                return;
            case 2:
            case 5:
                if (planner.GetToTarget(1) == 0) state.Substate++;
                else { state.Stage = 5; state.Substate = 0; }
                return;
            case 3:
                if (planner.GetToTarget(2) != 0)
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
                if (planner.GetToTarget(0) != 0) { state.Stage = 6; state.Substate = 0; }
                else state.Substate = 3;
                return;
            default:
                throw new InvalidDataException($"Unknown CSimby MoveFreely substate {state.Substate}.");
        }
    }

    private void MoveFreelyIdleOrSeek()
    {
        if ((state.BehaviorFlags & 0x200000) != 0 && !host.LineOfSight(host.Body.Position, host.PlayerPosition, out _))
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
        if (state.AlertModeTimer != 0 && planner.DistanceToPlayer(0) < 350)
        {
            state.Stage = 17;
            state.Substate = 0;
            return;
        }
        if (state.AlertModeTimer != 0 && planner.DistanceToPlayer(0) < 1500)
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
