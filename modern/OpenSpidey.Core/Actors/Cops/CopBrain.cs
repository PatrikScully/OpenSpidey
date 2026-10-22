using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Cops;

/// <summary>Native effects around the recovered CCop AI.</summary>
public interface ICopHost : IActorMotionHost, IActorAnimationCycleHost, IActorHeightAdjustmentHost
{
    /// <summary>0x5FCCF4.</summary>
    int AttackClock { get; }
    ThugPlayerState Player { get; }
    /// <summary>CPlayer 0xEA8.</summary>
    short PlayerSurfaceClearance { get; }
    ActorBodyMotion? PlayerBody { get; }
    int AnimationId { get; }
    int AnimationFrame { get; }
    byte AnimationFinished { get; set; }
    /// <summary>CItem 0x4.</summary>
    ushort ItemFlags { get; set; }
    bool HasMessages { get; }
    /// <summary>CBaddy 0x2F0, reset by GetNextWaypoint.</summary>
    uint RouteFlags { get; set; }
    void RunAnimation(ushort clip, int from = 0, int to = -1);
    void BuildTransform();
    /// <summary>0x42F470.</summary>
    void ProcessMessages();
    int GroundHeight(FixedVector3 position, int above, int below, int flags);
    /// <summary>0x402F60.</summary>
    bool GetNextWaypoint();
    /// <summary>Trig_GetPosition (0x4E3940) of the 0x1F4 waypoint node.</summary>
    FixedVector3 WaypointPosition();
    /// <summary>0x1A8[index].</summary>
    FixedVector3 GetPathPoint(int index);
    /// <summary>Utils_LineOfSight (0x4E67A0) with no hit output and flags 0.</summary>
    bool LineOfSight(FixedVector3 from, FixedVector3 to);
    /// <summary>CBaddy::PathCheck with no hit output; <paramref name="globalCollisionFlag"/> is 0x5FBEE0 during the call.</summary>
    int PathCheck(FixedVector3 from, FixedVector3 to, int clearance, bool globalCollisionFlag);
    /// <summary>0x4048F0.</summary>
    bool AddPointToPath(FixedVector3 point, int argument);
    /// <summary>0x403B60.</summary>
    FixedVector3 GetWaypointNearTarget(FixedVector3 target, int radius, byte phase);
    /// <summary>0x403230.</summary>
    void SendSignal();
    /// <summary>CCop virtual Hit (0x429C60) with an SHitInfo of flags, damage and a zero vector.</summary>
    void ApplyHit(byte flags, short damage);
    /// <summary>0x42B270.</summary>
    void WarnOtherCops();
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitch);
    /// <summary>CBaddy::MakeSpriteRing (0x403FC0).</summary>
    void MakeSpriteRing(FixedVector3 offset);
    /// <summary>Mem_RecoverPointer on the 0x34C handle.</summary>
    ActorBodyMotion? RecoverAcknowledgeTarget();
    /// <summary>CCop::DrawLaserSiteThingie (0x42BB50). Returns 0 when the shot misses or is not ready.</summary>
    int DrawLaserSite(bool checkPlayer);
    /// <summary>Deletes the 0x380 CGPolyLine handle.</summary>
    void ClearLaser();
    void ApplyPlayerHit(byte flags, short damage);
    void SendSelfMessage(int code);
    /// <summary>Attaches CAIProc_MonitorAttack for the melee swing.</summary>
    void StartAttackMonitor(int startFrame, int attackMask, int frameCount, uint completionMask);
    /// <summary>CPlayer::NotifyKill (0x4BBC60) on the player singleton.</summary>
    void NotifyKill(int actorType);
    /// <summary>Pulses the trigger links of node 0xDE; the 0x211 latch and 0xFFFF check stay with the caller.</summary>
    void SendDeathPulse();
    /// <summary>CSuper::KillShadow (0x460570) deletes the 0xCC shadow after the caller clears body bit 8.</summary>
    void KillShadow();
    /// <summary>Removes the item once CBaddy::Die has set body bit 0x40 and item bit 1.</summary>
    void MarkDead();
    /// <summary>CBaddy::StruckGameObject (0x404AE0) from 0x2FC to the current position.</summary>
    IThugInjuryBody? StruckGameObject(int first, int second);
    /// <summary>CBaddy_ElasticCollision (0x404700).</summary>
    void ElasticCollision(IThugInjuryBody target, FixedVector3 velocity, int duration);
    /// <summary>SFX_Play (0x471C50).</summary>
    void PlayGlobalSound(ushort sound, int volume, int pan);
    /// <summary>CSuper::ApplyPose with the police (0x549264) or SWAT (0x549344) fall pose.</summary>
    void ApplyFallPose(bool police);
    /// <summary>CSuper 0x188 joints present; <see cref="JointOnePitch"/> is joint word 6.</summary>
    bool HasJoints { get; }
    short JointOnePitch { get; set; }
    /// <summary>CCop 0x384 mpThrowPoints[index], the eight whip throw points the player stores before state 16.</summary>
    FixedVector3 GetThrowPoint(int index);
    /// <summary>CBaddy_GetClosest (0x403AD0, CCop vtable slot 13) with radius 0.</summary>
    CopState? FindClosest(int actorType);
    /// <summary>A CMessage from this cop to <paramref name="target"/>.</summary>
    void SendMessage(CopState target, int code);
    /// <summary>CBaddy::PathCheck (0x403310) with a hit-point output.</summary>
    int PathCheckWithHit(FixedVector3 from, FixedVector3 to, int clearance, bool globalCollisionFlag, out FixedVector3 hit);
    /// <summary>The player's current clip (CItem 0x12A).</summary>
    int PlayerAnimationId { get; }
    /// <summary>Mem_RecoverPointer on the 0x104 trap-web handle.</summary>
    IThugTrapWeb? RecoverTrapWeb();
    /// <summary>CPlayer 0x528.</summary>
    int PlayerWebComboValue { get; }
    /// <summary>Writes CPlayer 0x534 (duration) and 0x52C (amount).</summary>
    void SetPlayerWebCombo(int duration, int amount);
    /// <summary>CPlayer 0xE1C; 0x8000000 while the player holds the grab.</summary>
    int PlayerActionState { get; }
    /// <summary>The player's yaw (CBody 0x16).</summary>
    short PlayerYaw { get; }
    /// <summary>CPlayer_GrabUpdate (0x4BB810) on this cop's position and yaw.</summary>
    bool GrabUpdate(ref FixedVector3 position, ref short yaw);
}

/// <summary>Recovered CCop::AI (0x42FA10) and the leaf states reached by an idle, patrolling or confused cop.</summary>
public sealed partial class CopBrain
{
    private readonly CopState state;
    private readonly ActorProcedureState actor;
    private readonly ActorProcedureList procedures;
    private readonly CopCombatCoordinator combat;
    private readonly ICopHost host;
    private readonly Func<int, int> random;

    public CopBrain(CopState state, ActorProcedureState actor, ActorProcedureList procedures,
        CopCombatCoordinator combat, ICopHost host, Func<int, int> random)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
        this.procedures = procedures ?? throw new ArgumentNullException(nameof(procedures));
        this.combat = combat ?? throw new ArgumentNullException(nameof(combat));
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.random = random ?? throw new ArgumentNullException(nameof(random));
        combat.Register(state, this);
    }

    private readonly ThugStateFlags flags = CopState.CreateStateFlags();
    private int elapsedTicks;

    public void Advance(int ticks)
    {
        elapsedTicks = ticks;
        if (state.State != 0) DoAISwitchLogic();
        if ((flags.Check(state.State) & 4) == 0) DoPhysics(false);
        if ((host.ItemFlags & 4) == 0) host.BuildTransform();
        PlaySounds();
        if ((state.ActionFlags & 2) != 0) state.ShadowScale = state.ShadowScale < 5 ? 0 : state.ShadowScale - 5;
        else
        {
            state.ShadowScale = unchecked((ushort)(state.ShadowScale + 10));
            if (state.ShadowScale > 50) state.ShadowScale = 50;
            state.ShadowPosition = actor.Body.Position with { Y = unchecked(actor.Body.Position.Y + (state.SurfaceClearance << 12)) };
        }
        if (host.HasMessages) host.ProcessMessages();
        state.ActionFlags = 0;
        if (procedures.Count != 0) procedures.Execute(ticks);
        actor.BehaviorFlags &= ~0x20u;
        Dispatch();
    }

    /// <summary>0x404C50. Mode 1 limits movement to the 0x1F8 counter.</summary>
    private void DoPhysics(bool limited)
    {
        actor.Body.UseLocalVelocity = (actor.BehaviorFlags & 1) != 0;
        actor.Body.LimitedMotionTicks = state.ActionCounter;
        actor.Body.Integrate(elapsedTicks, limited);
        if (limited) state.ActionCounter = actor.Body.LimitedMotionTicks;
    }

    private int Timer(int value) => Math.Max(0, unchecked(value - elapsedTicks));

    /// <summary>0x42F810.</summary>
    public void DoAISwitchLogic()
    {
        state.AlertTicks = Timer(state.AlertTicks);
        state.Timer328Ticks = Timer(state.Timer328Ticks);
        if ((state.ActionFlags & 4) != 0 && (state.AttackOwnershipFlags & 1) != 0 &&
            DistanceToPlayer(0) < (state.Type != 306 ? 300 : 200))
        {
            host.Neutralize();
            SetState(5);
        }
        if ((state.CombatFlags & 0x4000) != 0 && state.State != 14)
        {
            host.ApplyHit(4, 50);
            state.CombatFlags &= ~0x4000u;
        }
        if (state.ClosePlayerTicks != 0)
        {
            state.ClosePlayerTicks = Timer(state.ClosePlayerTicks);
            if (state.ClosePlayerTicks == 0 && DistanceToPlayer(2) < 200 &&
                ThugPerception.SourceAbs(unchecked(host.Player.Position.Y - actor.Body.Position.Y)) < 819200 &&
                (flags.Check(state.State) & 0x40) != 0 && state.AlertTicks == 0)
                SetState(3);
        }
        state.FightSuppressionTicks = Timer(state.FightSuppressionTicks);
        if (state.State == 28)
        {
            state.BodyFlags |= 0x10;
            state.TurnRate = 0;
            if (!DetermineFightState()) SetState(host.Player.UnavailableFlag != 0 && state.AlertTicks != 0 ? 25 : 2);
        }
        else if ((flags.Check(state.State) & 0x40) != 0 &&
            (unchecked(host.AttackClock + state.ActorPhase) & state.FightPollInterval) == 0)
            DetermineFightState();
        if (state.AlertTicks != 0 && (unchecked(host.AttackClock + state.ActorPhase) & 0x1F) == 0 && PlayerIsVisible())
            host.WarnOtherCops();
    }

    /// <summary>0x42E4D0.</summary>
    public bool DetermineFightState()
    {
        int oldState = state.State;
        int path = -1;
        int distance = DistanceToPlayer(2);
        ThugPlayerState player = host.Player;
        if (state.Health <= 0 || player.Health <= 0 || player.UnavailableFlag != 0) return false;
        state.FightPollInterval = distance > 2000 ? 31 : distance > 1000 ? 15 : 3;
        if (state.AlertTicks == 0 && ((distance < 200 && state.ClosePlayerTicks == 0 &&
            ThugPerception.SourceAbs(unchecked(player.Position.Y - actor.Body.Position.Y)) < 819200) || BumpedIntoSpidey(110)))
            state.ClosePlayerTicks = 120;
        if (state.FightSuppressionTicks != 0) return false;
        FixedVector3 position = actor.Body.Position;
        if (!host.LineOfSight(position, player.Position))
        {
            if ((actor.BehaviorFlags & 0x800) != 0)
            {
                actor.BehaviorFlags &= ~0x800u;
                if (PathCheck(position, state.LastSeenPlayerPosition) == 0 &&
                    host.AddPointToPath(position, 0) && host.AddPointToPath(state.LastSeenPlayerPosition, 0))
                {
                    SetState(24);
                    return true;
                }
            }
            else if (state.AlertTicks != 0 && state.State != 23 && state.State != 24)
            {
                SetState(23);
                return true;
            }
            return false;
        }
        bool changed = false;
        if (state.AlertTicks != 0 && distance < state.SightFar)
        {
            path = PathCheck(position, player.Position);
            if (path == 0) { SetState(4); changed = true; }
        }
        if (!changed && CheckSightCone())
        {
            if (state.AlertTicks == 0) { SetState(3); changed = true; }
            else if ((path == -1 ? PathCheck(position, player.Position) : path) == 0) { SetState(4); changed = true; }
            else if (state.State != 1) SetState(4);
        }
        if (!changed && player.UnavailableFlag == 0 && !combat.HasFightOwner && !player.HoldsObject &&
            (((state.CombatFlags & 0x800) != 0 && distance < state.FightClaimDistance) ||
             (state.AlertTicks != 0 && distance < 1500 &&
              (path == -1 ? PathCheck(position, player.Position) : path) != 0)))
        {
            host.Neutralize();
            combat.FightOwner = state;
            SetState(9);
        }
        if (oldState == state.State) return false;
        state.BodyFlags |= 0x10;
        host.SendSignal();
        return true;
    }

    /// <summary>CBaddy::PlayerIsVisible (0x404650), CCop vtable slot 5.</summary>
    public bool PlayerIsVisible()
    {
        if (host.Player.IsDead || !host.LineOfSight(actor.Body.Position, host.Player.Position)) return false;
        if (PathCheck(actor.Body.Position, host.Player.Position) == 0)
        {
            state.LastSeenPlayerPosition = host.Player.Position;
            actor.BehaviorFlags |= 0x800;
        }
        return true;
    }

    /// <summary>CBaddy::DistanceToPlayer (0x404170) and its 0x204/0x208 cache.</summary>
    public int DistanceToPlayer(int cacheTolerance)
    {
        if (state.DistanceSampleClock != 0 && unchecked(host.AttackClock - state.DistanceSampleClock) <= cacheTolerance)
            return state.CachedPlayerDistance;
        state.DistanceSampleClock = host.AttackClock;
        state.CachedPlayerDistance = ThugPerception.ApproximateHorizontalDistance(actor.Body.Position, host.Player.Position);
        return state.CachedPlayerDistance;
    }

    /// <summary>CBaddy::BumpedIntoSpidey (0x404470).</summary>
    private bool BumpedIntoSpidey(int maximumDistance)
    {
        if (DistanceToPlayer(4) >= maximumDistance) return false;
        int clearance = unchecked((actor.Body.Position.Y >> 12) + state.SurfaceClearance - (ushort)host.PlayerSurfaceClearance -
            (host.Player.Position.Y >> 12));
        return ThugPerception.SourceAbs(clearance) < 200;
    }

    /// <summary>CBaddy::CheckSightCone (0x404510) against the player with the 0x368/0x364/0x360/0x36C arguments.</summary>
    private bool CheckSightCone()
    {
        if (state.SightVertical == 0) return false;
        if (ThugPerception.SourceAbs(unchecked(host.Player.Position.Y - actor.Body.Position.Y)) > unchecked(state.SightVertical << 12))
            return false;
        int distance = DistanceToPlayer(2);
        if (distance < state.SightNear) return true;
        if (distance > state.SightFar) return false;
        int delta = FixedAim.Calculate(actor.Body.Position, host.Player.Position).Yaw - actor.Body.Angles.Y;
        if (delta < -2048) delta += 4096;
        else if (delta > 2048) delta -= 4096;
        return ThugPerception.SourceAbs(delta) <= (state.SightCone >> 1);
    }

    /// <summary>CBaddy::PathCheck (0x403310): 0x2A8 bit 0x2000 sets 0x5FBEE0 around the inner 0x403350 call.</summary>
    private int PathCheck(FixedVector3 from, FixedVector3 to) =>
        host.PathCheck(from, to, 55, (actor.BehaviorFlags & 0x2000) != 0);

    private void SetState(int value)
    {
        state.State = value;
        state.Substate = 0;
    }

    private void Dispatch()
    {
        switch (state.State)
        {
            case 0: Initialize(); break;
            case 1: Guard(); break;
            case 2:
            case 24:
                FollowWaypoints();
                host.SetHeight(false, 100, 600);
                break;
            case 3:
                actor.BehaviorFlags |= 0x20;
                state.AlertTicks = 300;
                Caution();
                host.SetHeight(false, 100, 600);
                break;
            case 4:
                actor.BehaviorFlags |= 0x20;
                state.AlertTicks = 300;
                CopChaseBehavior.ChasePlayer(state, actor, procedures, combat, host, random, elapsedTicks,
                    DistanceToPlayer, DetermineFightState, RunAppropriateAnim, mode => state.SetAnimMode(mode, true, RunAnimation));
                host.SetHeight(false, 100, 600);
                break;
            case 5:
                actor.BehaviorFlags |= 0x20;
                state.AlertTicks = 300;
                CopAttackBehavior.AttackPlayer(state, actor, procedures, host, DistanceToPlayer, elapsedTicks);
                host.SetHeight(false, 100, 600);
                break;
            case 9:
                actor.BehaviorFlags |= 0x20;
                state.AlertTicks = 300;
                CopRangedBehavior.ShootPlayer(state, actor, procedures, host, random, elapsedTicks,
                    mode => state.SetAnimMode(mode, true, RunAnimation), PlayerIsVisible, DistanceToPlayer);
                break;
            case 10:
                actor.BehaviorFlags |= 0x20;
                if (state.Substate == 0) { host.RunAnimation(12); state.Substate++; }
                else if (state.Substate == 1) { if (host.AnimationFinished != 0) SetState(28); }
                else throw new InvalidOperationException("Unknown substate!");
                break;
            case 11: throw new InvalidOperationException("Shouldn't be in TOSS_GRENADE state");
            case 12:
                actor.BehaviorFlags |= 0x20;
                TakeHit();
                state.AlertTicks = 300;
                break;
            case 13: BackpedalPlease(); break;
            case 14:
            case 15:
                actor.BehaviorFlags |= 0x20;
                GetYankedBySpidey();
                state.AlertTicks = 300;
                break;
            case 16: GetWhipped(); break;
            case 17:
                actor.BehaviorFlags |= 0x20;
                FlyAcrossRoom();
                break;
            case 18:
                actor.BehaviorFlags |= 0x20;
                state.AlertTicks = 300;
                GetTrapped();
                break;
            case 19:
                actor.BehaviorFlags |= 0x20;
                state.AlertTicks = 300;
                if ((actor.BehaviorFlags & 8) == 0) SetState(28);
                else if (state.Substate == 0)
                {
                    host.Neutralize();
                    host.CycleAnimation(17);
                    state.Substate++;
                }
                else if (state.Substate != 1) throw new InvalidOperationException("Unknown substate.");
                break;
            case 20:
                actor.BehaviorFlags |= 0x20;
                state.AlertTicks = 300;
                if ((actor.BehaviorFlags & 0x40) != 0)
                {
                    GettingGrabbed();
                    break;
                }
                host.PlayPositionSound(0x802D, actor.Body.Position, 0);
                SetState(13);
                break;
            case 21: Acknowledge(); break;
            case 22: Fall(); break;
            case 23: LookForPlayer(); break;
            case 25: LookConfused(); break;
            case 26: Die(false); break;
            case 27: Die(true); break;
            default: throw new InvalidOperationException("Unknown state!");
        }
    }

    private void Initialize()
    {
        if (state.Substate == 0)
        {
            if (unchecked(host.AttackClock - state.AttackRelatedSnapshot) > 10 && (host.AttackClock & 0xF) != 0) return;
            int ground = host.GroundHeight(actor.Body.Position, 300, 300, 0);
            if (ground == -1) return;
            int y = unchecked(ground - (state.SurfaceClearance << 12));
            actor.Body.Position = actor.Body.Position with { Y = y };
            state.GroundReferenceY = y;
            state.GroundHeight = ground;
            state.Substate++;
            host.SetHeight(true, 100, 600);
            host.CycleAnimation(state.IdleAnimation);
        }
        else if (state.Substate == 1)
            SetState(state.PathPointIndex != 0 || host.GetNextWaypoint() ? 2 : 1);
        else throw new InvalidOperationException("Unknown sub-state!");
    }

    /// <summary>0x42EA60.</summary>
    private void Guard()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.BodyFlags |= 0x10;
                state.Substate++;
                if (host.AnimationId != 31)
                {
                    host.RunAnimation(state.Type != 306 ? (ushort)33 : (ushort)12);
                    break;
                }
                host.AnimationFinished = 1;
                GuardWaitForClip(true);
                break;
            case 1: GuardWaitForClip(host.AnimationFinished != 0); break;
            case 2:
                if (state.PauseUpdates-- <= 0)
                {
                    host.RunAnimation(state.Type != 306 ? (ushort)32 : (ushort)7);
                    state.Substate++;
                }
                break;
            case 3:
                if (host.AnimationFinished != 0)
                {
                    host.CycleAnimation(state.IdleAnimation);
                    state.PauseUpdates = unchecked(random(30) + 20);
                    state.Substate++;
                }
                break;
            case 4:
                if (state.PauseUpdates-- <= 0) state.Substate = 0;
                break;
            default: throw new InvalidOperationException("Unknown substate!");
        }
    }

    private void GuardWaitForClip(bool finished)
    {
        if (!finished) return;
        host.CycleAnimation(31);
        state.PauseUpdates = unchecked(random(30) + 20);
        state.Substate++;
    }

    /// <summary>0x42A8B0.</summary>
    private void FollowWaypoints()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.BodyFlags |= 0x10;
                actor.BehaviorFlags &= ~0x800u;
                state.TurnRate = 0;
                FixedVector3 destination;
                if (state.PathPointIndex != 0)
                {
                    actor.BehaviorFlags &= ~0x10000000u;
                    destination = host.GetPathPoint(state.PathPointIndex);
                    host.RouteFlags = (host.RouteFlags & ~2u) | 1;
                }
                else
                {
                    actor.BehaviorFlags |= 0x10000000;
                    destination = host.WaypointPosition();
                    combat.ClearAttackFlags(state);
                }
                int speed, tolerance;
                if ((host.RouteFlags & 1) != 0)
                {
                    state.SetAnimMode(1, false, RunAnimation);
                    speed = state.IsPolice ? 200 : 144;
                    tolerance = 193;
                    host.RouteFlags &= ~1u;
                }
                else
                {
                    state.SetAnimMode(0, true, RunAnimation);
                    speed = state.IsPolice ? 120 : 48;
                    tolerance = 500;
                }
                procedures.AttachMove(new ActorMoveToProcedure(actor, destination, speed, 70, tolerance, 1));
                state.Substate++;
                break;
            case 1:
                RunAppropriateAnim();
                if ((actor.CompletionFlags & 1) == 0) return;
                actor.CompletionFlags &= ~1u;
                if (state.State == 24)
                {
                    if (state.AlertTicks != 0) { SetState(23); return; }
                    state.State = 2;
                }
                if (state.PathPointIndex != 0) state.PathPointIndex--;
                else if (!host.GetNextWaypoint()) { SetState(1); return; }
                if (state.AlertTicks != 0 && DetermineFightState()) return;
                if ((host.RouteFlags & 2) != 0)
                {
                    host.RunAnimation(state.IsPolice ? (ushort)12 : (ushort)(random(4) + 3));
                    state.Substate++;
                }
                else state.Substate = 0;
                break;
            case 2:
                if (host.AnimationFinished != 0)
                {
                    host.CycleAnimation(31);
                    state.PauseUpdates = 50;
                    state.Substate++;
                }
                break;
            case 3:
                if (state.PauseUpdates-- <= 0)
                {
                    if (state.IsPolice) { host.RunAnimation(7); state.Substate++; }
                    else state.Substate = 0;
                }
                break;
            case 4:
                if (host.AnimationFinished != 0) state.Substate = 0;
                break;
            default: throw new InvalidOperationException("Unknown substate!");
        }
    }

    private void RunAnimation(ushort clip) => host.RunAnimation(clip);

    /// <summary>CBaddy::RunAppropriateAnim (0x404B60) over the 0x294/0x298 words.</summary>
    private void RunAppropriateAnim()
    {
        uint movementFlags = actor.Body.MovementFlags;
        ushort first = (byte)state.AnimationWord294;
        ushort second = (byte)(state.AnimationWord294 >> 8);
        if ((movementFlags & 0x40000) != 0)
        {
            if (first != second && host.AnimationId != second && host.AnimationId != first)
            {
                host.RunAnimation(first);
                return;
            }
            if (first == second || host.AnimationId != first || host.AnimationFinished != 0) host.CycleAnimation(second);
        }
        else if ((movementFlags & 0x10000) != 0)
        {
            ushort clip = (byte)(state.AnimationWord294 >> 16);
            if (host.AnimationId != clip || host.AnimationFinished != 0) host.RunAnimation(clip);
        }
        else if ((movementFlags & 0x20000) != 0)
        {
            ushort clip = (byte)(state.AnimationWord294 >> 24);
            if (host.AnimationId != clip || host.AnimationFinished != 0) host.RunAnimation(clip);
        }
        else host.CycleAnimation(state.IdleAnimation);
    }

    /// <summary>0x42C570.</summary>
    private void TakeHit()
    {
        switch (state.Substate)
        {
            case 0:
                state.BodyFlags &= ~0x10u;
                state.TurnRate = 0;
                combat.ClearAttackFlags(state);
                if (state.HitKind == 1)
                {
                    state.MinorRadius = 0;
                    host.RunAnimation(22);
                    state.Substate = 2;
                }
                else
                {
                    ActorBodyMotion? player = host.PlayerBody;
                    procedures.Attach(ActorLookAtProcedure.ForBody(actor, () => player, 0, 80, 200));
                    host.RunAnimation(20);
                    state.Substate = 3;
                }
                break;
            case 1:
                if (host.AnimationFinished != 0) { state.HitKind = 0; SetState(28); }
                break;
            case 2:
                if (host.AnimationFinished != 0)
                {
                    state.MinorRadius = state.Type != 306 ? 150 : 96;
                    host.RunAnimation(23);
                    state.Substate = 1;
                }
                break;
            case 3:
                if (host.AnimationFinished != 0) SetState(28);
                break;
            default: throw new InvalidOperationException("Unknown substate!");
        }
    }

    /// <summary>0x42F310.</summary>
    private void BackpedalPlease()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.ActionCounter = 0;
                FixedVector3 position = actor.Body.Position;
                FixedVector3 player = host.Player.Position;
                FixedVector3 target = new(unchecked(position.X + 6 * (unchecked(position.X - player.X) >> 2)), position.Y,
                    unchecked(position.Z + 6 * (unchecked(position.Z - player.Z) >> 2)));
                if (host.AddPointToPath(position, 0) && host.AddPointToPath(target, 0))
                    actor.Body.Velocity = ((actor.Body.Position - host.Player.Position) >> 2) with { Y = 0 };
                host.RunAnimation(20);
                state.Substate++;
                break;
            case 1:
                DoPhysics(true);
                if (state.ActionCounter <= 0) host.Neutralize();
                if (host.AnimationFinished != 0) { host.Neutralize(); SetState(28); }
                break;
            default: throw new InvalidOperationException("Unknown substate!");
        }
    }

    /// <summary>0x42E950.</summary>
    private void Acknowledge()
    {
        switch (state.Substate)
        {
            case 0:
                state.BodyFlags |= 0x10;
                host.Neutralize();
                host.RunAnimation(2);
                ActorBodyMotion? target = host.RecoverAcknowledgeTarget();
                if (target is not null) procedures.Attach(ActorLookAtProcedure.ForBody(actor, () => target, 2, 70, 200));
                host.PlayPositionSound(0x800E, actor.Body.Position, 0);
                state.Substate++;
                break;
            case 1:
                if (host.AnimationFinished != 0) SetState(28);
                break;
            default: throw new InvalidOperationException("Unknown substate!");
        }
    }

    /// <summary>0x42F1C0.</summary>
    private void LookForPlayer()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                host.CycleAnimation(state.IdleAnimation);
                state.Substate++;
                state.ActorPhase = unchecked((byte)random(64));
                state.ActionCounter = 0;
                break;
            case 1:
                FixedVector3 origin = state.ActionCounter < 64 ? host.Player.Position : actor.Body.Position;
                FixedVector3 point = host.GetWaypointNearTarget(origin, 409600, state.ActorPhase);
                state.ActorPhase++;
                if (PathCheck(actor.Body.Position, point) == 0 &&
                    (host.AddPointToPath(point, 0) || (host.AddPointToPath(actor.Body.Position, 0) && host.AddPointToPath(point, 0))))
                    SetState(24);
                else if (state.ActionCounter++ > 128)
                {
                    state.AlertTicks = 0;
                    SetState(2);
                }
                break;
            default: throw new InvalidOperationException("Unknown substate!");
        }
    }

    /// <summary>0x429F00.</summary>
    private void Caution()
    {
        if (state.Substate != 0) throw new InvalidOperationException("Unknown substate!");
        state.TurnRate = 0;
        host.Neutralize();
        host.PlayPositionSound(unchecked((ushort)((random(3) + 14) | 0x8000)), actor.Body.Position, 0);
        if (ThugPerception.SourceAbs(unchecked(host.Player.Position.Y - state.GroundReferenceY - 0x4000)) < 409600 &&
            PlayerIsVisible())
        {
            if (!ShouldShootPlayer())
            {
                SetState(host.Player.UnavailableFlag != 0 ? 1 : 4);
            }
        }
        else if (!DetermineFightState())
            SetState(2);
    }

    /// <summary>Inlined shoot claim from CCop::Caution (0x429F00).</summary>
    private bool ShouldShootPlayer()
    {
        if (host.Player.UnavailableFlag != 0 || combat.HasFightOwner || DistanceToPlayer(2) >= 2000 ||
            host.Player.HoldsObject ||
            (DistanceToPlayer(2) <= 650 &&
             ThugPerception.SourceAbs(unchecked(host.Player.Position.Y - state.GroundReferenceY - 0x4000)) <= 409600))
            return false;
        host.Neutralize();
        combat.FightOwner = state;
        SetState(9);
        return true;
    }

    /// <summary>0x42EBD0.</summary>
    private void LookConfused()
    {
        switch (state.Substate)
        {
            case 0:
                state.ActionCounter = unchecked(random(3) + 2);
                state.Substate++;
                goto case 1;
            case 1:
                host.Neutralize();
                if (state.ActionCounter == 0)
                {
                    state.AlertTicks = 0;
                    SetState(28);
                }
                else
                {
                    state.Substate = 5;
                    state.ActionCounter--;
                }
                break;
            case 5:
                int angle = unchecked(random(1000) + 1000);
                if (random(2) != 0) angle = -angle;
                angle = unchecked(angle + actor.Body.Angles.Y);
                procedures.Attach(ActorLookAtProcedure.ForYaw(actor, angle, 2, 80, 200));
                state.Substate++;
                break;
            case 6:
                RunAppropriateAnim();
                if ((actor.CompletionFlags & 2) != 0)
                {
                    state.Substate = 1;
                    actor.CompletionFlags &= ~2u;
                }
                break;
            default: throw new InvalidOperationException("Unknown substate!");
        }
    }

    /// <summary>0x42ED60.</summary>
    private void PlaySounds()
    {
        int anim = host.AnimationId, frame = host.AnimationFrame;
        if (state.IsPolice)
        {
            switch (anim)
            {
                case 22: if (frame == 15) host.MakeSpriteRing(new(0, 0, 409600)); break;
                case 24: if (frame == 23) host.MakeSpriteRing(new(0, 0, -122880)); break;
                case 14: if (frame is 27 or 13) Step(300); break;
                case 8: if (frame is 4 or 14) Step(50); break;
                case 10: if (frame == 5) host.PlayPositionSound(0x801D, actor.Body.Position, 100); break;
            }
        }
        else
        {
            switch (anim)
            {
                case 24: if (frame == 28) host.MakeSpriteRing(new(0, 0, -204800)); break;
                case 22: if (frame == 15) host.MakeSpriteRing(new(0, 0, 204800)); break;
                case 14: if (frame is 2 or 12) Step(300); break;
                case 8: if (frame is 13 or 4) Step(50); break;
                case 10: if (frame == 3) host.PlayPositionSound(0x801D, actor.Body.Position, 100); break;
                case 11:
                    if (frame == 5) host.PlayPositionSound(unchecked((ushort)((random(2) + 30) | 0x8000)), actor.Body.Position, 0);
                    break;
            }
        }

        void Step(int pitch) => host.PlayPositionSound(unchecked((ushort)((random(4) + 32) | 0x8000)), actor.Body.Position, pitch);
    }
}
