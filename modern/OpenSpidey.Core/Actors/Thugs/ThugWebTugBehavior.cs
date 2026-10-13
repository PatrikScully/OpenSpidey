using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public interface IThugWebTugHost
{
    int PathCheckWithHit(FixedVector3 from, FixedVector3 to, int clearance, out FixedVector3 hit);
    bool AddPointToPath(FixedVector3 position, int argument);
    void Neutralize();
    void LookAtPoint(FixedVector3 position, uint completionMask, int rate, int tolerance);
    void ReplaceThrowPoints(IReadOnlyList<FixedVector3> points);
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitchOffset);
}

/// <summary>Original CThug virtual TugImpulse, launch setup and shared thug/cop WallHitCheck.</summary>
public static class ThugWebTugBehavior
{
    public static bool TugImpulse(ThugState state, ActorProcedureState actor, FixedVector3 direction,
        IReadOnlyList<FixedVector3>? path, IThugWebTugHost host)
    {
        actor.BehaviorFlags &= ~8u;
        if (path is not null)
        {
            state.State = 16; state.Substate = 0;
            host.ReplaceThrowPoints(path);
            return true;
        }
        int launched = GetLaunched(state, actor, direction, false, 80, host);
        if (launched == 0) return false;
        if (state.Health <= 50)
            actor.BehaviorFlags = launched == 1 ? actor.BehaviorFlags | 0x10u : actor.BehaviorFlags & ~0x10u;
        state.ActionFlags |= 0x80000;
        host.PlayPositionSound(0x800F, actor.Body.Position, 0);
        state.State = 14; state.Substate = 0;
        return true;
    }

    public static int GetLaunched(ThugState state, ActorProcedureState actor, FixedVector3 direction,
        bool keepFacing, int duration, IThugWebTugHost host)
    {
        FixedVector3 position = actor.Body.Position;
        FixedVector3 end = new(unchecked(position.X + (duration + 1) * direction.X), position.Y,
            unchecked(position.Z + (duration + 1) * direction.Z));
        int wall = WallHitCheck(state, actor, end, direction, duration, host);
        if (wall == 3) return 0;
        host.Neutralize();
        actor.Body.Velocity = direction with { Y = 0 };
        if (!keepFacing || wall == 2) host.LookAtPoint(end, 0, 80, 200);
        return wall;
    }

    public static int WallHitCheck(ThugState state, ActorProcedureState actor, FixedVector3 end,
        FixedVector3 direction, int duration, IThugWebTugHost host)
    {
        FixedVector3 position = actor.Body.Position;
        int adjustedY = unchecked(position.Y + ((state.SurfaceClearance - 20) << 12));
        state.ActionFlags &= ~0x500u;
        int code = host.PathCheckWithHit(position with { Y = adjustedY }, end with { Y = adjustedY }, 55, out FixedVector3 hit);
        if (code == 4) { state.ActionCounter = 0; return 3; }
        int result = 1;
        FixedVector3 destination = end;
        if (code == 2)
        {
            int x = SourceAbs(unchecked(hit.X - position.X)), z = SourceAbs(unchecked(hit.Z - position.Z));
            int divisor = SourceAbs(x > z ? direction.X : direction.Z);
            // Original faults when a blocked launch has no horizontal velocity. Reject that degenerate launch.
            if (divisor == 0) { state.ActionCounter = 0; return 3; }
            state.ActionCounter = (x > z ? x : z) / divisor;
            if (state.ActionCounter > duration) state.ActionCounter = duration;
            else result = 2;
            destination = new(unchecked(position.X + direction.X * state.ActionCounter), position.Y,
                unchecked(position.Z + direction.Z * state.ActionCounter));
            state.ActionFlags |= 0x400;
        }
        else
        {
            state.ActionFlags |= 0x100;
            state.ActionCounter = unchecked(duration - 1);
        }
        if (state.Health > 0)
        {
            if (host.AddPointToPath(position, 0) && host.AddPointToPath(destination, 0)) actor.BehaviorFlags &= ~0x10000000u;
            else state.Health = 0;
        }
        return result;
    }

    private static int SourceAbs(int value) => value < 0 ? unchecked(-value) : value;
}
