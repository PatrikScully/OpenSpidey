using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Original Level1 OBJ image/gradient ownership and rendered far-layer proof.</summary>
public static class RenderEnvironmentImportChecks
{
    public static async Task VerifyAsync(Node3D owner,GodotRenderEnvironment environment,PsxAssetLibrary objects,
        GodotPlayer player,PlayerFollowCamera camera)
    {
        if(!objects.AssetPath.Equals("data/L1A1_O.psx",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Original backdrop proof requires the actual L1A1 object bank.");
        BackgroundState[] backgrounds=environment.Backdrops.States.ToArray();
        if(backgrounds.Length!=2 || backgrounds[0].Checksum!=0xDE834642 || backgrounds[1].Checksum!=0x160FC566 ||
            backgrounds.Any(state=>state.AngularVelocity!=default || state.Angles!=default || (state.BodyFlags&~4u)!=0x10))
            throw new InvalidDataException("Original Restart14 did not create its two source502 backdrop bodies in creation order with ignored angular velocity.");
        foreach(BackgroundState background in backgrounds)
        {
            PsxModel original=objects.Scene.Models[objects.FindModel(background.Checksum)];
            MeshInstance3D native=environment.Backdrops.GetNode<MeshInstance3D>($"OriginalBackdrop{background.Index}_{background.Checksum:X8}");
            int expected=original.Faces.Sum(face=>face.IsTriangle?3:6);
            int actual=0;
            for(int surface=0;surface<native.Mesh!.GetSurfaceCount();surface++)
            {
                using var arrays=native.Mesh.SurfaceGetArrays(surface);
                actual+=arrays[(int)Godot.Mesh.ArrayType.Vertex].AsVector3Array().Length;
                if(native.Mesh.SurfaceGetMaterial(surface) is not ShaderMaterial material || material.RenderPriority!=-128+background.Index)
                    throw new InvalidDataException("Source tail-to-head background draw order was not retained in native material priority.");
            }
            if(actual!=expected || original.Faces.Select(face=>face.TextureChecksum).Distinct().Any(checksum=>checksum is null))
                throw new InvalidDataException("Original background faces/images were replaced or omitted.");
        }
        bool playerProcessing=player.IsPhysicsProcessing(),cameraProcessing=camera.IsPhysicsProcessing();
        ushort[] flags=backgrounds.Select(state=>state.ItemFlags).ToArray();
        player.SetPhysicsProcess(false);camera.SetPhysicsProcess(false);
        try
        {
            // Finish the genuine ten-render transition before comparing stable raster samples.
            for(int frame=0;frame<12 && environment.Fog.RemainingSteps!=0;frame++)await Frames();
            if(backgrounds.Any(state=>state.BodyFlags!=0x10))
                throw new InvalidDataException("Background EveryFrame did not clear its original constructor flag before AI.");
            if(environment.Fog.Start!=8000 || environment.Fog.End!=12088 || !environment.Fog.WhiteOpacityFade || environment.FogSurfaceCount==0)
                throw new InvalidDataException("Original Level1 render setup did not retain its exact reciprocal fog range and static world material ownership.");
            foreach(BackgroundState state in backgrounds)state.ItemFlags|=0x8000;
            await Frames();
            using Image before=owner.GetViewport().GetTexture().GetImage();
            foreach(BackgroundState state in backgrounds)state.ItemFlags&=0x7FFF;
            await Frames();
            using Image after=owner.GetViewport().GetTexture().GetImage();
            Rect2 area=PlayerArea(player,camera).Grow(2);
            int actorSamples=0,preserved=0,changed=0;
            int x0=Math.Clamp((int)area.Position.X,0,before.GetWidth()),y0=Math.Clamp((int)area.Position.Y,0,before.GetHeight());
            int x1=Math.Clamp((int)Math.Ceiling(area.End.X),0,before.GetWidth()),y1=Math.Clamp((int)Math.Ceiling(area.End.Y),0,before.GetHeight());
            for(int y=0;y<before.GetHeight();y++)for(int x=0;x<before.GetWidth();x++)
            {
                Color first=before.GetPixel(x,y),second=after.GetPixel(x,y);
                float difference=Math.Abs(first.R-second.R)+Math.Abs(first.G-second.G)+Math.Abs(first.B-second.B);
                if(difference>0.03f)changed++;
                if(x<x0 || x>=x1 || y<y0 || y>=y1)continue;
                bool suitPixel=first.R>0.15f && first.R>first.G*2.5f && first.R>first.B*2.5f ||
                    first.B>0.15f && first.B>first.R*2.5f && first.B>first.G*2.5f;
                if(!suitPixel)continue;actorSamples++;if(difference<0.015f)preserved++;
            }
            if(changed<200 || actorSamples<20 || preserved<actorSamples*0.95f)
                throw new InvalidDataException($"Native backdrop layering failed: changedBackgroundPixels={changed}, playerSuitPixels={actorSamples}, preserved={preserved}; actual far projection must keep the opaque/posed player visible.");
            foreach(BackgroundState background in backgrounds)
            {
                MeshInstance3D mesh=environment.Backdrops.GetNode<MeshInstance3D>($"OriginalBackdrop{background.Index}_{background.Checksum:X8}");
                if(mesh.GlobalPosition.DistanceTo(camera.GlobalPosition)>0.0001f || mesh.GlobalBasis.X.Length()<2.999f || mesh.GlobalBasis.X.Length()>3.001f)
                    throw new InvalidDataException("Native original city/gradient must remain camera-centered at exact source scale3.");
            }
            Error result=after.SavePng("/tmp/openspidey-native-source-background.png");
            if(result!=Error.Ok)throw new IOException($"Could not save original rendered background proof: {result}.");
            GD.Print($"Verified original Level1 sky/city/fog: two genuine OBJ image models, scale3/camera centering, exact8000/12088 reciprocal range, {changed} backdrop pixels and {preserved}/{actorSamples} preserved player suit pixels.");
        }
        finally
        {
            for(int index=0;index<backgrounds.Length;index++)backgrounds[index].ItemFlags=flags[index];
            player.SetPhysicsProcess(playerProcessing);camera.SetPhysicsProcess(cameraProcessing);
        }
        async Task Frames()
        {
            await owner.ToSignal(owner.GetTree(),SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(),SceneTree.SignalName.ProcessFrame);
        }
    }
    private static Rect2 PlayerArea(GodotPlayer player,Camera3D camera)
    {
        Aabb bounds=player.Model.PoseBounds;
        Rect2 rectangle=default;bool first=true;
        for(int corner=0;corner<8;corner++)
        {
            Vector3 local=bounds.Position+new Vector3((corner&1)!=0?bounds.Size.X:0,(corner&2)!=0?bounds.Size.Y:0,(corner&4)!=0?bounds.Size.Z:0);
            Vector2 screen=camera.UnprojectPosition(player.Model.GlobalTransform*local);
            if(first){rectangle=new(screen,Vector2.Zero);first=false;}else rectangle=rectangle.Expand(screen);
        }
        return rectangle;
    }
}
