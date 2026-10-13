using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.World;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

public sealed record GodotPlayerTriggerContact(object Identity, string SourceAsset, int InstanceIndex, int ModelIndex,
    uint CommandPointChecksum, int FaceIndex, FixedVector3 Position);

/// <summary>Source player recording sweep over real native zone triangles, environmental-head order and the original map grid.</summary>
public sealed class GodotPlayerTriggerZones : IPlayerTriggerZoneFrameHost, IPlayerTriggerZoneSweepHost
{
    private readonly GodotPlayer player;
    private readonly ImportedLevelWorld world;
    private readonly string environmentAsset;
    private readonly PsxZoneGrid grid;
    private readonly Func<IReadOnlyList<GodotScriptActor>> environmentalActors;
    private readonly Action<uint> triggerCommandPoint;
    public PlayerTriggerZoneSweep Sweep { get; }
    public IReadOnlyList<GodotPlayerTriggerContact> LastContacts { get; private set; } = [];

    public GodotPlayerTriggerZones(GodotPlayer player, ImportedLevelWorld world, string environmentAsset, PsxZoneGrid grid,
        PlayerTriggerZoneSweep sweep, Func<IReadOnlyList<GodotScriptActor>> environmentalActors, Action<uint> triggerCommandPoint)
    {
        ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(sweep); ArgumentNullException.ThrowIfNull(environmentalActors); ArgumentNullException.ThrowIfNull(triggerCommandPoint);
        this.player = player; this.world = world; this.grid = grid;
        Sweep = sweep;
        this.environmentAsset = PsxLibraryRepository.AssetPath(environmentAsset);
        this.environmentalActors = environmentalActors; this.triggerCommandPoint = triggerCommandPoint;
        RequireEnvironment();
    }

    public void FinishTriggerZoneSweep(FixedVector3 previousPosition)
    {
        LastContacts = [];
        Sweep.Sweep(player.Controller.Position, previousPosition, (uint)player.Controller.Action, this);
    }

    public void RecordTriggerZoneHits(PlayerTriggerZoneRay ray)
    {
        var contacts = new List<GodotPlayerTriggerContact>();
        LastContacts = contacts.AsReadOnly();
        if (ray.Start == ray.End) return;
        Vector3 start = PsxCoordinates.FixedWorldPosition(ray.Start), end = PsxCoordinates.FixedWorldPosition(ray.End);
        // Source mode1 visits the genuine environmental body list before grid items. Each body is one inquiry identity.
        var inquiry = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (GodotScriptActor actor in environmentalActors())
        {
            if (!GodotObject.IsInstanceValid(actor) || !actor.IsInsideTree() || actor.Lifecycle.Phase != ActorLifePhase.Active ||
                !OriginalCollisionFilter.IsInstanceEnabled(actor.Variables.ItemFlags) || !inquiry.Add(actor)) continue;
            GodotPlayerTriggerContact? contact = Crossings(actor, actor).FirstOrDefault();
            if (contact is not null) Dispatch(contact);
        }
        ImportedLevelSource source = RequireEnvironment();
        var surfaces = Descendants(world.Collision).OfType<ImportedTriggerSurface>()
            .Where(area => area.SourceAsset?.Equals(environmentAsset, StringComparison.OrdinalIgnoreCase) == true &&
                area.IsInsideTree() && area.CollisionLayer != 0).GroupBy(area => area.InstanceIndex)
            .ToDictionary(group => group.Key, group => group.ToArray());
        foreach (int index in OriginalZoneTraversal.Instances(grid, ray.Start, ray.End))
        {
            if ((uint)index >= source.Scene.Instances.Count) throw new InvalidDataException("Player trigger grid references a missing environment instance.");
            if (!OriginalCollisionFilter.IsInstanceEnabled(source.Scene.Instances[index].Flags) || !surfaces.TryGetValue(index, out var areas)) continue;
            GodotPlayerTriggerContact? contact = areas.SelectMany(area => Crossings(area, area)).FirstOrDefault();
            if (contact is not null) Dispatch(contact);
        }

        void Dispatch(GodotPlayerTriggerContact contact)
        {
            contacts.Add(contact);
            // This is the same checksum table as Spool_SkipPackets, indexed by the item's model, not the instance/node number.
            triggerCommandPoint(contact.CommandPointChecksum);
        }

        IEnumerable<GodotPlayerTriggerContact> Crossings(Node owner, object identity)
        {
            IEnumerable<ImportedTriggerSurface> areas = owner is ImportedTriggerSurface self ? [self] : Descendants(owner).OfType<ImportedTriggerSurface>();
            foreach (ImportedTriggerSurface area in areas)
            {
                if (!GodotObject.IsInstanceValid(area) || !area.IsInsideTree() || area.CollisionLayer == 0) continue;
                if (area.SourceAsset?.Equals(environmentAsset, StringComparison.OrdinalIgnoreCase) != true)
                    throw new InvalidDataException("The original found a trigger zone outside the active environment region.");
                uint checksum = area.ModelChecksum ?? throw new InvalidDataException("Native trigger model has no original command-point checksum.");
                var shapeNode = area.GetChildren().OfType<CollisionShape3D>().Single();
                if (shapeNode.Disabled) continue;
                if (shapeNode.Shape is not ConcavePolygonShape3D shape) throw new InvalidDataException("Original trigger zone has no imported native triangle shape.");
                Vector3[] faces = shape.GetFaces();
                if (faces.Length != area.SourceTriangles.Count * 3) throw new InvalidDataException("Trigger triangles do not match native original-face metadata.");
                for (int face = 0; face < area.SourceTriangles.Count; face++)
                {
                    CollisionTriangle triangle = area.SourceTriangles[face];
                    if (!OriginalTriggerZoneContacts.Records(triangle.PackedMetadata, true)) continue;
                    Vector3 a = shapeNode.ToGlobal(faces[face * 3]), b = shapeNode.ToGlobal(faces[face * 3 + 1]), c = shapeNode.ToGlobal(faces[face * 3 + 2]);
                    // Godot imports clockwise native faces. Restore their outward normal for the source's one-sided plane gate.
                    Vector3 normal = (c - a).Cross(b - a);
                    if (normal.Dot(end - start) >= 0) continue;
                    Variant hit = Geometry3D.SegmentIntersectsTriangle(start, end, a, b, c);
                    if (hit.VariantType == Variant.Type.Nil) continue;
                    yield return new(identity, area.SourceAsset!, area.InstanceIndex, area.ModelIndex, checksum, triangle.FaceIndex,
                        PsxCoordinates.OriginalWorldPosition(hit.AsVector3()));
                }
            }
        }
    }

    private ImportedLevelSource RequireEnvironment() => world.Sources.SingleOrDefault(source =>
        source.AssetPath.Equals(environmentAsset, StringComparison.OrdinalIgnoreCase)) ??
        throw new InvalidOperationException("Player trigger sweep requires the active original environment source.");

    private static IEnumerable<Node> Descendants(Node owner)
    {
        foreach (Node child in owner.GetChildren())
        { yield return child; foreach (Node nested in Descendants(child)) yield return nested; }
    }
}
