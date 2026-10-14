using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Effects;

/// <summary>One real protected Jonah CQuadBit, with actual pose hooks, Shadow2 and deterministic native resource ownership.</summary>
public partial class GodotJonahFootprintShadow : Node3D
{
    private DecodedTexture sourceTexture = null!;
    private ImmediateMesh? mesh;
    private Shader? shader;
    private ShaderMaterial? material;
    private ImageTexture? texture;
    private bool disposed;
    public bool HasSourceQuad => mesh is not null;
    public bool IsAlive => !disposed && HasSourceQuad;
    public bool ResourcesDisposed => disposed;
    public int SourceQuadCreations { get; private set; }
    public IReadOnlyList<FixedVector3> SourceCorners { get; private set; } = [];
    public const bool SourceProtected = true;
    public const short SourceDepthBias = 32;
    public const byte SourceTransparency = 64;
    public const uint SourceCodeBgr = 0x1C0341;

    public static GodotJonahFootprintShadow Create(DecodedTexture decoded)
    {
        ArgumentNullException.ThrowIfNull(decoded);
        if (decoded.Checksum != GodotActorShadow.ShadowTextureChecksum || decoded.Width != 64 || decoded.Height != 64)
            throw new InvalidDataException("Jonah's source CQuadBit requires the genuine64x64 Shadow2 texture.");
        return new() { Name = "SourceJonahFootprint", TopLevel = true, sourceTexture = decoded };
    }

    public void UpdateFromPose(ImportedAnimatedModel model, FixedVector3 position, FixedVector3 transformTranslation,
        FixedJointTransform rotation, ActorScriptVariables variables)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (model.SourcePartCount != 16 || model.ClipCount != 26)
            throw new InvalidDataException("Jonah footprint requires a genuine16-part,26-clip Jameson/Joel model.");
        var pose = new FixedJointTransform[model.SourcePartCount];
        for (int part = 0; part < pose.Length; part++) pose[part] = model.GetSourcePartPose(part);
        ArgumentNullException.ThrowIfNull(variables);
        SourceCorners = Array.AsReadOnly(JonahFootprintGeometry.FromPose(position, transformTranslation, rotation, variables.Registers[0], pose));
        if (!HasSourceQuad) CreateSourceQuad();
        mesh!.ClearSurfaces();
        Vector2[] uv = [new(.01f, .01f), new(.99f, .01f), new(.01f, .99f), new(.99f, .99f)];
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        foreach (int index in new[] { 0, 1, 2, 1, 3, 2 })
        { mesh.SurfaceSetUV(uv[index]); mesh.SurfaceAddVertex(PsxCoordinates.FixedWorldPosition(SourceCorners[index])); }
        mesh.SurfaceEnd();
    }

    private void CreateSourceQuad()
    {
        try
        {
            using var image = Image.CreateFromData(sourceTexture.Width, sourceTexture.Height, false, Image.Format.Rgba8, sourceTexture.RgbaPixels);
            texture = ImageTexture.CreateFromImage(image);
            shader = new() { Code = """
                shader_type spatial;
                render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, skip_vertex_transform;
                uniform sampler2D shadow_texture : source_color, filter_linear, repeat_disable;
                void vertex() { VERTEX = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz / 1.03; }
                void fragment() {
                    vec4 texel = texture(shadow_texture, UV);
                    ALBEDO = texel.rgb * (94.0 / 255.0);
                    ALPHA = texel.a;
                }
                """ };
            material = new() { Shader = shader };
            material.SetShaderParameter("shadow_texture", texture);
            mesh = new();
            AddChild(new MeshInstance3D { Name = "SourceProtectedJonahQuad", Mesh = mesh, MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            SourceQuadCreations++;
        }
        catch { ReleaseResources(); throw; }
    }

    internal Resource[] CaptureOwnedResources() => new Resource?[] { mesh, material, shader, texture }.OfType<Resource>().ToArray();

    private void ReleaseResources()
    {
        if (disposed) return;
        disposed = true;
        Visible = false;
        mesh?.ClearSurfaces();
        mesh?.Dispose(); material?.Dispose(); shader?.Dispose(); texture?.Dispose();
    }
    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) ReleaseResources();
    }
}
