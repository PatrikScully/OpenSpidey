using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.World;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Actual L1A1 packet10, trigger shapes, model checksums and source command-point contact/rearming over the real player owner.</summary>
internal static class PlayerTriggerZoneImportChecks
{
    public static void Verify(GodotPlayer player, ImportedLevelWorld world, GodotActorRegistry registry, PkrArchive archive)
    {
        const string asset = "data/l1a1_g.psx";
        ImportedLevelSource source = world.Sources.Single(source => source.AssetPath.Equals(asset, StringComparison.OrdinalIgnoreCase));
        PsxZoneGrid grid = PsxZoneGridReader.Read(archive.Read(asset), source.Scene.Instances.Count) ??
            throw new InvalidDataException("Actual L1A1 has no original type10 zone grid.");
        TriggerFile triggers = TriggerFile.Read(archive.Read("data/l1a1_t.trg"));
        // This private real TRG owner exercises queueing only. Production handlers execute later in the mission phase.
        var mission = new MissionRuntime(triggers, registry, []);
        var queued = new List<ushort>();
        mission.Trace += trace => { if (trace.Kind == MissionTraceKind.Queued) queued.Add(trace.NodeId); };
        var collector = new GodotPlayerTriggerZones(player, world, asset, grid, new PlayerTriggerZoneSweep(),
            () => registry.SourceList(ActorUpdateGroup.Environment).SourceOrder.OfType<GodotScriptActor>().Where(registry.Owns).ToArray(),
            checksum => mission.TriggerCommandPoint(checksum));
        FixedVector3 savedPosition = player.Controller.Position;
        uint savedAction = player.Combat.State.ActionState;
        try
        {
            int verified = 0;
            foreach (ImportedTriggerSurface area in Descendants(world.Collision).OfType<ImportedTriggerSurface>()
                .Where(area => area.SourceAsset?.Equals(asset, StringComparison.OrdinalIgnoreCase) == true))
            {
                uint checksum = area.ModelChecksum ?? throw new InvalidDataException("Actual trigger area has no model checksum.");
                Assert(source.Scene.Models[area.ModelIndex].Checksum == checksum && mission.CommandPoints.Values.Any(point => point.Checksum == checksum),
                    "Every genuine trigger model must retain its actual Spool_SkipPackets checksum and matching TRG command point.");
                var shapeNode = area.GetChildren().OfType<CollisionShape3D>().Single();
                var shape = shapeNode.Shape as ConcavePolygonShape3D ?? throw new InvalidDataException("Original trigger shape is absent.");
                Vector3[] faces = shape.GetFaces();
                int index = Enumerable.Range(0, area.SourceTriangles.Count).First(index =>
                    OriginalTriggerZoneContacts.Records(area.SourceTriangles[index].PackedMetadata, true));
                Vector3 a = shapeNode.ToGlobal(faces[index * 3]), b = shapeNode.ToGlobal(faces[index * 3 + 1]), c = shapeNode.ToGlobal(faces[index * 3 + 2]);
                Vector3 centre = (a + b + c) / 3, normal = (c - a).Cross(b - a).Normalized();
                FixedVector3 previous = PsxCoordinates.OriginalWorldPosition(centre + normal * 0.1f);
                FixedVector3 position = PsxCoordinates.OriginalWorldPosition(centre - normal * 0.1f);
                using var ray = PhysicsRayQueryParameters3D.Create(PsxCoordinates.FixedWorldPosition(previous),
                    PsxCoordinates.FixedWorldPosition(position), CollisionLayers.TriggerZones);
                ray.CollideWithAreas = true; ray.CollideWithBodies = false; ray.HitBackFaces = false;
                using var hit = player.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                Assert(hit.Count > 0 && hit["collider"].AsGodotObject() is ImportedTriggerSurface,
                    "The actual original face must also be a live one-sided native trigger-area ray contact.");
                player.Combat.State.Position = position; player.Combat.State.ActionState = 1; player.Controller.ApplyCombatState();
                mission.BeginContactFrame(); int before = queued.Count;
                collector.FinishTriggerZoneSweep(previous); mission.EndContactFrame();
                Assert(collector.LastContacts.Any(contact => contact.InstanceIndex == area.InstanceIndex && contact.CommandPointChecksum == checksum) &&
                    mission.CommandPoints.Values.Any(point => point.Checksum == checksum && point.ContactThisFrame && point.ExecutedForContact) && queued.Count > before,
                    "The real player sweep must cross its authentic grid item and queue the actual source command-point owner.");
                int pending = mission.PendingCount;
                mission.BeginContactFrame(); collector.FinishTriggerZoneSweep(previous); mission.EndContactFrame();
                Assert(mission.PendingCount == pending,
                    "Repeated player contact must preserve executed state and avoid replaying an already queued source command point.");
                // Reverse one-sided approach. A real exit frame marks no matching contact and rearms that original point.
                mission.BeginContactFrame(); collector.RecordTriggerZoneHits(new(position, previous)); mission.EndContactFrame();
                Assert(mission.CommandPoints.Values.Where(point => point.Checksum == checksum).All(point => !point.ExecutedForContact),
                    "Leaving the real one-sided original face must rearm uncontacted command points after the player phase.");
                player.Combat.State.ActionState = 0x80; player.Controller.ApplyCombatState();
                FixedVector3 direction = collector.Sweep.LastMoveDirection;
                collector.FinishTriggerZoneSweep(position);
                Assert(collector.LastContacts.Count == 0 && collector.Sweep.LastMoveDirection == direction,
                    "The genuine source death action must skip both trigger recording and retained movement-direction updates.");
                verified++;
            }
            Assert(verified == 19, "Original L1A1 must expose all nineteen authored trigger models to the native player recording sweep.");
            GD.Print($"Verified native player trigger recording: {verified} actual L1A1 models/type10 cells, genuine one-sided faces, original TRG checksum queues, repeated contact/exit rearming and source80 death suppression.");
        }
        finally
        {
            player.Combat.State.Position = savedPosition; player.Combat.State.ActionState = savedAction;
            player.Controller.ApplyCombatState();
        }
    }

    private static IEnumerable<Node> Descendants(Node owner)
    {
        foreach (Node child in owner.GetChildren())
        { yield return child; foreach (Node nested in Descendants(child)) yield return nested; }
    }
    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native player triggers: {evidence}"); }
}
