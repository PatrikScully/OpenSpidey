using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;

namespace OpenSpidey.Core.Actors.Cops;

/// <summary>Recovered CCop::ShootPlayer (0x42BFC0).</summary>
public static class CopRangedBehavior
{
    public static void ShootPlayer(CopState state, ActorProcedureState actor, ActorProcedureList procedures,
        ICopHost host, Func<int, int> random, int elapsedTicks, Func<ushort, bool> setAnimMode,
        Func<bool> playerIsVisible, Func<int, int> distanceToPlayer)
    {
        switch (state.Substate)
        {
            case 0:
                state.AlertTicks = 300;
                state.TurnRate = 20;
                host.Neutralize();
                if (setAnimMode(1)) state.Substate = 6;
                else
                {
                    state.CombatFlags &= ~0x1000u;
                    procedures.Attach(new CopStateSwitchMessageProcedure(state, 12, host.SendSelfMessage));
                    procedures.Attach(new CopStateSwitchMessageProcedure(state, 17, host.SendSelfMessage));
                    procedures.Attach(ActorLookAtProcedure.ForBody(actor, () => host.PlayerBody, 2, 130, 4));
                    host.CycleAnimation(29);
                    state.ActionCounter = 120;
                    state.Substate++;
                }
                break;
            case 1:
                state.ActionCounter = Math.Max(0, unchecked(state.ActionCounter - elapsedTicks));
                if (state.ActionCounter != 0)
                {
                    if (host.Player.Health <= 0 || host.Player.HoldsObject) goto FinishShooting;
                    host.DrawLaserSite(true);
                    if ((actor.CompletionFlags & 2) != 0)
                    {
                        actor.CompletionFlags &= ~2u;
                        state.Substate++;
                        state.AimPlayerY = host.Player.Position.Y;
                        state.PauseUpdates = 15;
                        state.ActionCounter = unchecked(random(3) + 1);
                    }
                }
                else
                {
                    host.Neutralize();
                    state.State = 4;
                    state.Substate = 0;
                }
                break;
            case 2:
                if (host.Player.Health <= 0 || host.Player.HoldsObject) goto FinishShooting;
                host.DrawLaserSite(true);
                int pause = state.PauseUpdates;
                state.PauseUpdates = unchecked(pause - 1);
                if (pause == 7)
                {
                    if (!host.Player.HoldsObject) host.PlayPositionSound(0x801D, actor.Body.Position, 100);
                }
                else if (pause == 1)
                {
                    state.Substate++;
                    host.RunAnimation(state.Type != 306 ? (ushort)29 : (ushort)10);
                }
                break;
            case 3:
                if (host.AnimationFinished != 0)
                {
                    host.RunAnimation(11);
                    state.Substate++;
                }
                break;
            case 4:
                if (host.AnimationFinished != 0)
                {
                    state.ActionCounter = unchecked(state.ActionCounter - 1);
                    if (state.ActionCounter > 0 && playerIsVisible())
                    {
                        if (state.Type != 306 && distanceToPlayer(2) < 650 && HeightWithin())
                        {
                            state.Substate++;
                            break;
                        }
                        state.CombatFlags &= ~0x1000u;
                        host.RunAnimation(11);
                    }
                    else
                    {
                        state.Substate++;
                        break;
                    }
                }
                if (host.Player.HoldsObject) goto FinishShooting;
                if (host.DrawLaserSite(true) != 0)
                {
                    host.ApplyPlayerHit(4, unchecked((short)state.Field374));
                    if (host.Player.Health <= 0) host.PlayPositionSound(0x8024, actor.Body.Position, 0); // CCop::Victorious 0x42BFA0
                }
                if (state.Type == 306 && (host.AttackClock & 3) == 0 && distanceToPlayer(2) < 650 && HeightWithin())
                    state.Substate++;
                break;
            case 5:
                host.ClearLaser();
                host.PlayPositionSound(unchecked((ushort)((random(2) + 30) | 0x8000)), actor.Body.Position, 0);
                goto FinishShooting;
            case 6:
                if (host.AnimationFinished != 0) state.Substate = 0;
                break;
            default:
                throw new InvalidOperationException("Unknown substate!");
        }
        return;

    FinishShooting:
        state.State = 10;
        state.Substate = 0;

        bool HeightWithin() =>
            ThugPerception.SourceAbs(unchecked(host.Player.Position.Y - state.GroundReferenceY - 0x4000)) < 409600;
    }
}
