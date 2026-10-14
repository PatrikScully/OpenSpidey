using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Owns the native primary-tail mesh and the captured genuine source texture generation. AI and spool-region allocation are separate owners.</summary>
internal partial class GodotScorpionTailModel : Node3D
{
    private readonly ArrayMesh mesh = new();
    private readonly StandardMaterial3D material = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
        TextureRepeat = false
    };
    private ImageTexture? texture;
    private IReadOnlyList<ActorHook> hooks = [];
    private IReadOnlyList<PsxFace> faces = [];
    private bool disposed;
    private readonly Color paletteColour = GodotGlowFlash.SourceColour(ScorpionTailMeshLayout.PaletteColour, 0, GodotGlowFlash.SourceBrightness());
    public ScorpionTailGeometry Geometry { get; } = new();
    public PsxAssetLibrary SourceBank { get; private set; } = null!;
    public DecodedTexture SourceTexture { get; private set; } = null!;
    internal bool ResourcesDisposed => disposed;
    internal ArrayMesh RenderMesh => mesh;
    internal int SourceRenderVisits { get; private set; }

    public GodotScorpionTailModel() => TopLevel = true;

    public static GodotScorpionTailModel Create(PsxAssetLibrary bank, DecodedTexture sourceTexture, OriginalScorpionTailData data)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(sourceTexture);
        ArgumentNullException.ThrowIfNull(data);
        if (!bank.Textures.Any(value => ReferenceEquals(value, sourceTexture)))
            throw new InvalidDataException("The primary tail must retain a texture from its genuine selected bank generation.");
        IReadOnlyList<PsxFace> sourceFaces = ScorpionTailMeshLayout.CreateFaces(sourceTexture);
        if (sourceTexture.RgbaPixels.Length != checked(sourceTexture.Width * sourceTexture.Height * 4))
            throw new InvalidDataException("The primary tail requires the complete decoded genuine texture pixels.");
        var owner = new GodotScorpionTailModel
        {
            Name = "OriginalScorpionPrimaryTail", SourceBank = bank, SourceTexture = sourceTexture,
            hooks = data.RenderHooks, faces = sourceFaces, Visible = false
        };
        try
        {
            using Image image = Image.CreateFromData(sourceTexture.Width, sourceTexture.Height, false, Image.Format.Rgba8, sourceTexture.RgbaPixels);
            owner.texture = ImageTexture.CreateFromImage(image);
            owner.material.AlbedoTexture = owner.texture;
            if (Enumerable.Range(0, sourceTexture.Width * sourceTexture.Height).Any(pixel => sourceTexture.RgbaPixels[pixel * 4 + 3] < 255))
                owner.material.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
            owner.AddChild(new MeshInstance3D { Name = "SourceTailQuads", Mesh = owner.mesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            return owner;
        }
        catch { owner.Free(); throw; }
    }

    /// <summary>Call in the source render phase after AI's TailPath.Update; a missing region preserves all prior geometry.</summary>
    public void Render(bool initialized, ReadOnlySpan<FixedVector3> nodes, Func<ActorHook, FixedVector3> readDynamicHook)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!Geometry.Rebuild(initialized, nodes, hooks, readDynamicHook)) return;
        SourceRenderVisits++;
        Transform = new(Basis.Identity, PsxCoordinates.FixedWorldPosition(Geometry.Position));
        mesh.ClearSurfaces();
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(material);
        foreach (PsxFace face in faces)
        {
            Append(0); Append(1); Append(2);
            Append(2); Append(1); Append(3);
            void Append(int corner)
            {
                int vertex = face.VertexIndices[corner];
                FixedVector3 point = Geometry.Vertices[vertex], normal = Geometry.Normals[vertex];
                surface.SetNormal(new Vector3(normal.X, -normal.Y, -normal.Z) / 4096f);
                surface.SetColor(paletteColour);
                surface.SetUV(Uv(face, corner));
                surface.AddVertex(PsxCoordinates.WorldPosition(new(point.X, point.Y, point.Z)));
            }
        }
        surface.Commit(mesh);
        Visible = true;
    }

    internal void VerifyNativeGeometry()
    {
        if (mesh.GetSurfaceCount() != 1 || !ReferenceEquals(mesh.SurfaceGetMaterial(0), material))
            throw new InvalidDataException("The primary tail must submit its one owned genuine texture surface.");
        using Godot.Collections.Array arrays = mesh.SurfaceGetArrays(0);
        Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        Vector3[] normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        Vector2[] uvs = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
        Color[] colours = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
        if (vertices.Length != ScorpionTailMeshLayout.FaceCount * 6 || normals.Length != vertices.Length ||
            uvs.Length != vertices.Length || colours.Length != vertices.Length)
            throw new InvalidDataException("Native primary-tail arrays lost source corners or attributes.");
        int index = 0;
        foreach (PsxFace face in faces)
        foreach (int corner in new[] { 0, 1, 2, 2, 1, 3 })
        {
            int source = face.VertexIndices[corner];
            FixedVector3 point = Geometry.Vertices[source], normal = Geometry.Normals[source];
            Vector3 expectedPoint = PsxCoordinates.WorldPosition(new(point.X, point.Y, point.Z));
            Vector3 expectedNormal = new Vector3(normal.X, -normal.Y, -normal.Z) / 4096f;
            // Native ArrayMesh packs normals and colours. Their quantization is independent of the exact core render storage.
            if (vertices[index].DistanceTo(expectedPoint) > 0.00001f || normals[index].DistanceTo(expectedNormal.Normalized()) > 0.002f ||
                uvs[index].DistanceTo(Uv(face, corner)) > 0.00001f || Math.Abs(colours[index].R - paletteColour.R) > 1 / 255f)
                throw new InvalidDataException($"Native primary-tail corner{index} differs from its original vertex,normal,UV or palette upload.");
            index++;
        }
        if (Transform.Origin.DistanceTo(PsxCoordinates.FixedWorldPosition(Geometry.Position)) > 0.00001f)
            throw new InvalidDataException("The primary tail lost its independent original midpoint transform.");
    }

    internal Resource[] CaptureOwnedResources() => texture is null ? [mesh, material] : [mesh, material, texture];

    // RenderGeneral478180 divides by the original entry's u3-u0/v3-v0 range, rather than the decoded image dimensions.
    private Vector2 Uv(PsxFace face, int corner) => new(face.TextureCoordinates[corner * 2] / (float)(SourceTexture.Width - 1),
        face.TextureCoordinates[corner * 2 + 1] / (float)(SourceTexture.Height - 1));

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        foreach (Node child in GetChildren())
            if (child is MeshInstance3D instance) instance.Mesh = null;
        material.AlbedoTexture = null;
        mesh.Dispose(); material.Dispose(); texture?.Dispose(); texture = null;
    }
}
