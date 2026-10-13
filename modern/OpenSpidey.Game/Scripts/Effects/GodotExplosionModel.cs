using Godot;
using System.Buffers.Binary;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Effects;

/// <summary>Actual expgrnd PSX model0/1 geometry with the ordinary PC DC unlit blend path.</summary>
public partial class GodotExplosionModel : Node3D
{
    private readonly List<StandardMaterial3D> materials = [];
    private readonly List<ImageTexture> textures = [];
    private ArrayMesh mesh = null!;
    private bool disposed;
    public ExplosionModelState State { get; private set; } = null!;
    internal ArrayMesh RenderMesh => mesh;
    internal IReadOnlyList<StandardMaterial3D> Materials => materials;
    internal IReadOnlyList<ImageTexture> Textures => textures;
    internal bool ResourcesDisposed => disposed;

    public GodotExplosionModel() => TopLevel = true;

    public static GodotExplosionModel Create(byte[] originalBytes, ExplosionModelState state)
    {
        ArgumentNullException.ThrowIfNull(originalBytes);
        ArgumentNullException.ThrowIfNull(state);
        if (!state.ModelName.Equals("expgrnd", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("This effect imports the original expgrnd model family only.");
        PsxScene scene = PsxSceneReader.Read(originalBytes);
        if (state.ModelIndex >= scene.Models.Count) throw new InvalidDataException("Explosion selects an absent source model frame.");
        IReadOnlyList<DecodedTexture> decoded = PsxTextureReader.Read(originalBytes);
        var effect = new GodotExplosionModel { Name = $"OriginalExpgrndModel{state.ModelIndex}", State = state };
        try
        {
            effect.mesh = effect.Build(scene.Models[state.ModelIndex], scene.ColorTable, decoded);
            effect.AddChild(new MeshInstance3D { Name = "SourceExplosionModel", Mesh = effect.mesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            effect.Synchronize();
            return effect;
        }
        catch { effect.Release(); effect.Free(); throw; }
    }

    public void Advance()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        State.Move();
        Synchronize();
    }

    public void Synchronize()
    {
        if (disposed || State is null) return;
        var scale = new Vector3(State.Scale.X, State.Scale.Y, State.Scale.Z) / 4096f;
        // CItem::mScale axes are source signed shorts; model1 starts genuinely at zero.
        Transform = new(Basis.FromScale(scale), PsxCoordinates.FixedWorldPosition(State.Position));
        // CBody::Die changes only body flags. Rendering stays visible until
        // the owner's two-update source lifecycle actually deletes this model.
        Visible = (State.ItemFlags & 1) == 0;
        // Ordinary unlit DCModel_RenderModel does not read the item tint. Source
        // C3DExplosion still fades mRGB and dies on that lifetime; applying an
        // opacity/tint fade here would select behavior from the PSX fallback path.
    }

    private ArrayMesh Build(PsxModel model, IReadOnlyList<uint> colours, IReadOnlyList<DecodedTexture> decoded)
    {
        var result = new ArrayMesh();
        mesh = result; // Factory failure cleanup owns partially built surfaces too.
        byte[] brightness = GodotGlowFlash.SourceBrightness();
        foreach (var group in model.Faces.GroupBy(face => (face.TextureChecksum, Mode: Blend(face.Flags))))
        {
            DecodedTexture? texture = group.Key.TextureChecksum is uint checksum ? decoded.FirstOrDefault(value => value.Checksum == checksum) : null;
            if (group.Key.TextureChecksum is not null && texture is null)
                throw new InvalidDataException("Original explosion face references an absent decoded texture.");
            var material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                VertexColorUseAsAlbedo = true,
                VertexColorIsSrgb = true,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
                TextureRepeat = false,
                Transparency = group.Key.Mode != 0 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.AlphaScissor,
                BlendMode = group.Key.Mode == 2 ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
                DepthDrawMode = group.Key.Mode != 0 ? BaseMaterial3D.DepthDrawModeEnum.Disabled : BaseMaterial3D.DepthDrawModeEnum.OpaqueOnly,
                AlbedoColor = new(1, 1, 1, group.Key.Mode == 2 ? 128 / 255f : 1)
            };
            materials.Add(material);
            if (texture is not null)
            {
                using var image = Image.CreateFromData(texture.Width, texture.Height, false, Image.Format.Rgba8, texture.RgbaPixels);
                ImageTexture uploaded = ImageTexture.CreateFromImage(image);
                textures.Add(uploaded);
                material.AlbedoTexture = uploaded;
            }
            using var tool = new SurfaceTool();
            tool.Begin(Mesh.PrimitiveType.Triangles);
            tool.SetMaterial(material);
            int triangleCount = 0;
            foreach (PsxFace face in group)
            {
                if (face.VertexIndices.Take(face.IsTriangle ? 3 : 4).Any(index => (model.Vertices[index].Flags & 3) != 0))
                    throw new NotSupportedException("Explosion mesh must contain genuine source vertices rather than actor stitch references.");
                AddTriangle(0, 1, 2);
                if (!face.IsTriangle) AddTriangle(2, 1, 3);

                void AddTriangle(int first, int second, int third)
                {
                    foreach (int corner in new[] { first, second, third })
                    {
                        PsxVertex vertex = model.Vertices[face.VertexIndices[corner]];
                        tool.SetColor(Colour(face, corner));
                        tool.SetUV(Uv(face, corner, texture));
                        tool.AddVertex(PsxCoordinates.WorldPosition(vertex.Position / ((vertex.Flags & 0x10) != 0 ? 8 : 1)));
                    }
                    triangleCount++;
                }
            }
            if (triangleCount != 0) { tool.GenerateNormals(); tool.Commit(result); }
        }
        if (result.GetSurfaceCount() == 0) { result.Dispose(); throw new InvalidDataException("Original explosion model has no source face geometry."); }
        return result;

        Color Colour(PsxFace face, int corner)
        {
            uint packed = face.Color;
            if ((face.Flags & 0x800) != 0)
            {
                int index = (int)((packed >> (corner * 8)) & 255);
                if (index >= colours.Count) throw new InvalidDataException("Explosion face references an absent original palette colour.");
                packed = colours[index];
            }
            return GodotGlowFlash.SourceColour(packed, 0, brightness);
        }
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
    private void Release()
    {
        if (disposed) return;
        disposed = true;
        mesh?.Dispose();
        foreach (StandardMaterial3D material in materials) material.Dispose();
        foreach (ImageTexture texture in textures) texture.Dispose();
    }
    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) Release();
    }
}
