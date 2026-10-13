using System.Runtime.CompilerServices;
using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Effects;

/// <summary>Original per-item Shatter_Item, chunk tetrahedra and textureless additive glass; no substitute explosion.</summary>
public partial class GodotItemShatter : Node3D,IShatterHost
{
    private sealed record Item(PsxAssetLibrary Library,int ModelIndex,ShatterItem State);
    private sealed record Texture(DecodedTexture Source,ImageTexture Image,StandardMaterial3D Material);
    private readonly ConditionalWeakTable<object,Item> items=new();
    private readonly List<ShatterFragment> chunks=[];
    private readonly List<ShatterGlassFragment> glass=[];
    private readonly Dictionary<uint,Texture> textures=[];
    private readonly ImmediateMesh chunkMesh=new(),glassMesh=new();
    private readonly StandardMaterial3D glassMaterial=new()
    {
        ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,CullMode=BaseMaterial3D.CullModeEnum.Disabled,
        Transparency=BaseMaterial3D.TransparencyEnum.Alpha,BlendMode=BaseMaterial3D.BlendModeEnum.Add,
        DepthDrawMode=BaseMaterial3D.DepthDrawModeEnum.Disabled,VertexColorUseAsAlbedo=true,VertexColorIsSrgb=true,
        AlbedoColor=new(1,1,1,160/255f)
    };
    private Func<FixedVector3,FixedVector3,int> trace=null!;
    private Func<int,int> random=null!;
    private bool disposed;
    public bool LowMemory {get;set;}
    public int ChunkCount=>chunks.Count;
    public int GlassCount=>glass.Count;
    public event Action<SourceParticleRegistration>? ChunkEmitted;
    public event Action<SourceParticleRegistration>? GlassEmitted;
    internal IReadOnlyList<ShatterFragment> Chunks=>chunks;
    internal IReadOnlyList<ShatterGlassFragment> Glass=>glass;
    internal ImmediateMesh ChunkMesh=>chunkMesh;
    internal ImmediateMesh GlassMesh=>glassMesh;
    public static GodotItemShatter Create(Func<FixedVector3,FixedVector3,int> sourceGlassGroundTrace,Func<int,int> sourceRandom)
    {
        ArgumentNullException.ThrowIfNull(sourceGlassGroundTrace);ArgumentNullException.ThrowIfNull(sourceRandom);
        return new(){Name="OriginalItemShatter",TopLevel=true,trace=sourceGlassGroundTrace,random=sourceRandom};
    }
    public override void _Ready()
    {
        AddChild(new MeshInstance3D{Name="OriginalShatterChunks",Mesh=chunkMesh,ExtraCullMargin=100000,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
        AddChild(new MeshInstance3D{Name="OriginalShatterGlass",Mesh=glassMesh,ExtraCullMargin=100000,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
    }
    public bool Shatter(object owner,PsxAssetLibrary bank,int modelIndex,FixedVector3 position,ActorAngles angles,bool checkSeen,bool markProcessed,
        IReadOnlyList<DecodedTexture>? loadedTextures=null)
    {
        ObjectDisposedException.ThrowIf(disposed,this);ArgumentNullException.ThrowIfNull(owner);
        if(!items.TryGetValue(owner,out Item? item))
        {
            item=new(bank,modelIndex,new(bank,modelIndex,loadedTextures));
            foreach(DecodedTexture texture in loadedTextures??bank.Textures)EnsureTexture(texture);
            items.Add(owner,item);
        }
        else if(!ReferenceEquals(item.Library,bank) || item.ModelIndex!=modelIndex)
            throw new InvalidOperationException("A source shatter instance cannot silently change its original model/face state.");
        bool result=item.State.Shatter(position,angles,checkSeen,markProcessed,this,random);Rebuild();return result;
    }
    public void Forget(object owner)=>items.Remove(owner);
    public ShatterItem? InstanceState(object owner)=>items.TryGetValue(owner,out Item? item)?item.State:null;
    public void Advance()
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        foreach(ShatterFragment piece in chunks)piece.Advance();foreach(ShatterGlassFragment piece in glass)piece.Advance();
        chunks.RemoveAll(piece=>!piece.Alive);glass.RemoveAll(piece=>!piece.Alive);Rebuild();
    }
    public int TraceGlassGround(FixedVector3 start,FixedVector3 end)=>trace(start,end);
    public void EmitChunk(ShatterFragment fragment)
    {
        chunks.Insert(0,fragment);
        ChunkEmitted?.Invoke(new(fragment,_=>fragment.Advance(),
            ()=>!disposed && chunks.Contains(fragment) && fragment.Alive,
            ()=>{chunks.Remove(fragment);Rebuild();}));
    }
    public void EmitGlass(ShatterGlassFragment fragment)
    {
        glass.Insert(0,fragment);
        GlassEmitted?.Invoke(new(fragment,_=>fragment.Advance(),
            ()=>!disposed && glass.Contains(fragment) && fragment.Alive,
            ()=>{glass.Remove(fragment);Rebuild();}));
    }
    public override void _Process(double delta)=>Rebuild(); // Rendering never advances the source bit lifetime.
    private void EnsureTexture(DecodedTexture source)
    {
        if(textures.ContainsKey(source.Checksum))return;
        using Image image=Image.CreateFromData(source.Width,source.Height,false,Image.Format.Rgba8,source.RgbaPixels);
        ImageTexture native=ImageTexture.CreateFromImage(image);
        var material=new StandardMaterial3D
        {
            ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,CullMode=BaseMaterial3D.CullModeEnum.Disabled,
            VertexColorUseAsAlbedo=true,VertexColorIsSrgb=true,TextureFilter=BaseMaterial3D.TextureFilterEnum.Nearest,
            AlbedoTexture=native
        };
        if(Enumerable.Range(0,source.Width*source.Height).Any(pixel=>source.RgbaPixels[pixel*4+3]<255))
            material.Transparency=BaseMaterial3D.TransparencyEnum.AlphaScissor;
        textures.Add(source.Checksum,new(source,native,material));
    }
    private void Rebuild()
    {
        if(disposed || !IsInsideTree())return;
        chunkMesh.ClearSurfaces();glassMesh.ClearSurfaces();
        Camera3D? camera=GetViewport().GetCamera3D();if(camera is null)return;
        foreach(var group in chunks.Where(piece=>piece.Alive).GroupBy(piece=>piece.TextureChecksum))
        {
            Texture texture=textures[group.Key];bool begun=false;
            foreach(ShatterFragment piece in group)
            {
                Vector3[] world=piece.WorldCorners.Select(corner=>PsxCoordinates.WorldPosition(new(corner.X,corner.Y,corner.Z))).ToArray();
                // Original PC draws the tetrahedron only when all four reciprocal depths are positive.
                if(world.Any(camera.IsPositionBehind))continue;
                if(!begun){chunkMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles,texture.Material);begun=true;}
                Add(0,1,2);Add(0,3,1);Add(1,3,2);Add(2,3,0);
                void Add(int a,int b,int c)
                {
                    int[] corners=[a,b,c];
                    for(int slot=0;slot<3;slot++)
                    {
                        int corner=corners[slot];uint rgb=piece.Colours[corner];
                        chunkMesh.SurfaceSetColor(new((byte)rgb/255f,(byte)(rgb>>8)/255f,(byte)(rgb>>16)/255f));
                        // Source reapplies UV0/1/2 by triangle slot on each tetrahedron face.
                        chunkMesh.SurfaceSetUV(new(piece.Uvs[slot].U/(float)texture.Source.Width,piece.Uvs[slot].V/(float)texture.Source.Height));
                        chunkMesh.SurfaceAddVertex(world[corner]);
                    }
                }
            }
            if(begun)chunkMesh.SurfaceEnd();
        }
        bool glassBegun=false;
        foreach(ShatterGlassFragment piece in glass.Where(piece=>piece.Alive))
        {
            if(!glassBegun){glassMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles,glassMaterial);glassBegun=true;}
            // Both source QPoly calls have a duplicated final corner; preserve their opposite-winding triangles.
            glassMesh.SurfaceSetColor(new(piece.Red/255f,piece.Green/255f,piece.Blue/255f));
            foreach(int index in new[]{1,0,2,0,1,2})glassMesh.SurfaceAddVertex(PsxCoordinates.FixedWorldPosition(piece.Corners[index]));
        }
        if(glassBegun)glassMesh.SurfaceEnd();
    }
    public override void _Notification(int what)
    {
        if(what!=NotificationPredelete||disposed)return;
        disposed=true;chunkMesh.Dispose();glassMesh.Dispose();glassMaterial.Dispose();
        foreach(Texture texture in textures.Values){texture.Material.Dispose();texture.Image.Dispose();}
        textures.Clear();chunks.Clear();glass.Clear();items.Clear();
    }
}
