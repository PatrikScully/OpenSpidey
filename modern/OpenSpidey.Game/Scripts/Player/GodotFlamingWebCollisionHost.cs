using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

/// <summary>Genuine shared source collision leaves; flaming damage/effects remain owned by their separate projectile.</summary>
public sealed class GodotFlamingWebCollisionHost : IGodotFlamingWebCollisionHost
{
    private readonly GodotImpactWebNativeHost impact;
    private readonly GodotActorWorldQuery world;

    public GodotFlamingWebCollisionHost(GodotImpactWebNativeHost impact, GodotActorWorldQuery world)
    {
        this.impact = impact ?? throw new ArgumentNullException(nameof(impact));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }
    public ImpactWebEnvironmentContact? TraceInitialEnvironment(FixedVector3 start, FixedVector3 end) => impact.TraceInitialEnvironment(start, end);
    public object? CheckObjectCollision(FixedVector3 start, FixedVector3 end) => impact.CheckObjectCollision(start, end);
    public void HitBody(object identity, ActorHit hit) => impact.HitBody(identity, hit);
    public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => impact.PlayPositionSound(sound, position, pitch);
    public void FlickEligibleSwitch(object identity) => impact.FlickEligibleSwitch(identity);
    public int GetGroundY(FixedVector3 position)
    {
        FixedVector3 end = position + new FixedVector3(0, 0x1388000, 0);
        return world.Trace(position, end, includeEnvironmentalActors: true)?.Position.Y ?? end.Y;
    }
}
