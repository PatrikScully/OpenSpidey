using Godot;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Effects;

/// <summary>Protected source CQuadBit with the cinematic actor's genuine limb footprint and Shadow2 texture.</summary>
public partial class GodotCinematicFootprintShadow : Node3D
{
    private readonly ImmediateMesh mesh = new();
    private ShaderMaterial material = null!;
    private ImageTexture texture = null!;
    private bool destroyed;
    public bool IsAlive => !destroyed;
    public IReadOnlyList<FixedVector3> SourceCorners { get; private set; } = [];
    public const short SourceDepthBias = 32;
    public const byte SourceTransparency = 0x40;

    public static GodotCinematicFootprintShadow Create(DecodedTexture decoded)
    {
        if (decoded.Checksum != GodotActorShadow.ShadowTextureChecksum || decoded.Width != 64 || decoded.Height != 64)
            throw new InvalidDataException("Cinematic footprint requires the genuine64x64 Shadow2 texture.");
        var shadow = new GodotCinematicFootprintShadow { Name = "OriginalCinematicFootprint", TopLevel = true };
        using var image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.RgbaPixels);
        shadow.texture = ImageTexture.CreateFromImage(image);
        shadow.material = new ShaderMaterial { Shader = new Shader { Code = """
            shader_type spatial;
            render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, skip_vertex_transform;
            uniform sampler2D shadow_texture : source_color, filter_linear, repeat_disable;
            void vertex() { VERTEX = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz / 1.03; }
            void fragment() {
                vec4 texel = texture(shadow_texture, UV);
                ALBEDO = texel.rgb * (94.0 / 255.0);
                ALPHA = texel.a;
            }
            """ } };
        shadow.material.SetShaderParameter("shadow_texture", shadow.texture);
        shadow.AddChild(new MeshInstance3D { Name = "SourceProtectedQuad", Mesh = shadow.mesh,
            MaterialOverride = shadow.material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return shadow;
    }

    public void SetCorners(IReadOnlyList<FixedVector3> corners)
    {
        ObjectDisposedException.ThrowIf(destroyed, this);
        if (corners.Count != 4) throw new ArgumentException("Source footprint needs four corners.", nameof(corners));
        SourceCorners = Array.AsReadOnly(corners.ToArray());
        mesh.ClearSurfaces();
        Vector2[] uv = [new(.01f, .01f), new(.99f, .01f), new(.01f, .99f), new(.99f, .99f)];
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        foreach (int index in new[] { 0, 1, 2, 1, 3, 2 })
        { mesh.SurfaceSetUV(uv[index]); mesh.SurfaceAddVertex(PsxCoordinates.FixedWorldPosition(corners[index])); }
        mesh.SurfaceEnd();
    }

    public void DestroySourceBit()
    {
        if (destroyed) return;
        destroyed = true; Visible = false; mesh.ClearSurfaces();
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        DestroySourceBit(); mesh.Dispose(); material?.Dispose(); texture?.Dispose();
    }
}
