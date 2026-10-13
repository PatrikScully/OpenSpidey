using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors;

public interface IActorHeightHost
{
    int GetGroundHeight(FixedVector3 position, int above, int below, out ActorBodyMotion? support);
}

/// <summary>Recovered CBaddy::SetHeight ground queries, platform support and quarter-step convergence.</summary>
public sealed class ActorHeightController
{
    private readonly ActorProcedureState actor;
    private readonly IActorHeightHost host;
    public int GroundHeight { get; set; }
    public ActorBodyMotion? Support { get; set; }
    public short Clearance { get; set; }
    public byte QueryInterval { get; set; }
    public byte QueryElapsed { get; set; }
    public uint RouteFlags { get; set; }

    public ActorHeightController(ActorProcedureState actor, IActorHeightHost host)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(host);
        this.actor = actor;
        this.host = host;
    }

    public int SetHeight(bool force, int above, int below, int elapsedTicks)
    {
        uint behavior = actor.BehaviorFlags;
        if ((behavior & 0x8000) != 0) return 1;
        bool query = Support is not null || force || (behavior & 0x40000) != 0 ||
            ((behavior & 0x8000000) != 0 && ((behavior & 0x20000000) != 0 || (RouteFlags & 4) != 0));
        byte elapsed = unchecked((byte)(elapsedTicks + QueryElapsed));
        if (query && (force || elapsed >= QueryInterval))
        {
            FixedVector3 position = actor.Body.Position;
            position = position with { Y = unchecked(position.Y + (Clearance << 12)) };
            int height = host.GetGroundHeight(position, above, below, out ActorBodyMotion? support);
            Support = support;
            if (height == -1) return 0;
            if (support is not null)
            {
                int verticalVelocity = support.Velocity.Y;
                if (verticalVelocity == 0) Support = null;
                else height = unchecked(height + verticalVelocity);
            }
            GroundHeight = height;
            if (QueryInterval == 0) QueryInterval = 30;
            QueryElapsed = 0;
        }

        int targetY = unchecked(GroundHeight - (Clearance << 12));
        int y = actor.Body.Position.Y;
        if (targetY != y)
        {
            y = unchecked(y + (unchecked(targetY - y) >> 2));
            actor.Body.Position = actor.Body.Position with { Y = y };
            int difference = unchecked(y - targetY);
            int magnitude = difference < 0 ? unchecked(-difference) : difference;
            if (magnitude <= (Support is null ? 12288 : 122880))
                actor.Body.Position = actor.Body.Position with { Y = targetY };
            else return 1;
        }
        return 2;
    }
}
