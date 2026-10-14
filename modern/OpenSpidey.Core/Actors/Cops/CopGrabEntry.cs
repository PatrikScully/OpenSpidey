using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Cops;

/// <summary>CCop::Grab (0x4296B0): CheckStateFlags(17)&2, AddPointToPath, then state20 and behavior 0x40.</summary>
public static class CopGrabEntry
{
    public static bool TryGrab(CopState state, ActorProcedureState actor, ThugStateFlags flags,
        FixedVector3 destination, Func<FixedVector3, int, bool> addPointToPath)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(flags); ArgumentNullException.ThrowIfNull(addPointToPath);
        if ((flags.Check(state.State) & 2) != 0 || !addPointToPath(destination, 0)) return false;
        state.State = 20;
        state.Substate = 0;
        actor.BehaviorFlags |= 0x40;
        return true;
    }
}
