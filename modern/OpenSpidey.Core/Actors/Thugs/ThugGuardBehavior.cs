namespace OpenSpidey.Core.Actors.Thugs;

/// <summary>Animation and world effects used by CThug::Guard; supplied by a real actor adapter.</summary>
public interface IThugGuardHost : IActorMotionHost, IActorAnimationCycleHost
{
    int AnimationId { get; }
    int AnimationFrame { get; }
    byte AnimationFinished { get; set; }
    void RunAnimation(ushort clip, int from = 0, int to = -1);
    bool MonitorSpitPlease();
}

/// <summary>One call is one original Guard dispatch; wait counters are not multiplied by elapsed ticks.</summary>
public static class ThugGuardBehavior
{
    public static void Step(ThugState state, IThugGuardHost host, Func<int, int> random)
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.BodyFlags |= 0x10;
                if (state.Type != 304)
                {
                    host.RunAnimation(state.IdleAnimation);
                    state.ActionCounter = unchecked(random(5) + 8);
                    state.Substate = 5;
                    break;
                }
                state.Substate++;
                if (host.AnimationId == 11)
                {
                    host.AnimationFinished = 1;
                    break;
                }
                if (host.AnimationId != 10)
                {
                    host.RunAnimation(10);
                    break;
                }
                host.RunAnimation(10, host.AnimationFrame);
                goto case 1;
            case 1:
                if (host.AnimationFinished != 0)
                {
                    // CycleOrContinueAnim(11, -1, -1, -1) also accepts -1 as a current clip.
                    if (host.AnimationId != 11 && host.AnimationId != -1)
                        host.CycleAnimation(11);
                    state.PauseUpdates = unchecked(random(30) + 20);
                    state.Substate++;
                }
                break;
            case 2:
                int firstWait = state.PauseUpdates;
                state.PauseUpdates = unchecked(firstWait - 1);
                if (firstWait <= 0)
                {
                    host.RunAnimation(12);
                    state.Substate++;
                }
                break;
            case 3:
                if (host.AnimationFinished != 0)
                {
                    host.CycleAnimation(3);
                    state.PauseUpdates = unchecked(random(30) + 20);
                    state.Substate++;
                }
                break;
            case 4:
                int secondWait = state.PauseUpdates;
                state.PauseUpdates = unchecked(secondWait - 1);
                if (secondWait <= 0) state.Substate = 0;
                break;
            case 5:
                if (host.AnimationFinished != 0)
                {
                    state.ActionCounter = unchecked(state.ActionCounter - 1);
                    if (state.ActionCounter == 0)
                    {
                        state.ActionCounter = unchecked(random(5) + 8);
                        host.RunAnimation(21);
                        state.Substate++;
                    }
                    else host.RunAnimation(state.IdleAnimation);
                }
                break;
            case 6:
                if (host.MonitorSpitPlease()) state.Substate = 5;
                break;
            default:
                throw new InvalidOperationException($"Unknown thug guard substate {state.Substate}.");
        }
    }
}
