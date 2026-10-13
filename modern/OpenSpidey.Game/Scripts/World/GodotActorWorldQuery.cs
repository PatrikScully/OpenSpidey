using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.World;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

public sealed record GodotActorWorldHit(CollisionObject3D Collider, CollisionTriangle Triangle,
    FixedVector3 Position, ulong? SupportId, string? SourceAsset, int InstanceIndex, int ModelIndex, uint? ModelChecksum);

/// <summary>Original actor ground and line queries against native imported faces and live mechanism owners.</summary>
public sealed class GodotActorWorldQuery(Node3D host, TriggerFile triggers) : IActorPathHost, IActorHeightHost
{
    private readonly record struct RayPolicy(CollisionQuery Filter, uint Layer, bool QueryAreas);
    private RayPolicy lastZonedPolicy = new(CollisionQuery.Player, CollisionLayers.PlayerTerrain, false);
    public int CollisionCheckMode { get; set; }
    public bool LineOfSightCheck { get; set; }
    public bool CameraCollisionCheck { get; set; }
    public bool TriggerCollisionCheck { get; set; }
    public GodotActorWorldHit? LastHit { get; private set; }
    public int RayQueries { get; private set; }
    public int GroundQueries { get; private set; }

    public bool LineOfSight(FixedVector3 from, FixedVector3 to, int flags, ref FixedVector3 hit)
    {
        GodotActorWorldHit? contact = Trace(from, to, includeEnvironmentalActors: flags == 0);
        if (contact is null) return true;
        hit = contact.Position;
        return false;
    }

    public int GroundHeight(FixedVector3 position, int above, int below) => GetGroundHeight(position, above, below, out _);

    public int GetGroundHeight(FixedVector3 position, int above, int below, out ActorBodyMotion? support)
    {
        GroundQueries++;
        FixedVector3 from = position with { Y = unchecked(position.Y - (above << 12)) };
        FixedVector3 to = position with { Y = unchecked(position.Y + (below << 12)) };
        GodotActorWorldHit? contact = Trace(from, to);
        support = null;
        if (contact is null) return -1;
        if (contact.Collider is ImportedCollisionBody body && (body.SourceFlags & 0x10) != 0 && contact.SupportId is { } id)
            support = SnapshotSupport(id);
        return contact.Position.Y;
    }

    public FixedVector3 WaypointPosition(int node)
    {
        if ((uint)node >= triggers.Nodes.Count) throw new InvalidDataException($"Actor ground host references missing waypoint {node}.");
        TriggerPosition position = TriggerPlacement.Read(triggers.Nodes[node]).Position;
        return new FixedVector3(position.X, position.Y, position.Z) << 12;
    }

    public GodotActorWorldHit? Trace(FixedVector3 from, FixedVector3 to, bool includeEnvironmentalActors = true)
    {
        lastZonedPolicy = CurrentPolicy();
        return TraceWithPolicy(from, to, lastZonedPolicy, includeEnvironmentalActors, environmentalActorsOnly: false);
    }

    /// <summary>Direct source EnviroList traversal keeps the masks selected by the previous zoned ray.</summary>
    public GodotActorWorldHit? TraceEnvironmental(FixedVector3 from, FixedVector3 to) =>
        TraceWithPolicy(from, to, lastZonedPolicy, includeEnvironmentalActors: true, environmentalActorsOnly: true);

    private RayPolicy CurrentPolicy()
    {
        CollisionQuery filter = CollisionCheckMode != 0 ? CollisionQuery.Enemy : CollisionQuery.Player;
        if (LineOfSightCheck) filter |= CollisionQuery.LineOfSight;
        if (CameraCollisionCheck) filter |= CollisionQuery.Camera;
        if (TriggerCollisionCheck) filter |= CollisionQuery.Trigger;
        uint layer = CollisionCheckMode != 0 ? CollisionLayers.EnemyTerrain : CollisionLayers.PlayerTerrain;
        if (TriggerCollisionCheck) layer = CollisionLayers.TriggerZones;
        return new(filter, layer, TriggerCollisionCheck);
    }

    private GodotActorWorldHit? TraceWithPolicy(FixedVector3 from, FixedVector3 to, RayPolicy policy,
        bool includeEnvironmentalActors, bool environmentalActorsOnly)
    {
        RayQueries++;
        LastHit = null;
        if (from == to) return null;
        var excluded = new Godot.Collections.Array<Rid>();
        using var query = PhysicsRayQueryParameters3D.Create(PsxCoordinates.FixedWorldPosition(from),
            PsxCoordinates.FixedWorldPosition(to), policy.Layer);
        query.HitBackFaces = false;
        query.CollideWithAreas = policy.QueryAreas;
        while (true)
        {
            query.Exclude = excluded;
            using var result = host.GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (result.Count == 0) return null;
            if (result["collider"].AsGodotObject() is not CollisionObject3D collider)
                throw new InvalidDataException("An actor ray hit has no native collision object.");
            int face = result["face_index"].AsInt32();
            IReadOnlyList<CollisionTriangle> triangles;
            ulong? supportId;
            string? sourceAsset;
            int instance, model;
            uint? checksum;
            bool rejected = false;
            if (collider is ImportedCollisionBody body)
            {
                triangles = body.SourceTriangles; supportId = body.SupportId;
                sourceAsset = body.SourceAsset; instance = body.InstanceIndex; model = body.ModelIndex; checksum = body.ModelChecksum;
                rejected = !OriginalCollisionFilter.IsInstanceEnabled(body.SourceFlags) ||
                    (!includeEnvironmentalActors && supportId is not null) ||
                    (environmentalActorsOnly && supportId is null) || !IsLiveOwner(supportId);
            }
            else if (collider is ImportedTriggerSurface area)
            {
                triangles = area.SourceTriangles; supportId = null;
                sourceAsset = area.SourceAsset; instance = area.InstanceIndex; model = area.ModelIndex; checksum = area.ModelChecksum;
                rejected = environmentalActorsOnly;
            }
            else throw new InvalidDataException("Actor terrain contact has no original imported face metadata.");
            if ((uint)face >= triangles.Count) throw new InvalidDataException("Actor terrain contact references an invalid original face.");
            rejected |= !OriginalCollisionFilter.Accepts(triangles[face].PackedMetadata, policy.Filter);
            // Utils ground/LOS queries initialize RecordTriggerZoneHits to zero. Zone faces do not become blockers.
            rejected |= OriginalCollisionFilter.IsTriggerZone(triangles[face].PackedMetadata);
            if (rejected)
            {
                // Import groups share the source query-layer mask. Rejecting one category rejects the whole group,
                // and excluding it keeps the original ray origin so a farther accepted face remains discoverable.
                excluded.Add(collider.GetRid());
                continue;
            }
            FixedVector3 position = PsxCoordinates.OriginalWorldPosition(result["position"].AsVector3());
            LastHit = new(collider, triangles[face], position, supportId, sourceAsset, instance, model, checksum);
            return LastHit;
        }
    }

    private bool IsLiveOwner(ulong? id)
    {
        if (id is null) return true;
        GodotObject? owner = GodotObject.InstanceFromId(id.Value);
        if (owner is null || !GodotObject.IsInstanceValid(owner))
        {
            return false;
        }
        if (owner is not GodotScriptActor actor)
            throw new InvalidDataException("An imported actor collision owner has no source actor lifetime.");
        ActorLifePhase phase = actor.Lifecycle.Phase;
        // CBody::Suspend removes the mechanism from the environmental-object list used by source line queries.
        return phase == ActorLifePhase.Active;
    }

    private ActorBodyMotion SnapshotSupport(ulong id)
    {
        if (GodotObject.InstanceFromId(id) is not IGodotEnvironmentalActor actor)
            throw new NotSupportedException("This native support owner has no recovered body-motion adapter.");
        return actor.SupportMotion;
    }
}
