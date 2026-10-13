using Godot;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Effects;

/// <summary>Original protected Shadow2 CQuadBit, including the PC renderer's actual mix blend and 1.03 inverse-depth bias.</summary>
public partial class GodotActorShadow : Node3D
{
    public const uint ShadowTextureChecksum = 0x9F6CA828;
    public ActorShadowState State { get; } = new();
    private readonly ImmediateMesh mesh = new();
    private ShaderMaterial material = null!;
    private ImageTexture texture = null!;
    public static GodotActorShadow Create(DecodedTexture decoded)
    {
        if (decoded.Checksum != ShadowTextureChecksum || decoded.Width != 64 || decoded.Height != 64)
            throw new InvalidDataException("Original named Shadow2 texture is required; quick-animation0 is overridden by CQuadBit::SetTexture.");
        var shadow = new GodotActorShadow { Name = "OriginalBodyShadow", TopLevel = true };
        using var image = Image.CreateFromData(decoded.Width, decoded.Height, false, Image.Format.Rgba8, decoded.RgbaPixels);
        shadow.texture = ImageTexture.CreateFromImage(image);
        shadow.material = new ShaderMaterial { Shader = new Shader { Code = """
            shader_type spatial;
            render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, skip_vertex_transform;
            uniform sampler2D shadow_texture : source_color, filter_linear, repeat_disable;
            uniform float source_tint = 0.0;
            void vertex() { VERTEX = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz / 1.03; }
            void fragment() {
                vec4 texel = texture(shadow_texture, UV);
                ALBEDO = texel.rgb * source_tint;
                ALPHA = texel.a;
            }
            """ } };
        shadow.material.SetShaderParameter("shadow_texture", shadow.texture);
        shadow.AddChild(new MeshInstance3D { Name = "SourceProtectedQuad", Mesh = shadow.mesh,
            MaterialOverride = shadow.material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return shadow;
    }
    public void Rebuild()
    {
        mesh.ClearSurfaces();
        Visible = State.Exists;
        if (!State.Exists) return;
        // Default PC brightness4 maps incoming channels<128 to channel*2, otherwise255.
        byte channel = State.PcVertexChannel;
        material.SetShaderParameter("source_tint", (channel >= 128 ? 255 : channel * 2) / 255f);
        Vector2[] uv = [new(.01f,.01f),new(.99f,.01f),new(.01f,.99f),new(.99f,.99f)];
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        foreach (int index in new[] { 0, 1, 2, 1, 3, 2 })
        { mesh.SurfaceSetUV(uv[index]); mesh.SurfaceAddVertex(PsxCoordinates.FixedWorldPosition(State.Corners[index])); }
        mesh.SurfaceEnd();
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        mesh.Dispose(); material?.Dispose(); texture?.Dispose();
    }
}
