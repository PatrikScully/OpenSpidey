using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Actual selected platform model, original loaded images and native tetrahedron/glass lifetimes.</summary>
public static class ItemShatterImportChecks
{
    public static async Task VerifyAsync(Node3D owner,GodotPlatformActor platform,
        IReadOnlyList<DecodedTexture> loadedTextures,GodotActorWorldQuery world)
    {
        PsxAssetLibrary bank=platform.SourceModelLibrary;PsxModel model=bank.Scene.Models[platform.ModelIndex];
        if(!bank.AssetPath.StartsWith("data/L1A",StringComparison.OrdinalIgnoreCase) || model.Faces.Count==0)
            throw new InvalidDataException("Native item shatter requires an actual Level1 selected platform model.");
        int groundTraces=0;var sourceRandom=new GameRandom(0x12A6CC58);
        var effect=GodotItemShatter.Create((start,end)=>
        {
            groundTraces++;
            if(end-start!=new FixedVector3(0,3000<<12,0))throw new InvalidDataException("Native shatter replaced the original glass ground ray.");
            return world.Trace(start,end,includeEnvironmentalActors:true)?.Position.Y??int.MaxValue;
        },sourceRandom.Next);
        object identity=new();
        var viewport=new SubViewport{Name="OriginalShatterProofViewport",Size=new(320,240),OwnWorld3D=true,
            RenderTargetUpdateMode=SubViewport.UpdateMode.Always};
        var fixture=new Node3D{Name="OriginalShatterProofWorld"};
        owner.AddChild(viewport);viewport.AddChild(fixture);fixture.AddChild(effect);
        var camera=new Camera3D{Name="OriginalShatterProofCamera",Current=true,Near=0.01f,Far=10000};fixture.AddChild(camera);
        try
        {
            bool any=effect.Shatter(identity,bank,platform.ModelIndex,platform.Motion.Position,platform.Motion.Angles,false,true,loadedTextures);
            if(!any || effect.ChunkCount+effect.GlassCount==0 || effect.InstanceState(identity)?.ItemFlags!=1)
                throw new InvalidDataException($"Actual platform{platform.SourceNodeId} model{platform.ModelIndex} did not emit its source shatter geometry.");
            var chunks=effect.Chunks.ToArray();var glass=effect.Glass.ToArray();
            if(chunks.Any(piece=>piece.TextureChecksum==0 || piece.Uvs.Length!=3) || (glass.Length!=0 && groundTraces==0))
                throw new InvalidDataException("Native source shatter lost its genuine texture/ground dependency.");
            Vector3[] visibleCorners=chunks.Where(piece=>piece.Alive).SelectMany(piece=>piece.WorldCorners)
                .Select(corner=>PsxCoordinates.WorldPosition(new(corner.X,corner.Y,corner.Z))).ToArray();
            if(visibleCorners.Length==0)throw new InvalidDataException("The genuine selected platform fixture emitted no live textured chunk to verify on the GPU.");
            Vector3 minimum=visibleCorners.Aggregate((a,b)=>new Vector3(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Min(a.Z,b.Z))),maximum=visibleCorners.Aggregate((a,b)=>new Vector3(Math.Max(a.X,b.X),Math.Max(a.Y,b.Y),Math.Max(a.Z,b.Z)));
            Vector3 target=(minimum+maximum)/2;float distance=Math.Max((maximum-minimum).Length()*2,5);
            camera.GlobalPosition=target+new Vector3(distance*0.3f,distance*0.2f,distance);camera.LookAt(target,Vector3.Up);
            await owner.ToSignal(owner.GetTree(),SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(),SceneTree.SignalName.ProcessFrame);
            foreach(ShatterFragment chunk in chunks)
            {
                DecodedTexture image=loadedTextures.First(texture=>texture.Checksum==chunk.TextureChecksum);
                if(chunk.Uvs.Any(uv=>uv.U>unchecked((byte)(image.Width-1)) || uv.V>unchecked((byte)(image.Height-1))))
                    throw new InvalidDataException("Original texture rectangle was replaced by arbitrary fragment UVs.");
            }
            if(effect.ChunkMesh.GetSurfaceCount()==0)throw new InvalidDataException("Visible original chunks did not upload any GPU surfaces.");
            uint[] visibleTextures=chunks.Where(piece=>piece.Alive).Select(piece=>piece.TextureChecksum).Distinct().ToArray();
            int uploadedVertices=0;
            for(int surface=0;surface<effect.ChunkMesh.GetSurfaceCount();surface++)
            {
                using var arrays=effect.ChunkMesh.SurfaceGetArrays(surface);
                uploadedVertices+=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length;
                if(arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length%12!=0 ||
                    effect.ChunkMesh.SurfaceGetMaterial(surface) is not StandardMaterial3D {AlbedoTexture:not null} material)
                    throw new InvalidDataException("Native chunk mesh must retain four genuine textured tetrahedron faces per visible fragment.");
                DecodedTexture source=loadedTextures.First(texture=>texture.Checksum==visibleTextures[surface]);
                using Image uploaded=material.AlbedoTexture.GetImage();
                if(uploaded.GetWidth()!=source.Width || uploaded.GetHeight()!=source.Height || !uploaded.GetData().SequenceEqual(source.RgbaPixels))
                    throw new InvalidDataException("Native shatter texture upload differs from actual decoded source RGBA bytes.");
            }
            if(uploadedVertices!=chunks.Count(piece=>piece.Alive)*12)throw new InvalidDataException("The isolated camera did not expose all live original chunk tetrahedra.");
            if(glass.Length!=0)
            {
                if(effect.GlassMesh.GetSurfaceCount()!=1 || effect.GlassMesh.SurfaceGetMaterial(0) is not StandardMaterial3D material ||
                    material.AlbedoTexture is not null || material.BlendMode!=BaseMaterial3D.BlendModeEnum.Add || material.AlbedoColor.A!=160/255f)
                    throw new InvalidDataException("Original glass PC texture1 must be untextured blend2 with alphaA0.");
            }
            ushort[] lifetimes=chunks.Select(piece=>piece.Lifetime).ToArray();
            effect.Advance();
            for(int index=0;index<chunks.Length;index++)
                if(chunks[index].Alive && chunks[index].Lifetime!=lifetimes[index]-1)
                    throw new InvalidDataException("Native shatter advanced lifetime outside the single source bit phase.");
            for(int update=0;update<95;update++)effect.Advance();
            if(effect.ChunkCount!=0 || effect.GlassCount!=0)throw new InvalidDataException("Native source shards did not retire through their actual lifetimes/colour fades.");
            effect.Forget(identity);
            if(effect.InstanceState(identity)is not null)throw new InvalidDataException("Destroyed source item retained its mutable shatter face owner.");
            GD.Print($"Verified original platform item shatter: model{platform.ModelIndex}, {chunks.Length} textured tetrahedra, {glass.Length} additive glass triangles and source lifetimes.");
        }
        finally {viewport.Free();}
    }
}
