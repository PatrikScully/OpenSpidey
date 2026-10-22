using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Cops;

/// <summary>CCop fall, flight and death leaves with the CBaddy helpers they call.</summary>
public sealed partial class CopBrain
{
    private sealed class LandingHost(CopState state) : IActorFallHost { public int LandingY => state.LandingY; }

    /// <summary>CCop::Hit (0x429C60). Returns whether the hit took over the AI state.</summary>
    public bool Hit(ActorHit hit)
    {
        if (state.State == 26)
        {
            if (state.Substate == 8)
            {
                PainSound();
                host.PlayPositionSound(0x8019, actor.Body.Position, 0);
            }
            return false;
        }
        if ((hit.Flags & 4) != 0) state.Health = unchecked((short)(state.Health - (short)hit.Damage));
        if (state.Health <= 0) SendDeathPulse();
        int attack = (hit.Flags & 2) != 0 ? hit.AttackKind : 0;
        state.AlertTicks = 300;
        if ((hit.Flags & 4) == 0 || state.Health > 0 || (hit.Flags & 8) != 0) PainSound();
        else if (state.State != 26)
        {
            host.PlayPositionSound(0x8019, actor.Body.Position, 0);
            host.PlayPositionSound(unchecked((ushort)((random(3) + 21) | 0x8000)), actor.Body.Position, 0);
        }
        if (attack == 3 && state.Health > 0)
        {
            host.SendSelfMessage(20);
            host.CycleAnimation(state.IsPolice ? (ushort)3 : (ushort)26);
            host.PlayPositionSound(0x801C, actor.Body.Position, 0);
            return false;
        }
        state.HitKind = attack;
        CopState? buddy = host.FindClosest(306) ?? host.FindClosest(320);
        if (buddy is not null && buddy.AlertTicks == 0) host.SendMessage(buddy, 7);
        if ((attack == 29 && state.State == 20 && state.Health > 0) || (flags.Check(state.State) & 0x10) != 0 ||
            (state.Health > 0 && (state.ActionFlags & 1) != 0))
            return false;
        host.Neutralize();
        state.Substate = 0;
        state.State = state.Health <= 0 ? 26 : 12;
        if ((hit.Flags & 0x18) != 0)
        {
            Slide(hit.SlideDistance, hit.SlideFrames, hit.Direction);
            host.RunAnimation(21);
        }
        return true;

        void PainSound() => host.PlayPositionSound(unchecked((ushort)((random(3) + 25) | 0x8000)), actor.Body.Position, 0);
    }

    /// <summary>CCop's hit slide (0x4297D0): sets up the state-17 flight along the hit direction.</summary>
    private void Slide(int distance, int frames, FixedVector3 direction)
    {
        if (frames == 0) throw new InvalidOperationException("Illegal to specify 0 frames for HITINFO slide frames.");
        actor.BehaviorFlags &= ~0x10u;
        state.ActionCounter = frames;
        FixedVector3 velocity = new(unchecked(distance * direction.X), 0, unchecked(distance * direction.Z));
        FixedVector3 position = actor.Body.Position;
        FixedVector3 start = position with { Y = unchecked(((state.SurfaceClearance - 20) << 12) + position.Y) };
        FixedVector3 target = start + velocity;
        velocity = new(velocity.X / state.ActionCounter, 0, velocity.Z / state.ActionCounter);
        int path = host.PathCheckWithHit(start, target, 55, (actor.BehaviorFlags & 0x2000) != 0, out FixedVector3 hit);
        if (path == 2)
        {
            int x = ThugPerception.SourceAbs(unchecked(hit.X - start.X));
            int z = ThugPerception.SourceAbs(unchecked(hit.Z - start.Z));
            int count = x <= z ? z / ThugPerception.SourceAbs(velocity.Z) : x / ThugPerception.SourceAbs(velocity.X);
            state.ActionCounter = count;
            if (count < 4) return;
            state.ActionCounter = count - 1;
            target = new(unchecked(position.X + velocity.X * state.ActionCounter), position.Y,
                unchecked(position.Z + velocity.Z * state.ActionCounter));
        }
        else if (path == 4) return;
        if (host.AddPointToPath(actor.Body.Position, 0) && host.AddPointToPath(target, 0)) actor.BehaviorFlags &= ~0x10000000u;
        else state.Health = 0;
        host.Neutralize();
        if (state.Health <= 0 && state.ActionCounter >= 8)
        {
            int delta = NormalizeYaw(FixedAim.Calculate(actor.Body.Position + velocity * state.ActionCounter, actor.Body.Position).Yaw -
                actor.Body.Angles.Y);
            if (delta < -455 || delta > 455)
            {
                state.CombatFlags |= 0x2000;
                int turn = NormalizeYaw(FixedAim.Calculate(target, actor.Body.Position).Yaw - actor.Body.Angles.Y);
                state.SlidePitchRate = 0;
                state.SlideYawRate = (turn <= 0 ? unchecked(turn + 4096) : unchecked(turn - 4096)) / state.ActionCounter;
            }
            else if (host.PlayerAnimationId is 106 or 113 or 284)
            {
                state.CombatFlags |= 0x2000;
                state.ActionCounter = unchecked(state.ActionCounter * 2);
                velocity /= 2;
                state.SlidePitchRate = 4096 / state.ActionCounter;
                state.SlideYawRate = 0;
                FixedVector3 look;
                if (random(2) != 0)
                {
                    state.SlidePitchRate = unchecked(-state.SlidePitchRate);
                    look = actor.Body.Position - velocity * unchecked(2 * state.ActionCounter);
                }
                else look = actor.Body.Position + velocity * unchecked(2 * state.ActionCounter);
                procedures.Attach(ActorLookAtProcedure.ForPoint(actor, look, 0, 80, 200));
            }
        }
        SetState(17);
        actor.Body.Velocity = velocity;
    }

    private static int NormalizeYaw(int delta) => delta < -2048 ? delta + 4096 : delta > 2048 ? delta - 4096 : delta;

    /// <summary>CCop::FlyAcrossRoom (0x42CA70).</summary>
    private void FlyAcrossRoom()
    {
        state.PreviousPosition = actor.Body.Position;
        switch (state.Substate)
        {
            case 0:
            {
                combat.ClearAttackFlags(state);
                state.ActionFlags |= 2;
                state.TurnRate = 0;
                if (state.Health <= 0) state.BodyFlags &= ~0x10u;
                if (state.HitKind is 1 or 2 or 6 or 23) state.MinorRadius = 0;
                int speed = actor.Body.Velocity.X;
                if (ThugPerception.SourceAbs(speed) <= ThugPerception.SourceAbs(actor.Body.Velocity.Z)) speed = actor.Body.Velocity.Z;
                if (ThugPerception.SourceAbs(unchecked(speed * state.ActionCounter)) >= 819200) host.CycleAnimation(17);
                else host.RunAnimation(20);
                state.Substate = state.Health <= 0 || ((state.CombatFlags & 0x4000) != 0 && state.Health <= 50) ? 10 : 1;
                break;
            }
            case 1:
            {
                state.ActionFlags |= 2;
                state.PreviousPosition = actor.Body.Position;
                DoPhysics(true);
                IThugInjuryBody? struck = host.StruckGameObject(0, 1);
                if (struck is not null)
                {
                    actor.BehaviorFlags &= ~0x10u;
                    if (state.ActionCounter > 1 && struck.Type is 306 or 320)
                        host.ElasticCollision(struck, actor.Body.Velocity, state.ActionCounter >> 1);
                }
                if (state.ActionCounter != 0 && struck is null) break;
                actor.Body.Velocity = default;
                if (ShouldFall(200, 389120) != 0) { StartFalling(); break; }
                host.SetHeight(true, 100, 600);
                PlayHitWallSound();
                if (state.HitKind is 1 or 2 or 6 or 23 or 25)
                {
                    CheckFallBack();
                    host.RunAnimation((actor.BehaviorFlags & 0x10) != 0 ? (ushort)24 : (ushort)22);
                    state.Substate++;
                }
                else
                {
                    if (host.AnimationId == 17) SetState(28);
                    state.Substate = 5;
                }
                break;
            }
            case 2:
                host.SetHeight(false, 100, 600);
                if (host.AnimationFinished != 0)
                {
                    state.MinorRadius = state.IsPolice ? 96 : 150;
                    host.RunAnimation(23);
                    state.Substate++;
                }
                break;
            case 3:
                host.SetHeight(false, 100, 600);
                if ((short)host.AnimationFrame < 22) state.ActionFlags |= 1;
                if (host.AnimationFinished == 0) break;
                if (state.Health > 0) { state.BodyFlags |= 0x10; SetState(2); }
                else if (ShouldFall(200, 389120) != 0) StartFalling();
                else LandDead();
                break;
            case 5:
                if (host.SetHeight(false, 100, 600) != 2) return;
                if (host.AnimationFinished != 0) SetState(28);
                break;
            case 10:
            {
                state.ActionFlags |= 2;
                state.PreviousPosition = actor.Body.Position;
                DoPhysics(true);
                if ((state.CombatFlags & 0x2000) != 0)
                {
                    ActorAngles angles = actor.Body.Angles;
                    actor.Body.Angles = new(unchecked((short)(angles.X + (short)elapsedTicks * (short)state.SlidePitchRate)),
                        unchecked((short)(angles.Y + (short)elapsedTicks * (short)state.SlideYawRate)), angles.Z);
                }
                // The source's body local starts as the pushed `this`, so a running 0x328 delay counts as a hit.
                bool hit = state.Timer328Ticks != 0;
                if (!hit)
                {
                    IThugInjuryBody? body = host.StruckGameObject(0, 1);
                    hit = body is not null;
                    if (body is not null)
                    {
                        actor.BehaviorFlags &= ~0x10u;
                        if (state.ActionCounter > 1 && body.Type is 306 or 320)
                        {
                            host.ElasticCollision(body, actor.Body.Velocity, state.ActionCounter >> 1);
                            if ((state.CombatFlags & 0x2000) != 0) { state.Timer328Ticks = 10; hit = false; }
                        }
                    }
                }
                if (state.ActionCounter != 0 && !hit) break;
                actor.Body.Velocity = default;
                actor.Body.Angles = actor.Body.Angles with { X = 0 };
                if (ShouldFall(200, 389120) == 0) LandDead();
                else StartFalling();
                break;
            }
            default: throw new InvalidOperationException("Unknown substate!");
        }
        if (ShouldFall(200, 389120) != 0)
        {
            actor.Body.Velocity = default;
            actor.Body.Position = state.PreviousPosition;
            host.SetHeight(true, 100, 600);
            SetState(26);
        }
    }

    private void StartFalling()
    {
        state.CombatFlags &= ~2u;
        SetState(22);
    }

    private void LandDead()
    {
        PlayHitWallSound();
        host.SetHeight(true, 100, 600);
        SetState(26);
    }

    private void PlayHitWallSound()
    {
        if (state.ActionCounter == 0)
            host.PlayPositionSound((state.CombatFlags & 0x400) != 0 ? (ushort)0x802F : (ushort)0x802E, actor.Body.Position, 0);
    }

    /// <summary>CCop::GettingGrabbed (0x42E1D0): claims the primary-attacker slot and rides the player's grab.</summary>
    private void GettingGrabbed()
    {
        if (!ReferenceEquals(combat.PrimaryAttacker, state))
        {
            if ((state.AttackOwnershipFlags & 2) != 0) combat.SharedSlots.OccupiedSlots &= ~(uint)state.AttackSlotMask;
            state.AttackOwnershipFlags = 0;
            state.AttackSlotMask = 0;
            if (combat.PrimaryAttacker is { } previous) { previous.AttackOwnershipFlags = 0; previous.AttackSlotMask = 0; }
            combat.PrimaryAttacker = state;
            state.AttackOwnershipFlags = 1;
        }
        if (host.PlayerAnimationId == 124 && (ushort)host.AnimationId != 28) host.RunAnimation(28);
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                host.RunAnimation(25);
                state.Substate++;
                break;
            case 1:
            {
                if (host.AnimationFinished != 0) RunGrabAnimation(false);
                FixedVector3 target = host.GetPathPoint(state.PathPointIndex);
                FixedVector3 position = actor.Body.Position;
                if (ThugPerception.SourceAbs(unchecked(position.X - target.X)) >= 20 || ThugPerception.SourceAbs(unchecked(position.Z - target.Z)) >= 20)
                {
                    ActorYawSteering.YawTowards(actor.Body, host.PlayerYaw, 100);
                    actor.Body.Position = new(unchecked(position.X + (unchecked(target.X - position.X) >> 1)), position.Y,
                        unchecked(position.Z + (unchecked(target.Z - position.Z) >> 1)));
                }
                else
                {
                    actor.Body.AngularVelocity = actor.Body.AngularVelocity with { Y = 0 };
                    actor.Body.AngularAcceleration = actor.Body.AngularAcceleration with { Y = 0 };
                    state.PathPointIndex--;
                    actor.Body.Angles = actor.Body.Angles with { Y = host.PlayerYaw };
                    state.Substate++;
                }
                break;
            }
            case 2:
            {
                FixedVector3 position = actor.Body.Position;
                short yaw = actor.Body.Angles.Y;
                bool held = host.GrabUpdate(ref position, ref yaw);
                actor.Body.Position = position;
                actor.Body.Angles = actor.Body.Angles with { Y = yaw };
                if (!held) actor.BehaviorFlags &= ~0x40u;
                if (host.AnimationFinished != 0) RunGrabAnimation(true);
                break;
            }
            default: throw new InvalidOperationException("Unknown substate!");
        }

        void RunGrabAnimation(bool sound)
        {
            if (host.PlayerActionState == 0x8000000)
            {
                ushort clip = state.IsPolice ? (ushort)3 : (ushort)26;
                host.RunAnimation((ushort)host.AnimationId == clip || (ushort)host.AnimationId == 27 ? clip : (ushort)27);
                state.CombatFlags |= 4;
            }
            else
            {
                host.RunAnimation(26);
                state.CombatFlags &= ~4u;
                if (sound) host.PlayPositionSound(unchecked((ushort)((random(2) + 43) | 0x8000)), actor.Body.Position, 0);
            }
        }
    }

    /// <summary>CCop state 18 (0x42C750): stuck in a trap web, struggling until it bursts or reaches 80 strands.</summary>
    private void GetTrapped()
    {
        switch (state.Substate)
        {
            case 0:
            {
                procedures.Attach(new CopStateSwitchMessageProcedure(state, 14, host.SendSelfMessage));
                CopState? buddy = host.FindClosest(306) ?? host.FindClosest(320);
                if (buddy is not null && buddy.AlertTicks == 0) host.SendMessage(buddy, 7);
                host.PlayPositionSound(0x800E, actor.Body.Position, 0);
                state.TurnRate = 0;
                combat.ClearAttackFlags(state);
                host.RunAnimation(17);
                state.Substate++;
                break;
            }
            case 1:
                if (host.AnimationFinished == 0) break;
                state.WorkCounter = 0;
                state.WorkDelayCounter = 0;
                state.ActionCounter = 5;
                state.Substate = 2;
                host.CycleAnimation(17);
                host.PlayPositionSound(unchecked((ushort)((random(2) + 43) | 0x8000)), actor.Body.Position, 0);
                break;
            case 2:
            {
                if (state.WorkDelayCounter > 0) state.WorkDelayCounter--;
                state.ActionCounter = unchecked(state.ActionCounter - 1);
                if (state.ActionCounter > 0) break;
                IThugTrapWeb? web = host.RecoverTrapWeb();
                if (web is null || web.StrandCount == state.WorkCounter) { state.Substate++; break; }
                state.WorkDelayCounter = unchecked(state.WorkDelayCounter + (unchecked(8000 * unchecked(web.StrandCount - state.WorkCounter)) >> 12));
                state.ActionCounter = 5;
                state.WorkCounter = web.StrandCount;
                if (web.StrandCount >= 80)
                {
                    SetState(26);
                    SendDeathPulse();
                    state.Health = 0;
                    host.SetPlayerWebCombo(360, unchecked((host.PlayerWebComboValue + 11) << 10));
                    state.CombatFlags |= 0xC0;
                }
                break;
            }
            case 3:
                state.WorkDelayCounter = unchecked(state.WorkDelayCounter - 1);
                if (state.WorkDelayCounter <= 0)
                {
                    host.RecoverTrapWeb()?.Burst();
                    SetState(28);
                }
                break;
            default: throw new InvalidOperationException("Unknown substate!");
        }
    }

    /// <summary>CCop::GetYankedBySpidey (0x42D2B0), states 14 (bobbing on the web) and 15.</summary>
    private void GetYankedBySpidey()
    {
        state.PreviousPosition = actor.Body.Position;
        if (state.State == 14) ApplyYankBob(1);
        switch (state.Substate)
        {
            case 0:
            {
                state.ActionFlags |= 2;
                CopState? buddy = host.FindClosest(306) ?? host.FindClosest(320);
                if (buddy is not null && buddy.AlertTicks == 0) host.SendMessage(buddy, 7);
                state.TurnRate = 0;
                combat.ClearAttackFlags(state);
                state.BodyFlags &= ~0x10u;
                state.CombatFlags &= ~0x20u;
                state.YankOscillationTicks = 0;
                procedures.Attach(new CopStateSwitchMessageProcedure(state, 19, host.SendSelfMessage));
                host.RunAnimation(18);
                state.Substate = (state.CombatFlags & 0x100) != 0 ? 9 : state.Health > 0 ? 1 : 10;
                break;
            }
            case 1:
            {
                state.ActionFlags |= 2;
                if (host.AnimationFinished != 0) host.CycleAnimation(19);
                state.PreviousPosition = actor.Body.Position;
                DoPhysics(true);
                if (state.ActionCounter <= 0)
                    throw new InvalidOperationException("CCop::GetYankedBySpidey (0x42D4E5) reads the type of a null struck body once the yank counter runs out.");
                IThugInjuryBody? body = host.StruckGameObject(1, 1);
                if (body is null) break;
                if (state.ActionCounter > 1 && body.Type is 320 or 306)
                    host.ElasticCollision(body, actor.Body.Velocity, state.ActionCounter >> 1);
                if (body.Type == 50) actor.Body.Position = state.PreviousPosition;
                state.State = 15;
                actor.Body.Velocity = default;
                if (ShouldFall(200, 389120) != 0) { StartFalling(); break; }
                PlayHitWallSound();
                host.RunAnimation(22);
                state.Substate++;
                break;
            }
            case 2:
                if (host.AnimationFinished != 0) { host.RunAnimation(23); state.Substate++; }
                break;
            case 3:
                if (host.AnimationFinished != 0) { state.BodyFlags |= 0x10; SetState(2); }
                break;
            case 9:
                if (state.Health <= 0) actor.BehaviorFlags |= 0x10;
                state.Substate = 10;
                goto case 10;
            case 10:
            {
                state.ActionFlags |= 2;
                if (host.AnimationFinished != 0) host.CycleAnimation(19);
                state.PreviousPosition = actor.Body.Position;
                DoPhysics(true);
                IThugInjuryBody? body = host.StruckGameObject(1, 1);
                if (body is not null)
                {
                    if (body.Type == 50) actor.Body.Position = state.PreviousPosition;
                    actor.BehaviorFlags &= ~0x10u;
                    if (state.ActionCounter > 1 && body.Type is 306 or 320)
                        host.ElasticCollision(body, actor.Body.Velocity, state.ActionCounter >> 1);
                }
                if (state.ActionCounter > 0 && body is null) break;
                state.State = 15;
                actor.Body.Velocity = default;
                if (ShouldFall(200, 389120) != 0) StartFalling();
                else if (body is not null || (state.CombatFlags & 0x100) == 0)
                {
                    state.ActionCounter = 0;
                    if (state.Health <= 0)
                    {
                        PlayHitWallSound();
                        SetState(26);
                    }
                    else
                    {
                        host.PlayPositionSound(0x801B, actor.Body.Position, 0);
                        host.RunAnimation(22);
                        state.Substate = 2;
                    }
                }
                else
                {
                    host.RunAnimation(22);
                    state.Substate = 11;
                }
                break;
            }
            case 11:
                if (host.AnimationFinished == 0) break;
                if (state.Health <= 0) SetState(26);
                else { host.RunAnimation(23); state.Substate = 3; }
                break;
            default: throw new InvalidOperationException("Unknown substate!");
        }
        if (state.State == 14 && state.Substate > 0)
        {
            if ((state.CombatFlags & 0x20) != 0)
            {
                if (state.YankOscillationTicks != 0)
                    state.YankOscillationTicks = Math.Max(0, unchecked(state.YankOscillationTicks - elapsedTicks));
            }
            else
            {
                state.YankOscillationTicks = unchecked(state.YankOscillationTicks + elapsedTicks);
                if (state.YankOscillationTicks >= 40) state.CombatFlags |= 0x20;
            }
            ApplyYankBob(-1);
        }
        if (ShouldFall(200, 389120) != 0)
        {
            actor.Body.Velocity = default;
            actor.Body.Position = state.PreviousPosition;
            host.SetHeight(true, 100, 600);
            SetState(26);
        }
    }

    /// <summary>Adds (sign 1) or removes (sign -1) the bob: 6144 per tick of height and -8 of pitch.</summary>
    private void ApplyYankBob(int sign)
    {
        int ticks = state.YankOscillationTicks;
        actor.Body.Position = actor.Body.Position with { Y = unchecked(actor.Body.Position.Y + sign * 6144 * ticks) };
        actor.Body.Angles = actor.Body.Angles with { X = unchecked((short)(actor.Body.Angles.X - sign * 8 * ticks)) };
    }

    /// <summary>CCop::GetWhippedLikeTheWhoreYouAre (0x42D060): dragged along the throw points, then slammed.</summary>
    private void GetWhipped()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.Substate++;
                host.RunAnimation(22);
                state.ActionCounter = 0;
                goto case 1;
            case 1:
            {
                state.ActionFlags |= 2;
                state.PreviousPosition = actor.Body.Position;
                FixedVector3 point = host.GetThrowPoint(state.ActionCounter);
                int path = PathCheck(actor.Body.Position, point);
                if (path == 0 && host.AddPointToPath(host.GetThrowPoint(state.ActionCounter), 0) && ++state.ActionCounter < 8)
                {
                    actor.Body.Position = host.GetThrowPoint(state.ActionCounter - 1);
                    break;
                }
                if (ShouldFall(200, 389120) != 0)
                {
                    actor.Body.Position = state.PreviousPosition;
                    host.SetHeight(true, 100, 600);
                    SetState(26);
                }
                else
                {
                    if (path != 0) host.RunAnimation(22);
                    state.Substate++;
                }
                break;
            }
            case 2:
                if (host.AnimationFinished == 0) break;
                state.Health = unchecked((short)(state.Health - 50));
                if (state.Health <= 0) SetState(26);
                else { host.RunAnimation(23); state.Substate++; }
                break;
            case 3:
                if (host.AnimationFinished != 0) { state.BodyFlags |= 0x10; SetState(0); }
                break;
            case 4:
                actor.Body.Position = actor.Body.Position with { Y = unchecked(actor.Body.Position.Y + 409600) };
                if (actor.Body.Position.Y > state.LandingY)
                {
                    host.PlayPositionSound(0x802E, actor.Body.Position, 0);
                    actor.Body.Position = actor.Body.Position with { Y = state.LandingY };
                    state.Substate = 2;
                }
                break;
            default: throw new InvalidOperationException("Unknown substate!");
        }
    }

    /// <summary>CCop::Fall (0x42DD50).</summary>
    private void Fall()
    {
        state.ActionFlags |= 2;
        switch (state.Substate)
        {
            case 0:
                combat.ClearAttackFlags(state);
                state.TurnRate = 0;
                if ((state.CombatFlags & 0x2002) == 0 && random(3) == 0) { state.Substate = 3; return; }
                goto case 1;
            case 1:
                actor.Body.Acceleration = actor.Body.Acceleration with { Y = 147456 };
                procedures.Attach(new ActorFallProcedure(actor, new LandingHost(state), 4));
                if ((state.CombatFlags & 2) == 0) host.CycleAnimation(17);
                actor.BehaviorFlags &= ~1u;
                state.Substate++;
                state.ActionCounter = 65;
                if ((actor.BehaviorFlags & 0x400) == 0 || state.LandingY > unchecked(actor.Body.Position.Y + 1024000))
                    host.PlayGlobalSound(0x8018, 0x3FFF, 0);
                return;
            case 2:
                actor.Body.Angles = actor.Body.Angles with { X = unchecked((short)(actor.Body.Angles.X + 40)) };
                if (actor.Body.Velocity.Y >= 1228800) actor.Body.Velocity = actor.Body.Velocity with { Y = 1228800 };
                state.ActionCounter = Timer(state.ActionCounter);
                if ((actor.CompletionFlags & 4) != 0)
                {
                    actor.CompletionFlags &= ~4u;
                    host.Neutralize();
                    state.Health = 0;
                    SendDeathPulse();
                    actor.BehaviorFlags |= 0x8000;
                    host.PlayGlobalSound(0x802E, 0x3FFF, 0);
                    state.ActionCounter = 50;
                    state.Substate = 5;
                }
                else if (state.ActionCounter == 0)
                {
                    host.Neutralize();
                    state.Health = 0;
                    SendDeathPulse();
                    host.PlayGlobalSound(0x802E, 0x3FFF, 0);
                    state.ActionCounter = 50;
                    state.Substate = 6;
                }
                return;
            case 3:
                state.ActionCounter = 0;
                host.PlayPositionSound(0x8010, actor.Body.Position, 0);
                host.CycleAnimation(state.IdleAnimation);
                state.Substate++;
                return;
            case 4:
                if (host.HasJoints)
                {
                    if (state.ActionCounter != 0)
                    {
                        host.JointOnePitch = unchecked((short)(host.JointOnePitch - 90));
                        if (host.JointOnePitch <= 0) { host.ItemFlags &= unchecked((ushort)~4); state.Substate = 1; return; }
                    }
                    else
                    {
                        host.JointOnePitch = unchecked((short)(host.JointOnePitch + 90));
                        if (host.JointOnePitch >= 900) { state.ActionCounter = 1; return; }
                    }
                }
                host.ItemFlags |= 4;
                host.ApplyFallPose(state.IsPolice);
                return;
            case 5:
            case 6:
                host.ItemFlags |= 1;
                state.ActionCounter = Timer(state.ActionCounter);
                if (state.ActionCounter == 0)
                {
                    host.PlayGlobalSound(0x802C, 1023, 0);
                    if (state.Substate == 6) actor.BehaviorFlags |= 0x8000;
                    SetState(27);
                }
                return;
            default: throw new InvalidOperationException("Unknown substate!");
        }
    }

    /// <summary>CCop::Die (0x42D9E0); state 26 passes 0 and state 27 passes 1.</summary>
    private void Die(bool instant)
    {
        switch (state.Substate)
        {
            case 0:
                host.NotifyKill(state.Type);
                host.SetHeight(true, 100, 600);
                combat.ClearAttackFlags(state);
                state.TurnRate = 0;
                host.Neutralize();
                state.BodyFlags &= ~0x10u;
                actor.BehaviorFlags |= 0x41000;
                if (instant) { BaddyDie(0); state.Substate = 3; return; }
                if ((state.CombatFlags & 0x80) != 0) { host.RunAnimation(36); state.Substate = 5; return; }
                if ((ushort)host.AnimationId is not (24 or 22))
                {
                    CheckFallBack();
                    host.RunAnimation((actor.BehaviorFlags & 0x10) != 0 ? (ushort)24 : (ushort)22);
                }
                state.ActionCounter = 0;
                state.Substate++;
                return;
            case 1:
                host.SetHeight(false, 100, 600);
                if (host.AnimationFinished == 0) return;
                state.MinorRadius = 0;
                BaddyDie(1);
                state.ActionCounter = 0;
                state.Substate++;
                return;
            case 2:
                host.SetHeight(false, 100, 600);
                if (BaddyDie(2)) { BaddyDie(3); state.Substate++; }
                return;
            case 3: return;
            case 5:
                if (host.AnimationFinished != 0) { host.RunAnimation(37); state.Substate++; }
                return;
            case 6:
                if (host.AnimationFinished != 0) { state.ActionCounter = 0; state.Substate = 7; }
                return;
            case 7:
                state.ActionCounter = unchecked(state.ActionCounter + elapsedTicks);
                if (state.ActionCounter >= 10)
                {
                    if ((actor.BehaviorFlags & 0x4000) == 0) SendDeathPulse();
                    host.CycleAnimation(38);
                    state.ActionCounter = 120;
                    state.DeathSoundTicks = unchecked(random(300) + 150);
                    state.Substate++;
                }
                return;
            case 8:
                state.ActionCounter = Timer(state.ActionCounter);
                state.DeathSoundTicks = Timer(state.DeathSoundTicks);
                if (state.ActionCounter == 0)
                {
                    host.RunAnimation(unchecked((ushort)host.AnimationId));
                    state.Substate = 1;
                }
                else if (state.DeathSoundTicks == 0)
                {
                    state.DeathSoundTicks = unchecked(random(300) + 150);
                    host.PlayPositionSound(unchecked((ushort)((random(2) + 43) | 0x8000)), actor.Body.Position, 0);
                }
                return;
            default: throw new InvalidOperationException("Unknown substate!");
        }
    }

    /// <summary>CBaddy::Die (0x404320). Returns true when the stage-2 fade has finished.</summary>
    private bool BaddyDie(int stage)
    {
        if ((state.BodyFlags & 0x40) != 0) return false;
        switch (stage)
        {
            case 0:
                if ((actor.BehaviorFlags & 0x4000) == 0) SendDeathPulse();
                goto case 3;
            case 3:
                state.BodyFlags |= 0x40;
                host.ItemFlags |= 1;
                host.MarkDead();
                return false;
            case 1:
                if ((actor.BehaviorFlags & 0x4000) == 0) SendDeathPulse();
                host.ItemFlags |= 0x800;
                state.Transparency = 128;
                KillShadow();
                host.ItemFlags |= 0x400;
                state.TintColor = 0x404040;
                state.ActionCounter = 0;
                return false;
            case 2:
            {
                int previous = state.ActionCounter;
                state.ActionCounter = unchecked(previous + 1);
                if (previous >= 40) return true;
                int brightness = unchecked(6553 * (40 - state.ActionCounter)) >> 12;
                state.TintColor = unchecked((uint)(brightness | ((brightness | (brightness << 8)) << 8)));
                return false;
            }
            default: throw new InvalidOperationException("Unknown die state.");
        }
    }

    /// <summary>CBaddy::SendDeathPulse (0x4048B0).</summary>
    private void SendDeathPulse()
    {
        if (state.DeathPulseSent || (ushort)state.SourceNode == 0xFFFF) return;
        state.DeathPulseSent = true;
        host.SendDeathPulse();
    }

    private void KillShadow()
    {
        state.BodyFlags &= ~8u;
        host.KillShadow();
    }

    /// <summary>CBaddy::ShouldFall (0x404810).</summary>
    private int ShouldFall(int above, int maximumDrop)
    {
        int ground = host.GroundHeight(actor.Body.Position, above, 4096, 0);
        if (ground == -1)
        {
            state.LandingY = unchecked(actor.Body.Position.Y - 100);
            KillShadow();
            actor.BehaviorFlags &= ~0x400u;
            return -1;
        }
        int target = unchecked(ground - (state.SurfaceClearance << 12));
        if (unchecked(target - actor.Body.Position.Y) <= maximumDrop) return 0;
        KillShadow();
        actor.BehaviorFlags |= 0x400;
        state.LandingY = target;
        return unchecked(target - actor.Body.Position.Y);
    }

    /// <summary>CCop::CheckFallBack (0x42D900): a 75-unit probe behind the recoil, turning when it reaches a drop.</summary>
    private void CheckFallBack()
    {
        int forward = ((actor.BehaviorFlags & 0x10) != 0 ? -75 : 75) << 12;
        (short sine, short cosine) = GameAngle.Heading(actor.Body.Angles.Y);
        FixedVector3 offset = new(unchecked((forward >> 3) * sine) >> 9, 0, unchecked((forward >> 3) * cosine) >> 9);
        if (host.PathCheck(actor.Body.Position, actor.Body.Position + offset, 55, (actor.BehaviorFlags & 0x2000) != 0) == 2)
            procedures.Attach(new ActorYawRotationProcedure(actor, 2047, 4, 0));
    }
}
