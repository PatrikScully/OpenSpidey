using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Simby;

public enum SimbyPlayerAi
{
    /// <summary>SpideyAI_WaitForSimbyGrab (0x4A8720).</summary>
    WaitForSimbyGrab,
    /// <summary>SpideyAI_ThrownBySimby (0x4A8680).</summary>
    ThrownBySimby
}

public sealed partial class SimbyBrain
{
    /// <summary>CSimby::GettingGrabbed (0x4A7FA0): the player's grab, the Simby's counter and its throw.</summary>
    public void GettingGrabbed()
    {
        if (host.PlayerAnimationId == 124 && unchecked((short)host.AnimationId) != 29) host.RunAnimation(29);
        switch (state.Substate)
        {
            case 0:
                host.RunAnimation(26);
                state.Field230 = 180;
                state.Substate++;
                host.PlayPositionalSound(0x816C);
                break;
            case 1:
                RunField230();
                if (host.AnimationFinished) RunHeldAnimation();
                if ((host.Actor.CompletionFlags & 1) != 0)
                {
                    host.Actor.CompletionFlags &= ~1u;
                    host.Body.AngularVelocity = host.Body.AngularVelocity with { Y = 0 };
                    host.Body.AngularAcceleration = host.Body.AngularAcceleration with { Y = 0 };
                    state.Field1F0--;
                    host.Body.Angles = host.Body.Angles with { Y = host.PlayerYaw };
                    state.Substate++;
                    host.Neutralize();
                }
                else YawTowards(host.PlayerYaw, 100);
                break;
            case 2:
                RunField230();
                if (state.Field230 != 0)
                {
                    FixedVector3 position = host.Body.Position;
                    short yaw = host.Body.Angles.Y;
                    bool held = host.GrabUpdate(ref position, ref yaw);
                    host.Body.Position = position;
                    host.Body.Angles = host.Body.Angles with { Y = yaw };
                    if (!held) state.CombatFlags &= ~0x40u;
                    if (host.AnimationFinished) RunHeldAnimation();
                }
                else if (host.PlayerActionState == 0x8000000)
                {
                    host.RunAnimation(48);
                    state.Substate = 10;
                }
                else
                {
                    host.RunAnimation(49);
                    host.AttachAttackMonitor(20, 2, 8, 16);
                    state.Substate++;
                }
                break;
            case 3:
                if ((host.Actor.CompletionFlags & 0x10) != 0)
                {
                    host.Actor.CompletionFlags &= ~0x10u;
                    KnockSpideyDown(10);
                    state.Substate++;
                }
                FinishCounter();
                break;
            case 4:
                FinishCounter();
                break;
            case 10:
                host.SetPlayerAi(SimbyPlayerAi.WaitForSimbyGrab);
                state.PlayerAiSaved = true;
                state.Substate++;
                break;
            case 11:
                if (host.AnimationFrame < 5) break;
                host.SetPlayerAi(SimbyPlayerAi.ThrownBySimby);
                CarryPlayer();
                break;
            case 12:
                CarryPlayer();
                break;
            case 13:
                ThrowPlayer();
                break;
            default:
                throw new InvalidDataException($"Unknown CSimby GettingGrabbed substate {state.Substate}.");
        }
    }

    private void RunHeldAnimation()
    {
        if (host.PlayerActionState == 0x8000000)
        {
            short clip = unchecked((short)host.AnimationId);
            host.RunAnimation(clip is 47 or 46 ? 47 : 46);
            state.BehaviorFlags |= 4;
        }
        else
        {
            host.RunAnimation(27);
            state.BehaviorFlags &= ~4u;
        }
    }

    private void FinishCounter()
    {
        if (unchecked((short)host.AnimationId) == 49)
        {
            if (host.AnimationFrame > 48)
            {
                if (state.SlimeHandle == 0)
                {
                    host.CreateSlimeQuad(host.Body.Position with { Y = unchecked((state.SurfaceClearance << 12) + host.Body.Position.Y) }, 0, true);
                    state.SlimeHandle = 1;
                }
            }
            else if (host.AnimationFrame >= 12)
            {
                state.Field3A0 |= 2;
                ReleaseSlime();
            }
        }
        if (!host.AnimationFinished) return;
        state.Stage = 4;
        state.Substate = 0;
    }

    private void CarryPlayer()
    {
        FixedVector3 right = host.DynamicHookPosition(new ActorHook(0, 0, 0, 14));
        FixedVector3 left = host.DynamicHookPosition(new ActorHook(0, 0, 0, 11));
        state.HandPosition = (left + right) >> 1;
        if (!host.LineOfSight(host.PlayerPosition, state.HandPosition, out _))
        {
            state.Substate++;
            return;
        }
        state.GrabHandPosition = state.HandPosition;
        host.PlayerPosition = (state.GrabHandPosition >> 1) + (host.PlayerPosition >> 1);
        if (host.AnimationFrame >= 35) ThrowPlayer();
    }

    private void ThrowPlayer()
    {
        host.RestorePlayerAi();
        state.PlayerAiSaved = false;
        // The original leaves SHitInfo's part byte and attack kind uninitialized on the stack.
        host.HitPlayer(new ActorHit(4, 0, 0, 10, default));
        host.PlayerActionState = 0x800000;
        host.PlayPlayerSingleAnimation(176);
        FixedVector3 throwVelocity = FixedAim.Velocity(256, new AimAngles(host.Body.Angles.X, host.Body.Angles.Y)) with { Y = -65536 };
        host.PlayerVelocity = throwVelocity;
        if (!host.LineOfSight(host.PlayerPosition, host.PlayerPosition + throwVelocity, out _))
            host.PlayerVelocity = throwVelocity >> 2;
        state.Stage = 4;
        state.Substate = 0;
    }

    /// <summary>CBaddy::YawTowards (0x4030C0): keeps turning while the new yaw stays on the old side of the target.</summary>
    private void YawTowards(int target, int rate)
    {
        // Shared ActorYawSteering inverts the 0x403132 side test (it was generated from the recovered C++), so the
        // Simby keeps the executable's form here.
        ActorBodyMotion body = host.Body;
        int previous = body.Angles.Y;
        int residual = unchecked(target - previous);
        if (residual < -2048) residual = unchecked(residual + 4096);
        if (residual > 2048) residual = unchecked(residual - 4096);
        if (residual != 0)
        {
            int step = unchecked(rate * residual) >> 8;
            short yaw = unchecked((short)(previous + step));
            body.Angles = body.Angles with { Y = yaw };
            if (step != 0 && (unchecked(yaw - target) > 0) == (unchecked(previous - target) > 0)) return;
            body.Angles = body.Angles with { Y = unchecked((short)target) };
        }
        body.AngularAcceleration = body.AngularAcceleration with { Y = 0 };
        body.AngularVelocity = body.AngularVelocity with { Y = 0 };
    }

    /// <summary>CSimby::SimbyKnockSpideyDown (0x4AA660).</summary>
    private void KnockSpideyDown(int damage)
    {
        FixedVector3 away = FixedVectorMath.Normalize((host.Body.Position - host.PlayerPosition) >> 12);
        host.CreatePlayerImpactEffect(host.PlayerPosition + away * 50, 0);
        host.HitPlayer(new ActorHit(6, 0, 14, unchecked((ushort)damage), default));
    }

    /// <summary>CSimby::AI release of a held player once the Simby has left GettingGrabbed.</summary>
    private void ReleaseHeldPlayer()
    {
        if (!state.PlayerAiSaved || state.Stage == 14) return;
        host.PlayerActionState = 0x800000;
        host.PlayPlayerSingleAnimation(176);
        host.RestorePlayerAi();
        state.PlayerAiSaved = false;
    }
}
