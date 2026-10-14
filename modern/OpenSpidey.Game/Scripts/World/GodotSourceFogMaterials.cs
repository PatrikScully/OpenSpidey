using Godot;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Original static-world vertex fade; geometric collision and source material resources remain owned by the importer.</summary>
public sealed class GodotSourceFogMaterials : IDisposable
{
    private sealed record Surface(ulong Identity,ImportedSceneMesh Mesh,int Index,Material? PreviousOverride,ShaderMaterial Material,ShaderMaterial Translucent);
    private readonly List<Surface> surfaces=[];
    private readonly HashSet<ulong> seen=[];
    private const string FogCode="""
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_always, fog_disabled;
uniform sampler2D original_texture : source_color, filter_nearest;
uniform bool textured=false;
uniform bool keyed=false;
uniform bool white_fade=true;
uniform float source_start=6000.0;
uniform float source_end=8048.0;
uniform float pc_start=600000.0;
uniform float pc_end=804800.0;
uniform vec4 pc_colour : source_color=vec4(1.0);
uniform float item_alpha=1.0;
varying vec4 source_colour;
void vertex() {
    float source_depth=-(MODELVIEW_MATRIX*vec4(VERTEX,1.0)).z*100.0;
    source_colour=COLOR;
    if(white_fade && source_depth>source_start) {
        float reciprocal=1.0/source_depth;
        float near_reciprocal=1.0/source_end;
        float remap=reciprocal>near_reciprocal ? trunc((reciprocal-near_reciprocal)*(255.0/(1.0/source_start-near_reciprocal))) : 0.0;
        source_colour.a=floor(255.0*remap/256.0)/255.0;
    }
    if(source_depth>=pc_start && pc_end>pc_start) {
        float column=trunc(min((source_depth-pc_start)/(pc_end-pc_start),1.0)*511.0);
        source_colour.rgb=floor(mix(source_colour.rgb,pc_colour.rgb,column/511.0)*255.0)/255.0;
    }
}
void fragment() {
    vec4 pixel=textured?texture(original_texture,UV):vec4(1.0);
    if(keyed && pixel.a<0.5)discard;
    if(source_colour.a<=0.0)discard;
    ALBEDO=source_colour.rgb*pixel.rgb;
    ALPHA=source_colour.a;
}
""";
    private readonly Shader shader=new() {Code=FogCode};
    /// <summary>CItem flag800 global face override0x40: PC blend mode2 (SRCALPHA, ONE) without depth writes.</summary>
    private readonly Shader translucentShader=new() {Code=FogCode
        .Replace("render_mode unshaded, cull_disabled, depth_draw_always, fog_disabled;","render_mode unshaded, cull_disabled, blend_add, depth_draw_never, fog_disabled;")
        .Replace("ALPHA=source_colour.a;","ALPHA=source_colour.a*item_alpha;")};
    public int SurfaceCount=>surfaces.Count;
    public void Attach(Node root)
    {
        if(root is ImportedSceneMesh mesh && mesh.Mesh is { } geometry && seen.Add(mesh.GetInstanceId()))
        {
            for(int index=0;index<geometry.GetSurfaceCount();index++)
            {
                Material? previous=mesh.GetSurfaceOverrideMaterial(index);
                Material? source=mesh.SourceSurfaceMaterial(index) ?? previous ?? geometry.SurfaceGetMaterial(index);
                if(source is not StandardMaterial3D original)
                    throw new NotSupportedException("Source static-world fog requires the imported original textured/vertex-color material.");
                ShaderMaterial Create(Shader code)
                {
                    var material=new ShaderMaterial {Shader=code};
                    material.SetShaderParameter("textured",original.AlbedoTexture is not null);
                    material.SetShaderParameter("keyed",original.Transparency==BaseMaterial3D.TransparencyEnum.AlphaScissor);
                    if(original.AlbedoTexture is { } texture)material.SetShaderParameter("original_texture",texture);
                    return material;
                }
                ShaderMaterial opaque=Create(shader),translucent=Create(translucentShader);
                mesh.SetSurfaceOverrideMaterial(index,mesh.SourceTranslucency is null?opaque:translucent);
                surfaces.Add(new(mesh.GetInstanceId(),mesh,index,previous,opaque,translucent));
            }
        }
        foreach(Node child in root.GetChildren())Attach(child);
    }
    public void Synchronize(RenderFogState state)
    {
        for(int index=surfaces.Count-1;index>=0;index--)
            if(!GodotObject.IsInstanceValid(surfaces[index].Mesh))
            {seen.Remove(surfaces[index].Identity);surfaces[index].Material.Dispose();surfaces[index].Translucent.Dispose();surfaces.RemoveAt(index);}
        foreach(Surface surface in surfaces)
        {
            if(!GodotObject.IsInstanceValid(surface.Mesh))continue;
            float? alpha=surface.Mesh.SourceTranslucency;
            ShaderMaterial material=alpha is null?surface.Material:surface.Translucent;
            if(surface.Index<(surface.Mesh.Mesh?.GetSurfaceCount()??0)&&!ReferenceEquals(surface.Mesh.GetSurfaceOverrideMaterial(surface.Index),material))
                surface.Mesh.SetSurfaceOverrideMaterial(surface.Index,material);
            if(alpha is { } itemAlpha)material.SetShaderParameter("item_alpha",itemAlpha);
            material.SetShaderParameter("white_fade",state.WhiteOpacityFade);
            material.SetShaderParameter("source_start",(float)state.Start);
            material.SetShaderParameter("source_end",(float)state.End);
            material.SetShaderParameter("pc_start",state.PcStart);
            material.SetShaderParameter("pc_end",state.PcEnd);
            uint colour=state.PcColour;
            material.SetShaderParameter("pc_colour",new Color(((colour>>16)&255)/255f,((colour>>8)&255)/255f,(colour&255)/255f));
        }
    }
    public void Dispose()
    {
        foreach(Surface surface in surfaces)
        {
            if(GodotObject.IsInstanceValid(surface.Mesh))surface.Mesh.SetSurfaceOverrideMaterial(surface.Index,surface.PreviousOverride);
            surface.Material.Dispose();surface.Translucent.Dispose();
        }
        surfaces.Clear();seen.Clear();shader.Dispose();translucentShader.Dispose();
    }
}
