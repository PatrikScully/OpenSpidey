using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Rhino;

// Unknown substates only print through nullsub_1 in the original, so every switch below ignores them.
public sealed partial class RhinoBrain
{
    /// <summary>CRhino::FollowWaypoints (0x47F320).</summary>
    public void FollowWaypoints()
    {
        RunAppropriateAnim();
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.Field310 = 0;
                if (state.Field1F0 == 0)
                {
                    SetState(2);
                    break;
                }
                host.Actor.BehaviorFlags &= ~0x10000000u;
                host.AttachMoveTo(state.PathPoints[state.Field1F0], 40, 40, 500, 1);
                state.Substate++;
                break;
            case 1:
                if ((host.Actor.CompletionFlags & 1) == 0) break;
                host.Actor.CompletionFlags &= ~1u;
                if (state.Field1F0 != 0) state.Field1F0--;
                else if (!host.TryAdvanceWaypoint())
                {
                    SetState(2);
                    break;
                }
                if ((host.RouteFlags & 2) == 0) state.Substate = 0;
                break;
        }
    }

    /// <summary>CBaddy::RunAppropriateAnim (0x404B60) over the 0x294/0x298 animation words.</summary>
    private void RunAppropriateAnim()
    {
        uint movement = host.Body.MovementFlags;
        int walkStart = (byte)state.MovementAnimations;
        int walkLoop = (byte)(state.MovementAnimations >> 8);
        if ((movement & 0x40000) != 0)
        {
            if (walkStart == walkLoop) host.CycleAnimation(walkLoop);
            else if (host.AnimationId != walkLoop && host.AnimationId != walkStart) host.RunAnimation(walkStart);
            else if (host.AnimationId != walkStart || host.AnimationFinished) host.CycleAnimation(walkLoop);
        }
        else if ((movement & 0x10000) != 0)
        {
            int turn = (byte)(state.MovementAnimations >> 16);
            if (host.AnimationId != turn || host.AnimationFinished) host.RunAnimation(turn);
        }
        else if ((movement & 0x20000) != 0)
        {
            int turn = (byte)(state.MovementAnimations >> 24);
            if (host.AnimationId != turn || host.AnimationFinished) host.RunAnimation(turn);
        }
        else host.CycleAnimation((byte)state.IdleAnimations);
    }

    /// <summary>CRhino::Laugh (0x480090).</summary>
    public void Laugh()
    {
        switch (state.Substate)
        {
            case 0:
                state.Field230 = 100;
                PlaySingleAnim(20);
                host.PlayPositionalSound(((host.AttackClock & 1) == 0 ? 1 : 0) | 0x8046);
                state.Substate++;
                break;
            case 1:
                state.Field230 = RunTimer(state.Field230);
                if (!host.AnimationFinished) break;
                if (state.Field230 != 0) PlaySingleAnim(0);
                else SetState(2);
                break;
        }
    }

    /// <summary>CRhino::ChasePlayer (0x47F490); <paramref name="mode"/> 2 for state 4, 1 for state 5.</summary>
    public void ChasePlayer(int mode)
    {
        switch (state.Substate)
        {
            case 0:
                state.Field310 = 0x64;
                host.Neutralize();
                host.AttachPlayerLookAt(2, 0x50, 0);
                state.Field330 = 0;
                state.Substate++;
                break;
            case 1:
                host.DoPhysics(0);
                if ((host.Actor.CompletionFlags & 2) == 0) break;
                host.Actor.CompletionFlags &= ~2u;
                host.AttachAcceleration(0xF0, -0x28, 0);
                PlaySingleAnim(1);
                state.Substate++;
                break;
            case 2:
                if ((GonnaHitWall(0) & 0xD) != 0)
                {
                    host.Neutralize();
                    SetState(0x16);
                    break;
                }
                if (host.AnimationId == 1 && host.AnimationFinished) PlaySingleAnim(2);
                if (!LineOfSightCheck(host.PlayerPosition, 1)) SetState(0x16);
                if (ThugPerception.ApproximateHorizontalDistance(host.Body.Position, host.PlayerPosition) < (mode == 2 ? 5000 : 200))
                {
                    host.AttachAcceleration(0xA0, 0, 8);
                    PlaySingleAnim(3);
                    state.Substate++;
                }
                break;
            case 3:
                if (host.AnimationFinished && host.AnimationId == 3) PlaySingleAnim(0);
                if (GonnaHitWall(0) != 0)
                {
                    host.Neutralize();
                    SetState(2);
                    break;
                }
                if ((host.Actor.CompletionFlags & 8) == 0) break;
                host.Actor.CompletionFlags &= ~8u;
                host.Neutralize();
                int distance = ThugPerception.ApproximateHorizontalDistance(host.Body.Position, host.PlayerPosition);
                if (distance < 0xC8)
                {
                    if (host.PlayerIsCrawling) SetState(0xD);
                    else if ((host.PlayerActionState & 0x800000) != 0)
                    {
                        host.RunAnimation((byte)state.IdleAnimations);
                        state.Substate++;
                    }
                    else SetState(6);
                }
                else if (distance <= 0x1F4) SetState(2);
                else SetState(7);
                break;
            case 4:
                if (host.AnimationFinished) SetState(2);
                break;
        }

        state.Field330 = unchecked(state.Field330 + host.ElapsedTicks);
        if (mode == 1 && state.Field330 >= 0x3C) SetState(7);
    }

    /// <summary>CRhino::AttackPlayer (0x480170), alternating the two swings through field_360.</summary>
    public void AttackPlayer()
    {
        switch (state.Substate)
        {
            case 0:
                state.Field310 = 100;
                host.Neutralize();
                host.AttachPlayerLookAt(2, 80, 0);
                state.Field330 = unchecked(host.Random(30) + 90);
                state.Substate++;
                break;
            case 1:
                state.Field330 = unchecked(state.Field330 - host.ElapsedTicks);
                if ((host.Actor.CompletionFlags & 2) != 0) host.Actor.CompletionFlags &= ~2u;
                else if (state.Field330 > 0) return;
                state.Field330 = 0;
                host.PlayPositionalSound(((host.AttackClock & 1) == 0 ? 1 : 0) | 0x80C8);
                if (state.Field360 == 0)
                {
                    PlaySingleAnim(10);
                    host.AttachAttackMonitor(7, 0x7000, 6, 0x10);
                    state.Substate = 2;
                    state.Field360 = 1;
                }
                else if (state.Field360 == 1)
                {
                    PlaySingleAnim(11);
                    host.AttachAttackMonitor(7, 0xE00, 6, 0x10);
                    state.Substate = 2;
                    state.Field360 = 0;
                }
                break;
            case 2:
                if ((host.Actor.CompletionFlags & 0x10) != 0)
                {
                    host.Actor.CompletionFlags &= ~0x10u;
                    FixedVector3 delta = host.PlayerPosition - host.Body.Position;
                    state.Field344 = host.AttackClock;
                    if (host.HitPlayer(new ActorHit(14, 0, 11, 15, delta)))
                    {
                        state.Substate++;
                        break;
                    }
                }
                if (host.AnimationFinished) SetState(2);
                break;
            case 3:
                if (host.AnimationFinished) SetState(3);
                break;
        }
    }

    /// <summary>CRhino::HitWall (0x480F50).</summary>
    public void HitWall()
    {
        switch (state.Substate)
        {
            case 0:
                ShakePad();
                host.ShakeCamera();
                host.Neutralize();
                state.BodyFlags &= ~0x10u;
                PlaySingleAnim(17);
                state.Substate++;
                break;
            case 1:
                if (!host.AnimationFinished) break;
                if (state.Health <= 0)
                {
                    SetState(21);
                    break;
                }
                PlaySingleAnim(18);
                state.Substate++;
                break;
            case 2:
                if (!host.AnimationFinished) break;
                if (host.AnimationId == 18)
                {
                    host.Body.Angles = host.Body.Angles with { Y = unchecked((short)((host.Body.Angles.Y - 2048) & 0xFFF)) };
                    PlaySingleAnim(21);
                }
                else
                {
                    state.BodyFlags |= 0x10;
                    PlaySingleAnim(0);
                    SetState(2);
                }
                break;
        }
    }

    /// <summary>CRhino::StuckInWall (0x47E680), states 10 and 11.</summary>
    public void StuckInWall()
    {
        switch (state.Substate)
        {
            case 0:
                state.Field348 |= 2;
                state.CombatFlags &= ~8u;
                host.AttachStateSwitchMessage(state, 0x0C);
                state.Substate++;
                goto case 1;
            case 1:
                if ((host.Actor.CompletionFlags & 1) == 0) break;
                host.Actor.CompletionFlags &= ~1u;
                state.Field230 = host.PlayerInputSuppression != 0 ? 900 : Difficulty(200, 150, 120, 90);
                state.Substate++;
                break;
            case 2:
                state.Field230 = RunTimer(state.Field230);
                if (state.Field230 > 0x3C && host.PlayerInputSuppression == 0) state.Field230 = 0x3C;
                state.Field348 |= 2;
                if (!host.AnimationFinished) break;
                if (state.Field230 != 0)
                {
                    PlaySingleAnim(0x16);
                    break;
                }
                PlaySingleAnim(0x19);
                state.Substate++;
                state.State = 0xB;
                host.PlayPositionalSound(0x8048);
                if ((state.CombatFlags & 8) != 0) PlayXAPlease(9, 2, 1);
                break;
            case 3:
                if (host.AnimationFinished) SetState(2);
                break;
        }
    }

    /// <summary>CRhino::GetShocked (0x47E420), with GetShockDamage inlined.</summary>
    public void GetShocked()
    {
        state.Field3D0 = 0x1E;
        switch (state.Substate)
        {
            case 0:
                state.BodyFlags &= ~0x10u;
                state.Field348 |= 2;
                host.Electrify();
                host.AttachStateSwitchMessage(state, 0x11);
                if (state.Field338 == 0) state.Field338 = host.PlayTrackedSound(0x80CC);
                host.CycleAnimation(0x1E);
                state.Field34C = Difficulty(300, 300, 300, 300);
                state.Field420 = (Difficulty(250, 175, 125, 75) << 12) / state.Field34C;
                state.Field354 = state.Health;
                if (host.Random(4) != 0) PlayXAPlease(0xB, 1, 0);
                else PlayXAPlease(0xC, 3, 0);
                state.Substate++;
                break;
            case 1:
                state.Field34C = RunTimer(state.Field34C);
                state.Field348 |= 2;
                int damage = host.Difficulty switch { 0 or 1 => 175, 2 => 125, 3 => 75, _ => 0 };
                state.Health = unchecked((short)(state.Field354 + ((state.Field34C * state.Field420) >> 12) - damage));
                if (state.Field34C == 0) host.RunAnimation(0x1E, host.AnimationFrame);
                if (!host.AnimationFinished) break;
                if (state.Field338 != 0) host.StopSound(state.Field338);
                state.Field338 = 0;
                if (state.Health <= 0) state.State = 0x15;
                else
                {
                    state.BodyFlags |= 0x10;
                    host.CycleAnimation((byte)state.IdleAnimations);
                    PlayXAPlease(6, 3, 1);
                    state.State = 2;
                }
                state.Substate = 0;
                break;
        }
    }

    /// <summary>CRhino::StompGround (0x4810C0), states 13 and 20.</summary>
    public void StompGround()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.Field324 = 0;
                state.Field328 = 0;
                PlayXAPlease(0x13, 1, 1);
                PlaySingleAnim(0x13);
                state.Substate++;
                break;
            case 1:
                if (host.AnimationFrame < 0x11) break;
                host.PlayPositionalSound(0x804B);
                host.ShakeCamera();
                ShakePad();
                host.EmitStompEffect();
                state.Substate++;
                if (state.State == 0x14)
                {
                    int smashed = 0;
                    object? held = host.PlayerHeldObject;
                    foreach (RhinoEnvironmentBody body in host.EnvironmentalBodies)
                    {
                        if (smashed >= 2) break;
                        if (body.Type != 401) continue;
                        if (unchecked((uint)Venom.VenomNodeStates.CrapDistance(host.Body.Position, body.Position)) < 0x2BC &&
                            !ReferenceEquals(body.Identity, held))
                        {
                            host.SmashEnvironmentalBody(body.Identity);
                            smashed++;
                        }
                    }
                }
                else if (host.PlayerIsCrawling && host.PlayerOnWallOrCeiling)
                {
                    host.HitPlayer(new ActorHit(6, 0, 13, 30, default));
                    host.KnockPlayerFromCrawl();
                }
                else state.Substate = 3;
                break;
            case 2:
                if (!host.AnimationFinished) break;
                SetState(state.State == 0x14 ? 2 : 3);
                break;
            case 3:
                if (host.AnimationFinished) SetState(2);
                break;
        }
    }

    /// <summary>CRhino::TakeHit (0x480820).</summary>
    public void TakeHit()
    {
        switch (state.Substate)
        {
            case 0:
                state.Field310 = 0;
                host.AttachPlayerLookAt(0, 80, 200);
                PlaySingleAnim(15);
                state.Field230 = Difficulty(40, 30, 21, 21);
                state.Substate++;
                break;
            case 1:
                state.Field230 = RunTimer(state.Field230);
                if (state.Field230 == 0) RecoverFromHit();
                break;
        }
    }

    /// <summary>CRhino::GetLaunched (0x47ECA0): DoPhysics(1) spends the SlideFromHit flight budget.</summary>
    public void GetLaunched()
    {
        switch (state.Substate)
        {
            case 0:
                state.Substate = 1;
                PlaySingleAnim(15);
                host.AttachPlayerLookAt(2, 80, 0);
                state.Field230 = Difficulty(40, 30, 21, 21);
                goto case 1;
            case 1:
                host.DoPhysics(1);
                state.Field230 = RunTimer(state.Field230);
                if (state.Field1F8 == 0)
                {
                    host.Body.Velocity = default;
                    state.Substate++;
                }
                break;
            case 2:
                state.Field230 = RunTimer(state.Field230);
                if (state.Field230 == 0) RecoverFromHit();
                break;
        }
    }

    /// <summary>The DetermineFightState(1) recovery shared by TakeHit and GetLaunched.</summary>
    private void RecoverFromHit()
    {
        if (!DetermineFightState(1))
        {
            PlaySingleAnim(0);
            SetState(22);
            return;
        }
        if (DistanceToPlayer(0) <= 500)
        {
            if (state.State == 8) SetState(5);
        }
        else if (state.State is 5 or 4) SetState(8);
    }

    /// <summary>CRhino::GetTrapped (0x480A40), states 16 and 17, struggling against the trap web's strand count.</summary>
    public void GetTrapped()
    {
        switch (state.Substate)
        {
            case 0:
                host.AttachStateSwitchMessage(state, 0xE);
                host.RunAnimation(0x1A, 0);
                state.Field1F8 = 5;
                state.Field34C = 0;
                state.Field350 = 0;
                state.Substate++;
                break;
            case 1:
                if (host.AnimationFinished) host.CycleAnimation(0x1B);
                state.Field348 |= 1;
                if (state.Field350 > 0) state.Field350--;
                state.Field1F8--;
                if (state.Field1F8 > 0) break;
                IThugTrapWeb? web = host.RecoverTrapWeb();
                if (web is null || web.StrandCount == state.Field34C)
                {
                    state.Substate++;
                    break;
                }
                state.Field350 = unchecked(state.Field350 + ((4000 * (web.StrandCount - state.Field34C)) >> 12));
                state.Field1F8 = 5;
                state.Field34C = web.StrandCount;
                break;
            case 2:
                state.Field348 |= 1;
                state.Field350 = RunTimer(state.Field350);
                if (state.Field350 > 0) break;
                host.RunAnimation(0x1B, host.AnimationId == 0x1B ? host.AnimationFrame : 0);
                state.Substate++;
                break;
            case 3:
                state.Field348 |= 1;
                if (!host.AnimationFinished) break;
                host.RunAnimation(0x1C, 0);
                state.Substate++;
                break;
            case 4:
                if (host.AnimationFrame < 10)
                {
                    state.Field348 |= 1;
                    break;
                }
                if (host.TrapWebHandleSet)
                {
                    host.RecoverTrapWeb()?.Burst();
                    host.ReleaseTrapWebHandle();
                }
                state.State = 0x11;
                state.Substate++;
                break;
            case 5:
                if (!host.AnimationFinished) break;
                if (!DetermineFightState(1))
                {
                    PlaySingleAnim(0);
                    SetState(0x16);
                }
                else if (state.State is 5 or 4 && DistanceToPlayer(0) > 500) SetState(8);
                break;
        }
    }

    /// <summary>CRhino::DieRhino (0x480DA0): demo-camera orbit, then CBaddy::Die after 60 ticks.</summary>
    public void DieRhino()
    {
        switch (state.Substate)
        {
            case 0:
                host.PlayPositionalSound(0x8043);
                state.Field310 = 0;
                host.Neutralize();
                state.BodyFlags &= ~0x10u;
                host.Actor.BehaviorFlags |= 0x1000;
                state.Field1F8 = 0;
                host.StopMyXA();
                PlaySingleAnim(0x1D);
                PlayXAPlease(0x16, 1, 0);
                state.Substate++;
                host.SetPlayerIgnoreInputTimer(0x8000);
                if (host.CameraPresent) host.BeginDeathCamera();
                break;
            case 1:
                if (host.CameraPresent)
                    host.SetCameraAngle(unchecked((short)(host.CameraAngle + (host.ElapsedTicks << 5))), 16);
                if (host.AnimationFinished) state.Substate = 4;
                break;
            case 4:
                state.Field1F8 = unchecked(state.Field1F8 + host.ElapsedTicks);
                if (state.Field1F8 <= 0x3C) break;
                host.Die();
                state.Substate = 3;
                break;
        }
    }
}
