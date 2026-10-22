using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Simby;

public sealed partial class SimbyBrain
{
    /// <summary>CSimby::GetLaunched (0x4A7BD0); DoPhysics(1) drains the flight budget in Field1F8.</summary>
    public void GetLaunched()
    {
        switch (state.Substate)
        {
            case 0:
                host.PlayPositionalSound((356 - (host.Random(2) != 0 ? 1 : 0)) | 0x8000);
                state.Substate++;
                ReleaseSlime();
                if ((state.BehaviorFlags & 0x40) != 0)
                {
                    if (unchecked((byte)(state.SetupHigh >> 8)) == 0)
                    {
                        state.SetupLow = host.IdleSetupLow;
                        state.SetupHigh = host.IdleSetupHigh;
                    }
                    FixedVector3 ahead = host.Body.Position + host.Body.Velocity * 40;
                    host.AttachLookAtPoint(ahead, 2, 80, 200);
                }
                else
                {
                    host.AttachLookAtPlayer(2, 80, 0);
                    if (unchecked((byte)(state.SetupHigh >> 8)) != 0) RunAppropriateHitAnim();
                    else
                    {
                        host.RunAnimation(6);
                        state.SetupLow = host.IdleSetupLow;
                        state.SetupHigh = host.IdleSetupHigh;
                    }
                }
                state.Field230 = 10;
                Fly();
                break;
            case 1:
                Fly();
                break;
            case 2:
                Land();
                break;
            default:
                throw new InvalidDataException($"Unknown CSimby GetLaunched substate {state.Substate}.");
        }

        void Fly()
        {
            if ((state.BehaviorFlags & 0x40) != 0)
            {
                state.Field3A0 |= 2;
                if (host.AnimationFinished) host.CycleAnimation(30);
            }
            host.DoPhysics(1);
            RunField230();
            if (state.Field1F8 != 0) return;
            host.Body.Velocity = default;
            state.Substate++;
            Land();
        }

        void Land()
        {
            RunField230();
            if (state.Field230 != 0) return;
            state.Stage = 8;
            state.Substate = 0;
        }
    }

    /// <summary>CSimby::Trapped (0x4A87B0) with FireTrappedToDeath inlined.</summary>
    public void Trapped()
    {
        if (state.Substate > 0 && host.TrapWebBurning())
        {
            state.Stage = 23;
            state.Substate = 0;
            return;
        }
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                if (state.AlertModeTimer < 2400) state.AlertModeTimer = 2400;
                host.AttachStateSwitchMessage(14);
                state.Substate++;
                if (unchecked((byte)(state.SetupHigh >> 8)) != 1)
                {
                    StartStruggle();
                    break;
                }
                host.RunAnimation(6);
                state.SetupLow = host.IdleSetupLow;
                state.SetupHigh = host.IdleSetupHigh;
                state.Substate = 6;
                break;
            case 1:
                StartStruggle();
                break;
            case 2:
                if (!host.AnimationFinished) break;
                host.RunAnimation(0x18);
                state.Substate++;
                break;
            case 3:
                if (!host.AnimationFinished) break;
                host.RunAnimation(0x19);
                state.Substate++;
                break;
            case 4:
                if (host.AnimationFrame >= 28)
                {
                    state.Substate = 5;
                    state.Stage = 13;
                    if ((world.CountResetFlag & 1) == 0 && host.PlayerCanTalk() && host.PlayAndAttachPlayerXA(47, 4, 100))
                        world.CountResetFlag |= 1;
                }
                FinishTrapped();
                break;
            case 5:
                FinishTrapped();
                break;
            case 6:
                if (host.AnimationFinished) state.Substate = 1;
                break;
            default:
                throw new InvalidDataException($"Unknown CSimby Trapped substate {state.Substate}.");
        }

        void StartStruggle()
        {
            host.RunAnimation(0x17);
            state.Substate++;
        }

        void FinishTrapped()
        {
            if (!host.AnimationFinished) return;
            state.Stage = 4;
            state.Substate = 0;
        }
    }

    /// <summary>CSimby::DieWhileFalling (0x4ADD50). Substate 2 is the original's silent post-Die diagnostic.</summary>
    public void DieWhileFalling()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.Field230 = 0;
                state.DropSampleY = host.GroundHeight(host.Body.Position, 0, 6000, 0);
                host.CycleAnimation(36);
                state.Field230 = 122880;
                state.Field1F8 = 0;
                state.Substate++;
                break;
            case 1:
            {
                int frames = state.Field1F8++;
                if (frames > 75)
                {
                    state.CombatFlags |= 0x1000;
                    state.MinorRadius = 0;
                    host.NotifyPlayerKill();
                    host.Die();
                    state.Substate++;
                    break;
                }
                int speed = state.Field230;
                int y = unchecked(host.Body.Position.Y + speed);
                host.Body.Position = host.Body.Position with { Y = y };
                if (state.DropSampleY == -1 || y <= state.DropSampleY)
                {
                    state.Field230 = unchecked(speed + 122880);
                    if (state.Field230 > 1228800) state.Field230 = 300;
                    break;
                }
                state.Health = 0;
                state.Substate = 0;
                host.Body.Position = host.Body.Position with { Y = unchecked(state.DropSampleY - (state.SurfaceClearance << 12)) };
                state.Stage = 22;
                break;
            }
            case 2:
                break;
            default:
                throw new InvalidDataException($"Unknown CSimby DieWhileFalling substate {state.Substate}.");
        }
    }

    /// <summary>CSimby::DieSimby (0x4ADEA0).</summary>
    public void DieSimby()
    {
        switch (state.Substate)
        {
            case 0:
            {
                host.SetHeight(true, 100, 600);
                state.Field310 = 0;
                host.Neutralize();
                state.BodyFlags &= ~0x10u;
                ReleaseSlime();
                state.CombatFlags |= 0x1000;
                host.SplattyDeath();
                host.PlayPositionalSound((347 - (host.Random(2) != 0 ? 1 : 0)) | 0x8000);
                FixedVector3 basePosition = host.Body.Position;
                int ground = host.GroundHeight(basePosition, 0, 150, 0);
                if (ground != -1) basePosition = basePosition with { Y = ground };
                host.SpawnSymBase(basePosition, host.Body.Angles, false);
                host.NotifyPlayerKill();
                host.Die();
                state.Substate++;
                break;
            }
            case 1:
                break;
            default:
                throw new InvalidDataException($"Unknown CSimby DieSimby substate {state.Substate}.");
        }
    }

    /// <summary>CSimby::DieFireyDeath (0x4ADC30); CSimbyFireDeath owns BehaviorFlags bit 0x1000 until it finishes.</summary>
    public void DieFireyDeath()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                host.RunAnimation(0x2C);
                ReleaseSlime();
                host.SpawnFireDeath();
                host.PlayPositionalSound(33111);
                state.Substate++;
                break;
            case 1:
                if (host.AnimationFinished) host.CycleAnimation(45);
                if ((state.BehaviorFlags & 0x1000) != 0) break;
                host.NotifyPlayerKill();
                host.Die();
                state.Substate++;
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException($"Unknown CSimby DieFireyDeath substate {state.Substate}.");
        }
    }

    /// <summary>CSimbySlimeBase scale-down-and-die request (fields 0xA4 = -32, 0x9C = 1, 0x3A = 0) and detach.</summary>
    private void ReleaseSlime()
    {
        if (state.SlimeHandle == 0) return;
        host.ReleaseSlimeQuad();
        state.SlimeHandle = 0;
    }

    private void RunField230()
    {
        int timer = state.Field230;
        host.RunTimer(ref timer);
        state.Field230 = timer;
    }
}
