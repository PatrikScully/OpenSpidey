using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public interface IThugAttackHost : IThugGuardHost, IActorSoundHost
{
    short PlayerHealth { get; }
    FixedVector3 PlayerPosition { get; }
    ActorBodyMotion? RecoverPlayerBody();
    int DistanceToPlayer(int cacheTolerance);
    int PathCheck(FixedVector3 from, FixedVector3 to, int clearance);
    bool AddPointToPath(FixedVector3 point, int argument);
    FixedVector3 GetPathPoint(int sourceIndex);
    void StartAttackMonitor(int startFrame, int attackMask, int frameCount, uint completionMask);
}

/// <summary>Source melee attack sequence, alignment, recovery and close-range retreat.</summary>
public static class ThugAttackBehavior
{
    public static void Step(ThugState state, ActorProcedureState actor, ActorProcedureList procedures,
        IThugAttackHost host, Func<int, int> random, int elapsedTicks)
    {
        switch (state.Substate)
        {
            case 0:
                state.TurnRate = 160;
                host.Neutralize();
                ThugAnimationBehavior.SetMode(state, 3, true, host);
                state.BodyFlags |= 0x10;
                state.Substate++;
                goto case 1;
            case 1:
                if (host.PlayerHealth <= 0)
                {
                    state.State = 28;
                    state.Substate = 0;
                    break;
                }
                procedures.Attach(ActorLookAtProcedure.ForBody(actor, host.RecoverPlayerBody, 2, 100, 0));
                state.ActionCounter = 120;
                state.Substate++;
                break;
            case 2:
                if (host.AnimationFinished != 0) host.CycleAnimation(state.IdleAnimation);
                state.ActionCounter = Math.Max(0, unchecked(state.ActionCounter - elapsedTicks));
                if (state.ActionCounter == 0)
                {
                    host.Neutralize();
                    state.State = 4;
                    state.Substate = 0;
                    return;
                }
                if ((actor.CompletionFlags & 2) == 0) return;
                actor.CompletionFlags &= ~2u;
                procedures.Mark(category: 0x100);
                actor.Body.AngularVelocity = actor.Body.AngularVelocity with { Y = 0 };
                actor.Body.AngularAcceleration = actor.Body.AngularAcceleration with { Y = 0 };
                if (state.Type == 304)
                {
                    if (host.DistanceToPlayer(0) < 120) BeginAttack(5, 0, 136, 5);
                    else if (random(2) != 0) BeginAttack(22, 4, 10, 10);
                    else BeginAttack(23, 3, 4096, 7);
                }
                else BeginAttack(13, 11, 23552, 10);
                host.PlaySound(0x8014);
                state.Substate++;
                return;
            case 3:
                if (host.AnimationFinished == 0) return;
                host.CycleAnimation(state.IdleAnimation);
                state.Substate++;
                if (TryRetreat(state, actor, host)) return;
                goto case 4;
            case 4:
                if (host.DistanceToPlayer(2) < (state.Type == 304 ? 140 : 190) &&
                    ThugPerception.SourceAbs(unchecked(actor.Body.Position.Y - host.PlayerPosition.Y)) < 204800)
                {
                    state.Substate = 1;
                    break;
                }
                state.State = 28;
                state.Substate = 0;
                break;
            case 5:
                if (host.AnimationFinished != 0) host.CycleAnimation(state.IdleAnimation);
                int pause = state.PauseUpdates;
                state.PauseUpdates = unchecked(pause - 1);
                if (pause <= 0) state.Substate = 1;
                break;
            case 6:
                if (host.AnimationFinished != 0) host.CycleAnimation((byte)(state.AnimationWord294 >> 8));
                state.ActionCounter = unchecked(state.ActionCounter - 1);
                if (state.ActionCounter != 0)
                {
                    FixedVector3 position = actor.Body.Position;
                    FixedVector3 target = host.GetPathPoint(state.PathPointIndex);
                    actor.Body.Position = new(unchecked(position.X + (unchecked(target.X - position.X) >> 1)),
                        position.Y, unchecked(position.Z + (unchecked(target.Z - position.Z) >> 1)));
                }
                else state.Substate = 4;
                break;
            default:
                throw new InvalidOperationException($"Unknown thug melee substate {state.Substate}.");
        }

        void BeginAttack(ushort animation, int startFrame, int attackMask, int frameCount)
        {
            host.RunAnimation(animation);
            host.StartAttackMonitor(startFrame, attackMask, frameCount, 0);
        }
    }

    public static bool TryRetreat(ThugState state, ActorProcedureState actor, IThugAttackHost host)
    {
        int minimum = state.Type == 304 ? 105 : 162;
        if (host.DistanceToPlayer(2) >= minimum) return false;
        int clearance = state.Type == 304 ? 120 : 185;
        int factor = state.Type == 304 ? 17 : 11;
        FixedVector3 position = actor.Body.Position;
        int deltaX = unchecked(position.X - host.PlayerPosition.X);
        int offsetX = unchecked(deltaX * unchecked(clearance - host.DistanceToPlayer(2)) * factor) >> 12;
        int deltaZ = unchecked(position.Z - host.PlayerPosition.Z);
        int offsetZ = unchecked(deltaZ * unchecked(clearance - host.DistanceToPlayer(2)) * factor) >> 12;
        int x = unchecked(position.X + offsetX);
        int z = unchecked(position.Z + offsetZ);
        var target = new FixedVector3(x, position.Y, z);
        if (host.PathCheck(position, target, 55) != 0 || !host.AddPointToPath(target, 0)) return false;
        state.Substate = 6;
        state.ActionCounter = 3;
        return true;
    }
}
