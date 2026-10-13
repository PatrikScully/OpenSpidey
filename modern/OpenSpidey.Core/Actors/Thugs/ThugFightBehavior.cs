using OpenSpidey.Core.Actors.Procedures;

namespace OpenSpidey.Core.Actors.Thugs;

/// <summary>Recovered DetermineFightState, retaining path-query reuse and signal/early-return distinctions.</summary>
public static class ThugFightBehavior
{
    public static bool Determine(ThugState state, ActorProcedureState actor, IThugFightHost host)
    {
        int initialState = state.State;
        int distance = ThugPerception.DistanceToPlayer(state, actor.Body, host.Player, host.AttackClock, 2);
        int cachedPath = -1;
        if (state.Health <= 0 || host.Player.Health <= 0 || host.Player.UnavailableFlag != 0) return false;
        if (distance <= 1000) state.FightPollInterval = 3;
        else if (distance <= 2000) state.FightPollInterval = 15;
        else state.FightPollInterval = 31;
        if (state.AlertTicks == 0 && distance < 300 && state.ClosePlayerTicks == 0 &&
            ThugPerception.SourceAbs(unchecked(host.Player.Position.Y - actor.Body.Position.Y)) < 819200)
            state.ClosePlayerTicks = 60;
        if (state.FightSuppressionTicks != 0) return false;

        if (!host.LineOfSight(actor.Body.Position, host.Player.Position, 0))
        {
            if ((actor.BehaviorFlags & 0x800) != 0)
            {
                actor.BehaviorFlags &= ~0x800u;
                if (host.PathCheck(actor.Body.Position, state.LastSeenPlayerPosition, 55) == 0 &&
                    host.AddPointToPath(actor.Body.Position, 0) && host.AddPointToPath(state.LastSeenPlayerPosition, 0))
                {
                    state.State = 24;
                    state.Substate = 0;
                    return true;
                }
            }
            else if (state.AlertTicks != 0 && state.State is not 23 and not 24)
            {
                state.State = 23;
                state.Substate = 0;
                return true;
            }
            return false;
        }

        if (state.AlertTicks != 0 && distance < state.Field370)
        {
            cachedPath = host.PathCheck(actor.Body.Position, host.Player.Position, 55);
            if (cachedPath == 0) return ChangeState(4);
        }

        if (!ThugPerception.CheckSightCone(state, actor.Body, host.Player, host.AttackClock))
        {
            TryClaimFight();
            return ReportChange();
        }
        if (state.AlertTicks == 0) return ChangeState(3);
        if (cachedPath == -1) cachedPath = host.PathCheck(actor.Body.Position, host.Player.Position, 55);
        if (cachedPath == 0) return ChangeState(4);
        if (state.Type != 304 && state.RangedCooldownTicks == 0 &&
            ThugPerception.DistanceToPlayer(state, actor.Body, host.Player, host.AttackClock, 4) > 500 &&
            ThugPerception.DistanceToPlayer(state, actor.Body, host.Player, host.AttackClock, 4) < state.Field384)
            return ChangeState(11);
        if (state.State == 1)
        {
            TryClaimFight();
            return ReportChange();
        }
        state.State = 4;
        state.Substate = 0;
        TryClaimFight();
        return ReportChange();

        bool ChangeState(int next)
        {
            state.State = next;
            state.Substate = 0;
            return ReportChange();
        }

        void TryClaimFight()
        {
            if (host.Player.UnavailableFlag != 0 || host.HasFightOwner || host.Player.HoldsObject ||
                state.AlertTicks == 0 || distance >= 1500) return;
            int path = cachedPath == -1 ? host.PathCheck(actor.Body.Position, host.Player.Position, 55) : cachedPath;
            if (path == 0) return;
            host.Neutralize();
            host.SetFightOwner();
            host.ClearAttackOwnership();
            state.State = 9;
            state.Substate = 0;
        }

        bool ReportChange()
        {
            if (initialState == state.State) return false;
            state.BodyFlags |= 0x10;
            host.SendSignal();
            return true;
        }
    }
}
