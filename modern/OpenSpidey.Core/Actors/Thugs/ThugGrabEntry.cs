using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

/// <summary>CThug::Grab4D38E0: existing path construction must accept the destination before changing ownership/state.</summary>
public static class ThugGrabEntry
{
    public static bool TryGrab(ThugState state, ActorProcedureState actor, ThugStateFlags flags,
        FixedVector3 destination, Func<FixedVector3, int, bool> addPointToPath)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(flags); ArgumentNullException.ThrowIfNull(addPointToPath);
        if ((flags.Check(state.State) & 2) != 0 || !addPointToPath(destination, 0)) return false;
        state.State = 20; state.Substate = 0; actor.BehaviorFlags |= 0x40;
        return true;
    }
}
