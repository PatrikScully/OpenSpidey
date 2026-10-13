using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public interface IThugInitializationHost : IActorAnimationCycleHost, IActorHeightAdjustmentHost
{
    FixedVector3 Position { get; set; }
    int FindGroundHeight(int above, int below);
    void StoreGroundPlacement(int bodyHeight, int groundHeight);
}

/// <summary>Initial floor placement and selection of the first waypoint or guard state.</summary>
public static class ThugInitializationBehavior
{
    public static void Step(ThugState state, ActorWaypointRoute route, IThugInitializationHost host, int attackCounter)
    {
        switch (state.Substate)
        {
            case 0:
                state.AlertTicks = 0;
                if (unchecked(attackCounter - state.AttackRelatedSnapshot) > 10 && (attackCounter & 15) != 0) return;
                int ground = host.FindGroundHeight(300, 300);
                if (ground == -1) return;
                int height = unchecked(ground - (state.SurfaceClearance << 12));
                host.Position = host.Position with { Y = height };
                host.StoreGroundPlacement(height, ground);
                host.SetHeight(true, 100, 600);
                state.Substate++;
                host.CycleAnimation(state.IdleAnimation);
                break;
            case 1:
                state.State = state.PathPointIndex != 0 || route.Advance() ? 2 : 1;
                state.Substate = 0;
                break;
            default:
                throw new InvalidOperationException($"Unknown thug initialization substate {state.Substate}.");
        }
    }
}
