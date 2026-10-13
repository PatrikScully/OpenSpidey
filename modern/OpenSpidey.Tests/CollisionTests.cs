using System.Numerics;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.World;
using OpenSpidey.Core.Mathematics;

internal static class CollisionTests
{
    public static void Run(Action<bool, string> assert)
    {
        assert(OriginalCollisionFilter.Accepts(0, CollisionQuery.Player), "Ordinary faces accept player probes.");
        assert(!OriginalCollisionFilter.Accepts(0x10000, CollisionQuery.Player), "Original category gate rejects category one.");
        assert(OriginalCollisionFilter.Accepts(0x20000, CollisionQuery.Player) && OriginalCollisionFilter.IsTriggerZone(0x20000),
            "Trigger zones participate in probes but are marked separately from solid surfaces.");
        assert(!OriginalCollisionFilter.Accepts(0x200000, CollisionQuery.Player) &&
            OriginalCollisionFilter.Accepts(0x200000, CollisionQuery.Enemy), "Enemy-only surfaces do not block player probes.");
        assert(OriginalCollisionFilter.Accepts(0x400000, CollisionQuery.Player) &&
            !OriginalCollisionFilter.Accepts(0x400000, CollisionQuery.LineOfSight), "Line-of-sight exclusion is independent of player movement.");
        assert(!OriginalCollisionFilter.Accepts(0, CollisionQuery.Camera) &&
            OriginalCollisionFilter.Accepts(0x100000, CollisionQuery.Camera), "Camera probes require the original camera bit.");
        assert(!OriginalCollisionFilter.Accepts(0, CollisionQuery.Trigger) &&
            OriginalCollisionFilter.Accepts(0x20000, CollisionQuery.Trigger), "Trigger-specific probes require a trigger surface.");
        assert(OriginalCollisionFilter.IsInstanceEnabled(0x1000) && !OriginalCollisionFilter.IsInstanceEnabled(0x20)
            && !OriginalCollisionFilter.IsInstanceEnabled(1), "Collision instance flags are independent of render visibility.");
        var vertices = new PsxVertex[] { new(new(-100, 0, -100), 0), new(new(100, 0, -100), 0),
            new(new(-100, 0, 100), 0), new(new(100, 0, 100), 0) };
        var model = new PsxModel(0, ushort.MaxValue, vertices,
            new[] { new PsxFace(0, [0, 1, 2, 3], 0, null, [], 0x100000) })
        { Normals = new[] { new PsxNormal(new(0, -4096, 0), 0) } };
        IReadOnlyList<CollisionTriangle> triangles = CollisionGeometry.Read(model);
        assert(triangles.Count == 2, "Quads produce two collision triangles.");
        assert(triangles.All(triangle => triangle.FixedNormal == new FixedVector3(0, -4096, 0)),
            "Collision records retain unnormalized source Q12 normals for player response.");
        assert(triangles.All(triangle => Vector3.Dot(Vector3.Cross(triangle.B - triangle.A, triangle.C - triangle.A), triangle.Normal) > 0),
            "Collision winding agrees with the original face normal.");
        assert(triangles.All(triangle => triangle.FaceIndex == 0 && triangle.PackedMetadata == 0x100000),
            "Triangles preserve the source face and query metadata.");
        var indexed = model with
        {
            Normals = new[] { new PsxNormal(new(4096, 0, 0), 0), new PsxNormal(new(0, -4096, 0), 0) },
            Faces = new[] { model.Faces[0] with { CollisionWord = 0x100001 } }
        };
        assert(CollisionGeometry.Read(indexed).All(triangle => triangle.FixedNormal == new FixedVector3(0, -4096, 0)),
            "The on-disk normal index is read before the original loader's multiply-by-eight relocation.");
        vertices[0] = vertices[0] with { Flags = 0x10 };
        assert(CollisionGeometry.Read(model).Count == 2, "Render vertex flags do not change original coarse collision coordinates.");
        assert(CollisionGeometry.Read(model with { Faces = new[] { model.Faces[0] with { CollisionWord = null } } }).Count == 0,
            "Packets without collision data are not made solid.");
        bool rejected = false;
        try { CollisionGeometry.Read(model with { Faces = new[] { model.Faces[0] with { CollisionWord = 0x100008 } } }); }
        catch (InvalidDataException) { rejected = true; }
        assert(rejected, "Missing collision normal references are diagnosed.");
    }
}
