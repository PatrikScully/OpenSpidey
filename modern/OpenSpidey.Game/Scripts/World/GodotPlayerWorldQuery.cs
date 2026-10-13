using Godot;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.World;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Adapts native terrain ray hits to original player coordinates and face metadata.</summary>
public sealed class GodotPlayerWorldQuery(Node3D host) : IPlayerWorldQuery, IPlayerSupportWorld
{
    public PlayerWorldHit? Trace(FixedVector3 start, FixedVector3 end)
    {
        if (start == end) return null;
        Vector3 from = PsxCoordinates.FixedWorldPosition(start);
        using var query = PhysicsRayQueryParameters3D.Create(from,
            PsxCoordinates.FixedWorldPosition(end), CollisionLayers.PlayerTerrain);
        query.HitBackFaces = false;
        query.CollideWithAreas = false;
        using Godot.Collections.Dictionary hit = host.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return null;
        if (hit["collider"].AsGodotObject() is not ImportedCollisionBody body)
            throw new InvalidOperationException("Player terrain hit has no original collision metadata.");
        int triangleIndex = hit["face_index"].AsInt32();
        if ((uint)triangleIndex >= body.SourceTriangles.Count)
            throw new InvalidDataException("Player terrain hit references an invalid imported triangle.");
        CollisionTriangle triangle = body.SourceTriangles[triangleIndex];
        Vector3 position = hit["position"].AsVector3();
        int distance = GameWord.TruncateToInt32(from.DistanceTo(position) * 100);
        var contact = new PlayerGroundContact(PsxCoordinates.OriginalWorldPosition(position),
            PsxCoordinates.RotateOriginalNormal(triangle.FixedNormal, body.SourceRotation), distance,
            triangle.PackedMetadata, (body.SourceFlags & 0x100) != 0);
        return new(contact, body.InstanceIndex, body.ModelIndex, triangle.FaceIndex, body.SourceAsset, body.ModelChecksum, body.SupportId);
    }

    public PlayerSupportBody? FindSupport(ulong id)
    {
        return GodotObject.InstanceFromId(id) is IPlayerSupportActor actor ? actor.SupportState : null;
    }

    public void NotifyTrodden(ulong id)
    {
        if (GodotObject.InstanceFromId(id) is IPlayerSupportActor actor) actor.NotifyTrodden();
    }
}
