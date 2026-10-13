using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Actual camera-centered CBackground OBJ meshes; original colors/UVs supply the sky gradient and city images.</summary>
public partial class GodotSourceBackdrops : Node3D
{
    private sealed record Entry(BackgroundState State,GodotSourceBackdrop Mesh,ArrayMesh Geometry,ShaderMaterial[] Materials);
    private readonly List<Entry> entries=[];
    private readonly List<ShaderMaterial> materials=[];
    private readonly Shader shader=new() {Code="""
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, fog_disabled;
uniform sampler2D original_texture : source_color, filter_nearest;
uniform bool textured = false;
uniform float camera_far = 2000.0;
void vertex() {
    vec4 clip = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
    vec4 far_clip = PROJECTION_MATRIX * vec4(0.0, 0.0, -camera_far, 1.0);
    clip.z = clamp(far_clip.z / far_clip.w, -1.0, 1.0) * clip.w;
    POSITION = clip;
}
void fragment() {
    vec4 pixel = textured ? texture(original_texture, UV) : vec4(1.0);
    if (pixel.a < 0.5) discard;
    ALBEDO = COLOR.rgb * pixel.rgb;
    ALPHA = 1.0; // Ordered translucent pipeline without depth writes; original images are opaque/keyed.
}
"""};
    private Func<PsxAssetLibrary> objectBank=null!;
    private Func<IReadOnlyList<DecodedTexture>> textures=null!;
    private SourceActorList? sourceList;
    private bool disposed;
    public IReadOnlyList<BackgroundState> States=>entries.Select(entry=>entry.State).ToArray();
    public int Count=>entries.Count;
    public event Action<string,int>? SourceModelClaimed;
    public static GodotSourceBackdrops Create(Func<PsxAssetLibrary> objectBank,Func<IReadOnlyList<DecodedTexture>> textures)=>
        new() {Name="OriginalSourceBackdrops",objectBank=objectBank,textures=textures};
    public void BindSourceList(SourceActorList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        if(sourceList is not null)throw new InvalidOperationException("Original background bodies already have their source list.");
        sourceList=list;
        foreach(Entry entry in entries)Attach(entry);
    }
    public void Add(uint checksum,ActorAngles angularVelocity)
    {
        PsxAssetLibrary bank=objectBank();
        int index=bank.FindModel(checksum);
        PsxModel model=bank.Scene.Models[index];
        foreach(uint texture in model.Faces.Where(face=>face.TextureChecksum is not null).Select(face=>face.TextureChecksum!.Value).Distinct())
            if(!textures().Any(item=>item.Checksum==texture))throw new InvalidDataException($"Original backdrop{checksum:X8} texture{texture:X8} is not loaded.");
        ArrayMesh geometry=PsxGeometryBuilder.BuildModel(model,bank.Scene.ColorTable,new(textures()))
            ?? throw new InvalidDataException($"Original backdrop{checksum:X8} has no visible source faces.");
        short sourceIndex=checked((short)entries.Count);
        var ownedMaterials=new List<ShaderMaterial>();
        for(int surface=0;surface<geometry.GetSurfaceCount();surface++)
        {
            var original=(StandardMaterial3D)geometry.SurfaceGetMaterial(surface);
            var material=new ShaderMaterial {Shader=shader,RenderPriority=-128+sourceIndex};
            material.SetShaderParameter("textured",original.AlbedoTexture is not null);
            if(original.AlbedoTexture is { } image)material.SetShaderParameter("original_texture",image);
            materials.Add(material);ownedMaterials.Add(material);geometry.SurfaceSetMaterial(surface,material);
        }
        var state=new BackgroundState(checksum,sourceIndex); // Matching original constructor deliberately ignores ang_vel.
        var mesh=new GodotSourceBackdrop {Name=$"OriginalBackdrop{sourceIndex}_{checksum:X8}",State=state,Mesh=geometry,ExtraCullMargin=100000};
        var entry=new Entry(state,mesh,geometry,ownedMaterials.ToArray());
        mesh.Destroyed=()=>Remove(entry);
        AddChild(mesh);entries.Add(entry);Attach(entry);
        SourceModelClaimed?.Invoke(bank.AssetPath,index);
    }
    public void SetVisible(ushort index,bool visible)
    {
        BackgroundState? state=entries.AsEnumerable().Reverse().Select(entry=>entry.State).FirstOrDefault(state=>state.Index==unchecked((short)index));
        if(state is null)return; // Original Backgrnd_On/Off silently finds no match.
        state.ItemFlags=visible?(ushort)(state.ItemFlags&~1):(ushort)(state.ItemFlags|1);
    }
    public void AdvanceSource()
    {
        if(sourceList is not null)return; // Bound bodies advance in the actual source Background phase.
        foreach(Entry entry in entries)Advance(entry);
    }
    private void Attach(Entry entry)=>sourceList?.Attach(entry.Mesh,()=>Advance(entry));
    private static void Advance(Entry entry){entry.State.PrepareSourceUpdate();entry.State.Advance();}
    private void Remove(Entry entry)
    {
        if(!entries.Remove(entry))return;
        sourceList?.Detach(entry.Mesh);
        entry.Mesh.Mesh=null;entry.Geometry.Dispose();
        foreach(ShaderMaterial material in entry.Materials){materials.Remove(material);material.Dispose();}
    }
    public void Synchronize(Camera3D camera)
    {
        foreach(ShaderMaterial material in materials)material.SetShaderParameter("camera_far",camera.Far);
        foreach(Entry entry in entries)
        {
            entry.Mesh.Visible=entry.State.Visible;
            entry.Mesh.GlobalPosition=camera.GlobalPosition;
            // Original PC background scale550090 is3.0. Model vertices are original whole world units.
            var angles=entry.State.Angles;
            float factor=3.1415927f/2048f;
            float x=angles.X*factor,y=angles.Y*factor,z=angles.Z*factor;
            float sx=(float)Math.Sin(x),cx=(float)Math.Cos(x),sy=(float)Math.Sin(y),cy=(float)Math.Cos(y),sz=(float)Math.Sin(z),cz=(float)Math.Cos(z);
            float czcy=cz*cy,szsy=sz*sy,czsy=cz*sy,szcy=sz*cy;
            entry.Mesh.GlobalBasis=new Basis(new(szsy*sx+czcy,-sz*cx,-(szcy*sx-czsy)),
                new(-(czsy*sx-szcy),cz*cx,czcy*sx+szsy),new(-sy*cx,-sx,cy*cx)).Scaled(Vector3.One*3f);
        }
    }
    public override void _Notification(int what)
    {
        if(what!=NotificationPredelete||disposed)return;
        disposed=true;
        foreach(Entry entry in entries.ToArray())entry.Mesh.Free();
        shader.Dispose();
    }
}
