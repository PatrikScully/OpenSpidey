using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using System.Buffers.Binary;
using OpenSpidey.Assets.Animation;

namespace OpenSpidey.Game.Import;

/// <summary>Godot adapter for decoded geometry; asset parsing stays independent of the engine.</summary>
public static class PsxGeometryBuilder
{
    public static Node3D Build(PsxScene scene, IReadOnlyList<DecodedTexture> textures)
    {
        var root = new ImportedGeometryRoot { Name = "ImportedScene" };
        try
        {
            var materials = new PsxMaterialLibrary(textures);
            root.Initialize(materials);
            var meshes = new ArrayMesh?[scene.Models.Count];
            for (int index = 0; index < meshes.Length; index++)
            {
                meshes[index] = BuildModel(scene.Models[index], scene.ColorTable, materials);
                if (meshes[index] is { } mesh) root.Own(mesh);
            }

            for (int index = 0; index < scene.Instances.Count; index++)
            {
                PsxInstance instance = scene.Instances[index];
                if ((instance.Flags & 0x1001) != 0 || !PsxRenderPolicy.IsModelVisible(scene.Models[instance.ModelIndex]) || meshes[instance.ModelIndex] is not { } mesh)
                    continue;
                var node = new ImportedSceneMesh
                {
                    Name = $"Instance{index}", SourceInstanceIndex = index, SourceModelIndex = instance.ModelIndex,
                    Mesh = mesh,
                    Transform = PsxCoordinates.InstanceTransform(instance)
                };
                root.AddChild(node);
            }
            return root;
        }
        catch { root.Free(); throw; }
    }

    internal static ArrayMesh? BuildModel(PsxModel model, IReadOnlyList<uint> colorTable, PsxMaterialLibrary materials,
        IReadOnlyList<BoundCharacterVertex>? bindings = null, IList<IReadOnlyList<byte>>? sourceSurfaceVertices = null,
        bool sourceFacesPrepared = false)
    {
        var mesh = new ArrayMesh();
        try
        {
            foreach (var group in model.Faces.GroupBy(face => face.TextureChecksum))
            {
                using var tool = new SurfaceTool();
                tool.Begin(Mesh.PrimitiveType.Triangles);
                int triangles = 0;
                var sourceVertices = new List<byte>();
                DecodedTexture? texture = materials.FindTexture(group.Key);
                foreach (PsxFace face in group)
                {
                    if (sourceFacesPrepared ? (face.Flags & 0xC0) == 0 : !PsxRenderPolicy.IsFaceVisible(face.Flags)) continue;
                    if (bindings is null && face.VertexIndices.Take(face.IsTriangle ? 3 : 4).Any(index => (model.Vertices[index].Flags & 3) != 0))
                        continue;
                    AddTriangle(0, 1, 2);
                    if (!face.IsTriangle) AddTriangle(1, 3, 2);

                    void AddTriangle(int first, int second, int third)
                    {
                        foreach (int corner in new[] { first, second, third })
                        {
                            byte vertexIndex = face.VertexIndices[corner];
                            PsxVertex vertex = model.Vertices[vertexIndex];
                            sourceVertices.Add(vertexIndex);
                            tool.SetColor(ReadColor(face, corner, colorTable));
                            tool.SetUV(ReadUv(face, corner, texture));
                            if (bindings is not null)
                            {
                                BoundCharacterVertex binding = bindings[face.VertexIndices[corner]];
                                tool.SetBones(new[] { binding.PartIndex, 0, 0, 0 });
                                tool.SetWeights(new[] { 1f, 0, 0, 0 });
                                tool.AddVertex(PsxCoordinates.CharacterPosition(binding.Position));
                            }
                            else tool.AddVertex(PsxCoordinates.WorldPosition(vertex.Position / ((vertex.Flags & 0x10) != 0 ? 8 : 1)));
                        }
                        triangles++;
                    }
                }
                if (triangles == 0) continue;
                tool.GenerateNormals();
                tool.SetMaterial(materials.GetMaterial(group.Key));
                tool.Commit(mesh);
                sourceSurfaceVertices?.Add(sourceVertices.AsReadOnly());
            }
            if (mesh.GetSurfaceCount() > 0) return mesh;
            mesh.Dispose(); return null;
        }
        catch { mesh.Dispose(); throw; }
    }

    private static Vector2 ReadUv(PsxFace face, int corner, DecodedTexture? texture)
    {
        if (face.TextureCoordinates.Length == 16)
        {
            ushort u = BinaryPrimitives.ReadUInt16LittleEndian(face.TextureCoordinates.AsSpan(corner * 2, 2));
            ushort v = BinaryPrimitives.ReadUInt16LittleEndian(face.TextureCoordinates.AsSpan(8 + corner * 2, 2));
            return new(u / 512f, v / 512f);
        }
        if (face.TextureCoordinates.Length == 8 && texture is not null)
            return new(face.TextureCoordinates[corner * 2] / (float)texture.Width,
                face.TextureCoordinates[corner * 2 + 1] / (float)texture.Height);
        return Vector2.Zero;
    }

    private static Color ReadColor(PsxFace face, int corner, IReadOnlyList<uint> table)
    {
        uint packed = face.Color;
        if ((face.Flags & 0x800) != 0)
        {
            int index = (int)((packed >> (corner * 8)) & 255);
            if (index >= table.Count) throw new InvalidDataException($"Missing face color index {index}.");
            packed = table[index];
        }
        return new((packed & 255) / 255f, ((packed >> 8) & 255) / 255f, ((packed >> 16) & 255) / 255f);
    }

}
