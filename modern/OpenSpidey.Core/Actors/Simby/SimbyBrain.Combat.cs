using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Simby;

public sealed partial class SimbyBrain
{
    /// <summary>CSimby::WalkToTarget (0x4AA4D0).</summary>
    public void WalkToTarget()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                host.HeightQueryInterval = 20;
                host.AttachMoveTo(state.TargetPosition, 150, 80, 500, 1);
                state.Substate++;
                break;
            case 1:
                RunAppropriateAnim();
                if (host.PlayerDistance < unchecked((uint)(state.MinorRadius + host.PlayerMinorRadius)))
                {
                    host.Neutralize();
                    planner.RegisterTargetPos(host.Body.Position);
                    if (host.PlayerSpecialState == 0)
                    {
                        state.Stage = 17;
                        state.Substate = 0;
                        break;
                    }
                    host.RunAnimation(unchecked((byte)state.SetupHigh));
                    state.Substate++;
                    break;
                }
                if ((host.Actor.CompletionFlags & 1) == 0) break;
                host.Actor.CompletionFlags &= ~1u;
                state.Stage = 4;
                state.Substate = 0;
                break;
            case 2:
                if (!host.AnimationFinished) break;
                state.Stage = 4;
                state.Substate = 0;
                break;
            default:
                throw new InvalidDataException($"Unknown CSimby WalkToTarget substate {state.Substate}.");
        }
    }

    /// <summary>CSimby::SlideToAttack (0x4AA770).</summary>
    public void SlideToAttack()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                host.AttachMoveTo(state.TargetPosition, 150, 0, 0, 1);
                host.AttachLookAtPlayer(0, 80, 0);
                host.PlayPositionalSound((358 - (host.Random(2) != 0 ? 1 : 0)) | 0x8000);
                state.Field1F8 = 0;
                state.Substate++;
                break;
            case 1:
            {
                RunAppropriateAnim();
                int probes = state.Field1F8++;
                if (probes >= 3)
                {
                    state.Field1F8 = 0;
                    if (host.PlayerSpecialState == 0 && PlayerLevel()
                        && planner.DistanceToPlayer(2) < 350
                        && host.LineOfSight(host.Body.Position, host.PlayerPosition, out _)
                        && planner.RegisterTargetPos(host.Body.Position))
                    {
                        host.Neutralize();
                        state.Stage = 17;
                        state.Substate = 0;
                        break;
                    }
                }
                if ((host.Actor.CompletionFlags & 1) == 0) break;
                host.Actor.CompletionFlags &= ~1u;
                state.Stage = host.PlayerSpecialState == 0 && PlayerLevel() ? 17 : 4;
                state.Substate = 0;
                break;
            }
            default:
                throw new InvalidDataException($"Unknown CSimby SlideToAttack substate {state.Substate}.");
        }
    }

    /// <summary>CSimby::AttackPlayer (0x4AD520).</summary>
    public void AttackPlayer()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                if (state.AlertModeTimer < 2400) state.AlertModeTimer = 2400;
                state.Field310 = 100;
                if ((state.BehaviorFlags & 0x2700) != 0) break;
                ClearJointTranslations();
                if (unchecked((byte)(state.SetupHigh >> 8)) != 0)
                {
                    LatchIdleAnim();
                    break;
                }
                host.CycleAnimation(unchecked((byte)state.SetupHigh));
                host.AttachLookAtPlayer(2, 80, 0);
                state.Substate++;
                break;
            case 1:
                if ((host.Actor.CompletionFlags & 2) == 0) break;
                host.Actor.CompletionFlags &= ~2u;
                if (!host.LineOfSight(host.Body.Position, host.PlayerPosition, out _))
                {
                    state.Stage = 4;
                    state.Substate = 0;
                    break;
                }
                ChooseAttack();
                break;
            case 2:
                StretchStep(0x100, 11, 12, 41);
                break;
            case 3:
                StretchStep(0x200, 8, 9, 42);
                break;
            case 4:
                if (!host.AnimationFinished) break;
                state.Stage = state.AttackMask != 0 || host.PlayerHealth <= 0 ? 4 : 9;
                state.Substate = 0;
                break;
            case 5:
                if (host.AnimationFrame < 10) break;
                state.BehaviorFlags |= 0x400;
                state.Field230 = unchecked(state.Field230 + host.ElapsedTicks);
                if (!DoStretch(6, 5) && state.Field230 >= 20) state.Substate = 4;
                break;
            case 10:
                if (host.AnimationFinished) state.Substate = 0;
                break;
            default:
                throw new InvalidDataException($"Unknown CSimby AttackPlayer substate {state.Substate}.");
        }
    }

    private void ChooseAttack()
    {
        int pick;
        if (planner.DistanceToPlayer(0) <= 200) pick = host.Random(2);
        else if (planner.DistanceToPlayer(0) >= 350) pick = host.Random(4);
        else pick = host.Random(5);
        state.Field1F8 = pick;
        if (pick < 4) host.PlayPositionalSound(0x816D);
        switch (pick)
        {
            case 0: StartAttack(455, 15, 24832, 9, 2); return;
            case 1: StartAttack(455, 14, 3328, 9, 3); return;
            case 2: StartAttack(204, 41, 24832, 20, 2); return;
            case 3: StartAttack(204, 42, 3328, 20, 3); return;
            case 4:
                state.StretchScale = 204;
                host.RunAnimation(16);
                host.PlayPositionalSound((342 - (host.Random(2) != 0 ? 1 : 0)) | 0x8000);
                host.AttachAttackMonitor(10, 384, 20, 0);
                state.Substate = 5;
                state.Field230 = 0;
                return;
            default:
                state.Field230 = 0;
                return;
        }
    }

    private void StartAttack(int scale, int animation, uint hooks, int frames, int substate)
    {
        state.StretchScale = scale;
        state.Field1F8 = 10;
        host.RunAnimation(animation);
        host.AttachAttackMonitor(10, hooks, frames, 0);
        state.Substate = substate;
        state.Field230 = 0;
    }

    private void StretchStep(uint arm, int first, int second, int shortAnimation)
    {
        if (host.AnimationFrame < state.Field1F8) return;
        state.BehaviorFlags |= arm;
        state.Field230 = unchecked(state.Field230 + host.ElapsedTicks);
        if (DoStretch(first, second)) return;
        int limit = unchecked((short)(host.AnimationId - shortAnimation)) != 0 ? 9 : 20;
        if (state.Field230 >= limit) state.Substate = 4;
    }

    /// <summary>CSimby_DoStretch (0x4AD210): reaches the stretching arm's links toward the player; true when it gave up.</summary>
    public bool DoStretch(int first, int second)
    {
        host.ItemFlags |= 4;
        state.Field3A0 |= 1;
        FixedVector3 aim = host.PlayerPosition with { Y = unchecked(host.PlayerPosition.Y - 139264) };
        state.Joints[second] = state.Joints[second] with { TranslationX = 0, TranslationY = 0, TranslationZ = 0 };
        state.Joints[first] = state.Joints[first] with { TranslationX = 0, TranslationY = 0, TranslationZ = 0 };
        host.ApplyPose();
        // The 0x700 switch leaves the hook offset uninitialized when no arm bit is set; every caller sets one.
        ushort part = (state.BehaviorFlags & 0x700) switch
        {
            0x100 => 14,
            0x200 => 11,
            0x400 => 8,
            _ => throw new InvalidOperationException("CSimby_DoStretch requires exactly one 0x700 arm bit.")
        };
        FixedVector3 hand = host.DynamicHookPosition(new ActorHook(0, 0, 0, part));
        FixedVector3 reach = ((aim - hand) >> 12) * state.Field230 * state.StretchScale;
        FixedVector3 tip = reach + hand;
        reach >>= 12;
        if (planner.DistanceToPlayer(2) > 525 || Absolute(unchecked(host.Body.Position.Y - host.PlayerPosition.Y)) > 1638400
            || !host.LineOfSight(host.Body.Position, tip, out _))
        {
            state.Substate = 4;
            return true;
        }
        FixedVector3 local = FixedRotation.TransformTranspose(host.TransformBasis, reach);
        var translation = new RawJointPose(0, 0, 0, Half(local.X), Half(local.Y), Half(local.Z));
        state.Joints[first] = state.Joints[first] with
        {
            TranslationX = translation.TranslationX, TranslationY = translation.TranslationY, TranslationZ = translation.TranslationZ
        };
        state.Joints[second] = state.Joints[second] with
        {
            TranslationX = translation.TranslationX, TranslationY = translation.TranslationY, TranslationZ = translation.TranslationZ
        };
        return false;

        static short Half(int value) => unchecked((short)(unchecked(16 * value) / 2));
    }

    /// <summary>CSimby::AI tail (0x4AF00A): relaxes the stretched arm links, then ApplyPose instead of M3d_BuildTransform.</summary>
    private void DisplayStretchedPose()
    {
        bool relaxed = false;
        if ((state.Field3A0 & 1) == 0 && (state.BehaviorFlags & 0x700) != 0)
        {
            (int first, int second) = (state.BehaviorFlags & 0x100) != 0 ? (11, 12)
                : (state.BehaviorFlags & 0x200) != 0 ? (8, 9)
                : (6, 5);
            RawJointPose a = Halve(state.Joints[first]);
            state.Joints[first] = a;
            RawJointPose b = Halve(state.Joints[second]);
            state.Joints[second] = b;
            int spread = Absolute(a.TranslationX) | Absolute(a.TranslationY) | Absolute(a.TranslationZ)
                | Absolute(b.TranslationX) | Absolute(b.TranslationY) | Absolute(b.TranslationZ);
            if (spread < 10)
            {
                relaxed = true;
                state.BehaviorFlags &= ~0x700u;
            }
        }
        host.ApplyPose();
        if (relaxed) host.ItemFlags = unchecked((ushort)(host.ItemFlags & ~4));

        static RawJointPose Halve(RawJointPose joint) => joint with
        {
            TranslationX = unchecked((short)(joint.TranslationX >> 1)),
            TranslationY = unchecked((short)(joint.TranslationY >> 1)),
            TranslationZ = unchecked((short)(joint.TranslationZ >> 1))
        };
    }

    private void ClearJointTranslations()
    {
        for (int i = 0; i < state.Joints.Length; i++)
            state.Joints[i] = state.Joints[i] with { TranslationX = 0, TranslationY = 0, TranslationZ = 0 };
    }

    /// <summary>CSimby::Shoot (0x4ACFB0) with SetUpHandPos inlined in case 2.</summary>
    public void Shoot()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                host.CycleAnimation(unchecked((byte)state.SetupHigh));
                host.AttachLookAtPlayer(2, 80, 0);
                state.Substate++;
                break;
            case 1:
                if ((host.Actor.CompletionFlags & 2) == 0) break;
                host.Actor.CompletionFlags &= ~2u;
                host.RunAnimation(0x2B);
                state.Substate++;
                break;
            case 2:
                if (host.AnimationFrame < 14) break;
                FixedVector3 right = host.DynamicHookPosition(new ActorHook(0, 0, 0, 14));
                FixedVector3 left = host.DynamicHookPosition(new ActorHook(0, 0, 0, 11));
                state.HandPosition = (left + right) >> 1;
                host.SpawnShot(state.HandPosition);
                host.PlayPositionalSound(0x815C);
                state.Substate++;
                break;
            case 3:
                if (!host.AnimationFinished) break;
                state.Stage = 4;
                state.Field324 = 450 - host.Random(150);
                state.Substate = 0;
                break;
            default:
                throw new InvalidDataException($"Unknown CSimby Shoot substate {state.Substate}.");
        }
    }

    /// <summary>CSimby::TakeHit (0x4ADB00).</summary>
    public void TakeHit()
    {
        switch (state.Substate)
        {
            case 0:
                state.Field310 = 0;
                host.AttachLookAtPlayer(0, 80, 200);
                RunAppropriateHitAnim();
                state.Field230 = 10;
                state.Substate++;
                break;
            case 1:
            {
                int timer = state.Field230;
                host.RunTimer(ref timer);
                state.Field230 = timer;
                if (timer != 0) break;
                host.RunAnimation(unchecked((byte)state.SetupHigh));
                state.Stage = 4;
                state.Substate = 0;
                break;
            }
            default:
                throw new InvalidDataException($"Unknown CSimby TakeHit substate {state.Substate}.");
        }
    }

    /// <summary>CSimby::RunAppropriateHitAnim.</summary>
    private void RunAppropriateHitAnim()
    {
        if ((state.BehaviorFlags & 0x10) != 0) host.RunAnimation(17);
        else if ((state.BehaviorFlags & 0x20) != 0) host.RunAnimation(18);
        else host.RunAnimation(19);
    }

    private bool PlayerLevel() => Absolute(unchecked(host.Body.Position.Y - host.PlayerPosition.Y)) < 409600;
}
