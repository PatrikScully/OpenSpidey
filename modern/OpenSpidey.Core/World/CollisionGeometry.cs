using System.Numerics;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.World;

public static class CollisionGeometry
{
    public static IReadOnlyList<CollisionTriangle> Read(PsxModel model)
    {
        var triangles = new List<CollisionTriangle>();
        for (int faceIndex = 0; faceIndex < model.Faces.Count; faceIndex++)
        {
            PsxFace face = model.Faces[faceIndex];
            if (face.CollisionWord is not { } packed) continue;
            if (!OriginalCollisionFilter.Accepts(packed, CollisionQuery.Enemy)) continue;
            // The file stores a normal index. M3dInit_ParsePSX multiplies it by eight
            // before the original runtime's byte-offset lookup; managed imports remain immutable.
            int normalIndex = (int)(packed & 65535);
            if (normalIndex >= model.Normals.Count)
                throw new InvalidDataException($"Collision face {faceIndex} references missing normal {normalIndex}.");
            Vector3 normal = model.Normals[normalIndex].Direction;
            if (normal.LengthSquared() == 0) continue;
            var fixedNormal = new FixedVector3((int)normal.X, (int)normal.Y, (int)normal.Z);
            normal = Vector3.Normalize(normal);
            AddTriangle(0, 1, 2);
            if (!face.IsTriangle) AddTriangle(1, 3, 2);

            void AddTriangle(int first, int second, int third)
            {
                Vector3 a = ReadVertex(first), b = ReadVertex(second), c = ReadVertex(third);
                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.LengthSquared() == 0) return;
                if (Vector3.Dot(cross, normal) < 0) (b, c) = (c, b);
                triangles.Add(new(a, b, c, normal, faceIndex, packed) { FixedNormal = fixedNormal });
            }

            Vector3 ReadVertex(int corner)
            {
                // The original coarse collision pipeline reads raw coordinates regardless of render vertex flags.
                return model.Vertices[face.VertexIndices[corner]].Position;
            }
        }
        return triangles.AsReadOnly();
    }
}
