using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.World;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

public static class PsxWorldCollisionBuilder
{
    public static Node3D Build(PsxScene scene, string? sourceAsset = null)
    {
        var root = new Node3D { Name = "WorldCollision" };
        try
        {
            IReadOnlyList<CollisionTriangle>[] geometry = scene.Models.Select(CollisionGeometry.Read).ToArray();
            for (int index = 0; index < scene.Instances.Count; index++)
            {
                PsxInstance instance = scene.Instances[index];
                if (!OriginalCollisionFilter.IsInstanceEnabled(instance.Flags)) continue;
                Transform3D transform = PsxCoordinates.InstanceTransform(instance);
                foreach (var group in geometry[instance.ModelIndex].GroupBy(triangle => ReadLayers(triangle.PackedMetadata)))
                {
                    CollisionTriangle[] triangles = group.ToArray();
                    if (group.Key == 0) continue;
                    CollisionObject3D body;
                    if (group.Key == CollisionLayers.TriggerZones)
                        body = new ImportedTriggerSurface { SourceAsset = sourceAsset, ModelChecksum = scene.Models[instance.ModelIndex].Checksum,
                            InstanceIndex = index, ModelIndex = instance.ModelIndex,
                            SourceTriangles = Array.AsReadOnly(triangles), Monitoring = false, Monitorable = true };
                    else
                        body = new ImportedCollisionBody { SourceAsset = sourceAsset, ModelChecksum = scene.Models[instance.ModelIndex].Checksum,
                            InstanceIndex = index, ModelIndex = instance.ModelIndex,
                            SourceTriangles = Array.AsReadOnly(triangles), SourceFlags = instance.Flags,
                            SourceRotation = PsxCoordinates.InstanceRotation(instance) };
                    body.Name = $"Instance{index}_Layers{group.Key}";
                    body.CollisionLayer = group.Key;
                    body.CollisionMask = 0;
                    root.AddChild(body);
                    var vertices = new Vector3[triangles.Length * 3];
                    for (int triangle = 0; triangle < triangles.Length; triangle++)
                    {
                        CollisionTriangle source = triangles[triangle];
                        // Godot's concave faces use clockwise winding. Bake the full original basis into the vertices.
                        vertices[triangle * 3] = transform * PsxCoordinates.WorldPosition(source.A);
                        vertices[triangle * 3 + 1] = transform * PsxCoordinates.WorldPosition(source.C);
                        vertices[triangle * 3 + 2] = transform * PsxCoordinates.WorldPosition(source.B);
                    }
                    var shape = new ConcavePolygonShape3D { BackfaceCollision = false };
                    shape.SetFaces(vertices);
                    body.AddChild(new CollisionShape3D { Shape = shape });
                }
            }
            return root;
        }
        catch
        {
            root.Free();
            throw;
        }
    }

    private static uint ReadLayers(uint packed)
    {
        if (OriginalCollisionFilter.IsTriggerZone(packed)) return CollisionLayers.TriggerZones;
        uint layers = 0;
        if (OriginalCollisionFilter.Accepts(packed, CollisionQuery.Player)) layers |= CollisionLayers.PlayerTerrain;
        if (OriginalCollisionFilter.Accepts(packed, CollisionQuery.Camera)) layers |= CollisionLayers.CameraTerrain;
        if (OriginalCollisionFilter.Accepts(packed, CollisionQuery.LineOfSight)) layers |= CollisionLayers.LineOfSightTerrain;
        if (OriginalCollisionFilter.Accepts(packed, CollisionQuery.Enemy)) layers |= CollisionLayers.EnemyTerrain;
        return layers;
    }
}
