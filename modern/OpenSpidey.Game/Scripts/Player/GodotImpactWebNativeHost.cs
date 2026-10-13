using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

/// <summary>CImpactWeb collision and effect leaves against the live original scene and actor lists.</summary>
public sealed class GodotImpactWebNativeHost : IImpactWebHost
{
    private readonly GodotPlayer player;
    private readonly GodotPlayerMeleeDirectory melee;
    private readonly GodotActorSessionDirectory actors;
    private readonly GodotPlayerMeleeSession session;
    private readonly GodotActorWorldQuery world;
    private readonly GodotPlayerKillQuips quips;
    private readonly GodotWebImpactRings rings;
    private readonly GodotWebSurfaceSplats splats;
    private readonly GodotWebFragments fragments;
    private readonly Func<bool> lowMemory;

    public GodotImpactWebNativeHost(GodotPlayer player, GodotPlayerMeleeDirectory melee,
        GodotActorSessionDirectory actors, GodotPlayerMeleeSession session, GodotActorWorldQuery world,
        GodotPlayerKillQuips quips, GodotWebImpactRings rings, GodotWebSurfaceSplats splats,
        GodotWebFragments fragments, Func<bool> lowMemory)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.melee = melee ?? throw new ArgumentNullException(nameof(melee));
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.quips = quips ?? throw new ArgumentNullException(nameof(quips));
        this.rings = rings ?? throw new ArgumentNullException(nameof(rings));
        this.splats = splats ?? throw new ArgumentNullException(nameof(splats));
        this.fragments = fragments ?? throw new ArgumentNullException(nameof(fragments));
        this.lowMemory = lowMemory ?? throw new ArgumentNullException(nameof(lowMemory));
    }

    public bool LowMemory => lowMemory();
    public int ScaleDamage(int damage) => (player.Melee ??
        throw new InvalidOperationException("Impact-web damage needs the actual player's source difficulty/suit host.")).Controller.GetDamage(damage);

    public ImpactWebEnvironmentContact? TraceInitialEnvironment(FixedVector3 start, FixedVector3 end)
    {
        world.LineOfSightCheck = true;
        GodotActorWorldHit? hit;
        try { hit = world.Trace(start, end, includeEnvironmentalActors: true); }
        finally { world.LineOfSightCheck = false; }
        if (hit is null) return null;
        if (hit.Collider is not ImportedCollisionBody body)
            throw new InvalidDataException("Impact-web constructor hit has no original CItem flags.");
        return new(session.ResolveWorldContact(hit), body.SourceFlags, hit.Triangle.PackedMetadata,
            hit.Position, Normal(hit), FixedAim.Distance(start, hit.Position));
    }

    public object? CheckObjectCollision(FixedVector3 start, FixedVector3 end)
    {
        ActorLaserBody[] bodies = actors.SourceBaddyBodies.Select(body =>
            new ActorLaserBody(body.Identity, body.Position, body.MinorRadius, body.BodyFlags)).ToArray();
        ActorSphereContact? sphere = ActorLaserCollision.CheckBodies(start, end, bodies,
            radiusScale: 4096, ignoreRadius: session.IgnoreSphereRadius);
        if (sphere is not null) return sphere.Body.Identity;
        world.LineOfSightCheck = true;
        try
        {
            GodotActorWorldHit? hit = world.TraceEnvironmental(start, end);
            return hit is null ? null : session.ResolveWorldContact(hit);
        }
        finally { world.LineOfSightCheck = false; }
    }

    public void SetPlayerFirstContact()
    {
        PlayerCombatState combat = player.Combat.State;
        combat.Field534 = 360;
        combat.Field52C = unchecked((combat.Field528 + 11) << 10);
        quips.SetFirstContactDetails();
    }
    public void HitBody(object identity, ActorHit hit) => actors.ApplyHit(identity, hit);
    public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) =>
        player.Combat.PlayPositionSound(sound, position, pitch);
    public void CreateSplat(FixedVector3 position, ActorAngles normal) => splats.Emit(position, normal);
    public void FlickEligibleSwitch(object environmentalIdentity)
    {
        foreach (GodotSwitchActor control in melee.SourceControls)
        {
            if (!ReferenceEquals(control.State.OffItem?.Identity, environmentalIdentity) &&
                !ReferenceEquals(control.State.OnItem?.Identity, environmentalIdentity)) continue;
            if (control.State.Mode != 0) control.Flick();
            return;
        }
    }
    public void CreateImpactRing(ImpactWebRingRequest request) => rings.Emit(request);
    public int TraceGroundY(FixedVector3 start, FixedVector3 end) =>
        world.Trace(start, end, includeEnvironmentalActors: true)?.Position.Y ?? end.Y;
    public void CreateWebFragment(ImpactWebFragmentRequest request, Func<int, int> random) => fragments.Emit(request, random);

    internal static ActorAngles Normal(GodotActorWorldHit hit)
    {
        if (hit.Collider is not ImportedCollisionBody body)
            throw new InvalidDataException("Impact-web normal requires the original item's rotation.");
        FixedVector3 normal = PsxCoordinates.RotateOriginalNormal(hit.Triangle.FixedNormal, body.SourceRotation);
        return new(unchecked((short)normal.X), unchecked((short)normal.Y), unchecked((short)normal.Z));
    }
}
