using OpenSpidey.Assets.Models;

namespace OpenSpidey.Assets.Animation;

/// <summary>Welded corners share the exact attachment vertex and its bone instead of blending independent pivots.</summary>
public static class StitchedVertexBindings
{
    public static IReadOnlyList<IReadOnlyList<BoundCharacterVertex>> Read(PsxScene scene, int partCount)
    {
        if (partCount <= 0 || partCount > scene.Models.Count)
            throw new ArgumentOutOfRangeException(nameof(partCount));
        var attachments = new List<BoundCharacterVertex>();
        var parts = new IReadOnlyList<BoundCharacterVertex>[partCount];
        for (int part = 0; part < partCount; part++)
        {
            PsxModel model = scene.Models[part];
            var vertices = new BoundCharacterVertex[model.Vertices.Count];
            for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
            {
                PsxVertex vertex = model.Vertices[vertexIndex];
                if ((vertex.Flags & 2) != 0)
                {
                    // M3dInit relocates the file's Y index into X * 8 before DCModel conversion.
                    int attachment = Math.Min(unchecked((short)((int)vertex.Position.Y * 8)) >> 3, 255);
                    if (attachment < 0 || attachment >= attachments.Count)
                        throw new InvalidDataException($"Part {part}, vertex {vertexIndex}: missing stitch attachment {attachment}.");
                    vertices[vertexIndex] = attachments[attachment];
                }
                else
                {
                    if ((vertex.Flags & 0x10) != 0)
                        throw new NotSupportedException($"Part {part}, vertex {vertexIndex}: billboard vertices need their own geometry adapter.");
                    vertices[vertexIndex] = new(part, vertex.Position);
                    if ((vertex.Flags & 1) != 0) attachments.Add(vertices[vertexIndex]);
                }
            }
            parts[part] = Array.AsReadOnly(vertices);
        }
        return Array.AsReadOnly(parts);
    }
}
