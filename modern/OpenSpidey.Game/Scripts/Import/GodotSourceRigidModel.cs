using System.Buffers.Binary;
using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Actors;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Game.Effects;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Import;

/// <summary>Original rigid DC source geometry, including actual face blends, baked colours and optional owned vertex deformation.</summary>
internal partial class GodotSourceRigidModel : Node3D
{
    private readonly List<StandardMaterial3D> materials = [];
    private readonly List<ImageTexture> textures = [];
    private readonly ArrayMesh mesh = new();
    private PsxAssetLibrary bank = null!;
    private PsxModel model = null!;
    private bool disposed;
    internal bool ResourcesDisposed => disposed;
    internal ArrayMesh RenderMesh => mesh;
    internal PsxAssetLibrary SourceBank => bank;
    internal PsxModel SourceModel => model;
    public GodotSourceRigidModel() => TopLevel = true;

    public static GodotSourceRigidModel Create(PsxAssetLibrary bank, ushort modelIndex) => CreateModel<GodotSourceRigidModel>(bank, modelIndex);

    protected static T CreateModel<T>(PsxAssetLibrary bank, ushort modelIndex) where T : GodotSourceRigidModel, new()
    {
        if (modelIndex >= bank.Scene.Models.Count) throw new InvalidDataException("Rigid body selected an absent genuine source model.");
        var result = new T { Name = $"Original{bank.AssetPath.Split('/').Last()}Model{modelIndex}" };
        ((GodotSourceRigidModel)result).bank = bank;
        ((GodotSourceRigidModel)result).model = bank.Scene.Models[modelIndex];
        try
        {
            result.BuildGeometry(null);
            result.AddChild(new MeshInstance3D { Name = "SourceDomeMesh", Mesh = result.mesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            return result;
        }
        catch { result.Release(); result.Free(); throw; }
    }
    public void Synchronize(FixedVector3 position, FixedVector3 scale, ushort itemFlags)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Vector3 axes = (itemFlags & 0x200) != 0 ? new Vector3(scale.X, scale.Y, scale.Z) / 4096f : Vector3.One;
        Transform = new(Basis.FromScale(axes), PsxCoordinates.FixedWorldPosition(position));
        Visible = (itemFlags & 1) == 0;
        // Genuine dome banks lack NumVertices+NumFaces normals. Actual DCModel disables its dynamic tint path,
        // keeping baked vertex colours while source itemRGB controls the AI's transition/death timers.
    }
    public void Synchronize(FixedVector3 position, FixedVector3 scale, ushort itemFlags, ActorAngles angles)
    {
        Synchronize(position, scale, itemFlags);
        Transform3D rotation = PsxCoordinates.CharacterTransform(FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0)));
        if ((itemFlags & 0x200) != 0)
        {
            FixedJointTransform source = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
            var scaling = new FixedJointTransform((short)scale.X, 0, 0, 0, (short)scale.Y, 0, 0, 0, (short)scale.Z, 0, 0, 0);
            rotation = PsxCoordinates.CharacterTransform(SourceVertexLighting.Multiply(source, scaling));
        }
        rotation.Origin = PsxCoordinates.FixedWorldPosition(position);
        Transform = rotation;
    }
    public void Deform(IReadOnlyList<FixedVector3> vertices) => BuildGeometry(vertices);

    private void BuildGeometry(IReadOnlyList<FixedVector3>? deformed)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (deformed is not null && deformed.Count != model.Vertices.Count)
            throw new InvalidDataException("CDomeRing deformation must preserve the genuine source vertex count.");
        mesh.ClearSurfaces();
        byte[] brightness = GodotGlowFlash.SourceBrightness();
        int materialIndex = 0;
        foreach (var group in model.Faces.GroupBy(face => (face.TextureChecksum, Blend: Blend(face.Flags))))
        {
            DecodedTexture? pixels = group.Key.TextureChecksum is uint checksum ? bank.Textures.FirstOrDefault(value => value.Checksum == checksum) : null;
            if (group.Key.TextureChecksum is not null && pixels is null) throw new InvalidDataException("Source rigid face texture is absent.");
            StandardMaterial3D material;
            if (materialIndex < materials.Count) material = materials[materialIndex];
            else
            {
                material = new()
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
                    TextureRepeat = false,
                    Transparency = group.Key.Blend != 0 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.AlphaScissor,
                    BlendMode = group.Key.Blend == 2 ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
                    DepthDrawMode = group.Key.Blend != 0 ? BaseMaterial3D.DepthDrawModeEnum.Disabled : BaseMaterial3D.DepthDrawModeEnum.OpaqueOnly,
                    AlbedoColor = new(1, 1, 1, group.Key.Blend == 2 ? 128 / 255f : 1)
                };
                materials.Add(material);
                if (pixels is not null)
                {
                    using Image image = Image.CreateFromData(pixels.Width, pixels.Height, false, Image.Format.Rgba8, pixels.RgbaPixels);
                    ImageTexture texture = ImageTexture.CreateFromImage(image); textures.Add(texture); material.AlbedoTexture = texture;
                }
            }
            materialIndex++;
            using var tool = new SurfaceTool(); tool.Begin(Mesh.PrimitiveType.Triangles); tool.SetMaterial(material);
            int triangles = 0;
            foreach (PsxFace face in group)
            {
                Add(0, 1, 2); if (!face.IsTriangle) Add(2, 1, 3);
                void Add(int first, int second, int third)
                {
                    foreach (int corner in new[] { first, second, third })
                    {
                        int index = face.VertexIndices[corner]; PsxVertex vertex = model.Vertices[index];
                        if ((vertex.Flags & 3) != 0) throw new NotSupportedException("Rigid bodies require their genuine source vertices.");
                        uint rgb = face.Color;
                        if ((face.Flags & 0x800) != 0)
                        {
                            int palette = (int)((rgb >> (corner * 8)) & 255);
                            if (palette >= bank.Scene.ColorTable.Count) throw new InvalidDataException("Dome face colour-table entry is absent.");
                            rgb = bank.Scene.ColorTable[palette];
                        }
                        tool.SetColor(GodotGlowFlash.SourceColour(rgb, 0, brightness)); tool.SetUV(Uv(face, corner, pixels));
                        var source = deformed is null ? vertex.Position : new System.Numerics.Vector3(deformed[index].X, deformed[index].Y, deformed[index].Z);
                        tool.AddVertex(PsxCoordinates.WorldPosition(source / ((vertex.Flags & 0x10) != 0 ? 8 : 1)));
                    }
                    triangles++;
                }
            }
            if (triangles != 0) { tool.GenerateNormals(); tool.Commit(mesh); }
        }
        if (mesh.GetSurfaceCount() == 0) throw new InvalidDataException("Original rigid body model has no genuine native face geometry.");
    }
    private static int Blend(uint flags) => (flags & 0x40) == 0 ? 0 : (flags & 0x80) != 0 ? 2 : 1;
    private static Vector2 Uv(PsxFace face, int corner, DecodedTexture? texture)
    {
        if (face.TextureCoordinates.Length == 16)
            return new(BinaryPrimitives.ReadUInt16LittleEndian(face.TextureCoordinates.AsSpan(corner * 2, 2)) / 512f,
                BinaryPrimitives.ReadUInt16LittleEndian(face.TextureCoordinates.AsSpan(8 + corner * 2, 2)) / 512f);
        if (face.TextureCoordinates.Length == 8 && texture is not null)
            return new(face.TextureCoordinates[corner * 2] / (float)texture.Width, face.TextureCoordinates[corner * 2 + 1] / (float)texture.Height);
        return Vector2.Zero;
    }
    internal void Release()
    {
        if (disposed) return;
        disposed = true; mesh.Dispose();
        foreach (StandardMaterial3D material in materials) material.Dispose(); materials.Clear();
        foreach (ImageTexture texture in textures) texture.Dispose(); textures.Clear();
    }
    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) Release();
    }
}
