using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public interface IShatterHost
{
    bool LowMemory {get;}
    // Original PC Shatter_Glass traces 3000 units downward from centre+500*normal.
    // Return original contact Y or int.MaxValue when it has no item.
    int TraceGlassGround(FixedVector3 start,FixedVector3 end);
    void EmitChunk(ShatterFragment fragment);
    void EmitGlass(ShatterGlassFragment fragment);
}

/// <summary>Bounded original Shatter_Item/Face/Split; mutable state belongs to one source item instance.</summary>
public sealed class ShatterItem
{
    private sealed class Face(PsxFace source)
    {
        public PsxFace Source {get;}=source;
        public uint Flags=PsxRenderPolicy.PrepareFaceFlags(source.Flags);
        public ushort State=(ushort)((source.CollisionWord??0)>>16);
    }
    private readonly PsxModel model;
    private readonly IReadOnlyList<uint> colours;
    private readonly Dictionary<uint,DecodedTexture> textures=[];
    private readonly Face[] faces;
    public ushort ItemFlags {get;private set;}
    public ShatterItem(PsxAssetLibrary library,int modelIndex,IReadOnlyList<DecodedTexture>? loadedTextures=null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(modelIndex);
        if(modelIndex>=library.Scene.Models.Count)throw new ArgumentOutOfRangeException(nameof(modelIndex));
        model=library.Scene.Models[modelIndex];colours=library.Scene.ColorTable;faces=model.Faces.Select(source=>new Face(source)).ToArray();
        foreach(DecodedTexture texture in loadedTextures??library.Textures)textures.TryAdd(texture.Checksum,texture);
        foreach(Face face in faces)
        {
            int normal=(ushort)(face.Source.CollisionWord??0); // Loader multiplies disk index by8; Shatter_Face shifts it back by3.
            if(normal>=model.Normals.Count)throw new InvalidDataException("Shatter face references a missing original normal.");
            if((face.Flags&1)!=0 && (face.Source.TextureChecksum is not uint checksum || !textures.ContainsKey(checksum)))
                throw new InvalidDataException("Textured shatter face requires its actual loaded source texture.");
            if((face.Flags&0x800)!=0)
                for(int corner=0;corner<(face.Source.IsTriangle?3:4);corner++)
                    if((byte)(face.Source.Color>>(corner*8))>=colours.Count)throw new InvalidDataException("Shatter face references a missing source colour.");
        }
    }
    public ushort FaceState(int index)=>faces[index].State;
    public uint FaceFlags(int index)=>faces[index].Flags;

    public bool Shatter(FixedVector3 position,ActorAngles angles,bool checkSeen,bool markProcessed,IShatterHost host,Func<int,int> random)
    {
        int recursiveCount=0;bool any=false;
        var rotation=FixedJointTransform.FromJoint(new(angles.X,angles.Y,angles.Z,0,0,0));
        foreach(Face face in faces)
        {
            int depth=random(5)==0?1:0;
            int result=Process(face,depth,recursiveCount<6);
            if(result!=0){any=true;if(result==1)recursiveCount++;}
        }
        if(markProcessed && any)ItemFlags|=1;
        return any;

        int Process(Face face,int depth,bool chance)
        {
            if(checkSeen)
            {
                if((face.State&8)==0 || (face.State&1)!=0)return 0;
                if(markProcessed)
                {
                    face.State|=1;face.Flags&=0xFFFFFE7F;
                    if(!chance){if((face.Flags&0x40)!=0 || (face.Flags&1)==0)face.Flags&=~0x40u;return 1;}
                }
                else if(!chance)return 1;
            }
            else if(!chance)return 1;
            int corners=face.Source.IsTriangle?3:4;
            var world=new FixedVector3[corners];
            for(int index=0;index<corners;index++)
            {
                var vertex=model.Vertices[face.Source.VertexIndices[index]].Position;
                world[index]=(FixedRotation.Transform(rotation,new((short)vertex.X,(short)vertex.Y,(short)vertex.Z))+(position>>12))<<12;
            }
            FixedVector3 centre=Centre(world);
            var normalSource=model.Normals[(ushort)(face.Source.CollisionWord??0)].Direction;
            var normal=new FixedVector3((short)normalSource.X,(short)normalSource.Y,(short)normalSource.Z);
            uint rgb=Colour(face.Source.Color,(face.Flags&0x800)!=0,corners);
            if((face.Flags&0x40)==0 && (face.Flags&1)!=0)
            {
                if(!host.LowMemory)
                {
                    uint checksum=face.Source.TextureChecksum!.Value;
                    DecodedTexture texture=textures[checksum];
                    // Shatter_Face reads Texture* rectangle corners, not the source face's UV array.
                    (int U,int V)[] uv=[(0,0),((byte)(texture.Width-1),0),(0,(byte)(texture.Height-1)),((byte)(texture.Width-1),(byte)(texture.Height-1))];
                    uint shard=(face.Flags&0x20)!=0?(4u*((uint)(-texture.Height)&0xF8u|0xF8800000u))|(((uint)(-texture.Width)>>3)&0x1Fu):0;
                    Split(world[0],world[1],world[2],uv[0],uv[1],uv[2],depth);
                    if(corners==4)Split(world[1],world[2],world[3],uv[1],uv[2],uv[3],depth);
                    void Split(FixedVector3 a,FixedVector3 b,FixedVector3 c,(int U,int V) ua,(int U,int V) ub,(int U,int V) uc,int remaining)
                    {
                        if(remaining!=0)
                        {
                            int longest=0,max=Length(b-c),ac=Length(a-c),ab=Length(a-b);
                            if(ac>max){max=ac;longest=1;}if(ab>max)longest=2;
                            int fraction=random(9);
                            if(longest==0){var point=b+((c-b)*fraction>>3);var u=Interpolate(ub,uc,fraction);Split(a,b,point,ua,ub,u,remaining-1);Split(a,c,point,ua,uc,u,remaining-1);}
                            else if(longest==1){var point=a+((c-a)*fraction>>3);var u=Interpolate(ua,uc,fraction);Split(b,a,point,ub,ua,u,remaining-1);Split(b,c,point,ub,uc,u,remaining-1);}
                            else {var point=a+((b-a)*fraction>>3);var u=Interpolate(ua,ub,fraction);Split(c,a,point,uc,ua,u,remaining-1);Split(c,b,point,uc,ub,u,remaining-1);}
                            return;
                        }
                        FixedVector3 aw=a>>12,bw=b>>12,cw=c>>12;
                        FixedVector3 localCentre=(aw+bw+cw)/3;
                        FixedVector3 da=Short(aw-localCentre)+Jitter(),db=Short(bw-localCentre)+Jitter(),dc=Short(cw-localCentre)+Jitter();
                        host.EmitChunk(new(Short(da),Short(db),Short(dc),localCentre<<12,centre,normal,rgb,checksum,shard,
                            [((byte)ua.U,(byte)ua.V),((byte)ub.U,(byte)ub.V),((byte)uc.U,(byte)uc.V)],random));
                        FixedVector3 Jitter(){int value=random(100);return new(unchecked((short)(value*normal.X))>>12,unchecked((short)(value*normal.Y))>>12,unchecked((short)(value*normal.Z))>>12);}
                    }
                }
                return 1;
            }
            int result=1;
            if(!host.LowMemory)
            {
                FixedVector3 glassCentre=(world[1]+world[2])>>1;
                var probe=glassCentre+normal*500;
                int ground=host.TraceGlassGround(probe,probe+new FixedVector3(0,3000<<12,0));
                FixedVector3 dir1=(world[2]-world[0])>>12,dir2=(world[1]-world[0])>>12;
                for(int index=0;index<(corners==3?15:30);index++)
                {
                    int rand1=random(4096),rand2=random(4096);
                    FixedVector3 piece=((dir2*rand2+dir1*rand1)>>12)<<12;piece+=world[0];
                    FixedVector3 direction=FixedVectorMath.Normalize((piece-glassCentre)>>12);
                    int speed1=random(40),speed2=random(40);
                    // Original quad branch passes R,B,B; triangle passes R,G,B.
                    host.EmitGlass(new(piece,normal*speed1+direction*speed2,ground,(byte)rgb,(byte)(rgb>>(corners==4?16:8)),(byte)(rgb>>16),random));
                }
                result=2;
            }
            if(markProcessed)face.Flags&=~0x40u;
            return result;
        }
    }
    private uint Colour(uint packed,bool indexed,int corners)
    {
        if(!indexed)return packed&0xFFFFFF;
        int r=0,g=0,b=0;
        for(int index=0;index<corners;index++){uint color=colours[(byte)(packed>>(index*8))];r+=(byte)color;g+=(byte)(color>>8);b+=(byte)(color>>16);}
        return (uint)(r/corners|((g/corners)<<8)|((b/corners)<<16));
    }
    private static FixedVector3 Centre(FixedVector3[] corners)
    {
        FixedVector3 sum=default;
        foreach(FixedVector3 corner in corners)sum+=corner>>12;
        // PC quad computes signed floor /4, while triangle uses signed truncation /3.
        return (corners.Length==4?sum>>2:sum/3)<<12;
    }
    private static (int U,int V) Interpolate((int U,int V) a,(int U,int V) b,int fraction)=>
        (unchecked(a.U+((fraction*(b.U-a.U))>>3)),unchecked(a.V+((fraction*(b.V-a.V))>>3)));
    private static FixedVector3 Short(FixedVector3 value)=>new(unchecked((short)value.X),unchecked((short)value.Y),unchecked((short)value.Z));
    internal static int Length(FixedVector3 value){var whole=value>>12;return FixedVectorMath.OriginalSquareRoot(unchecked(whole.X*whole.X+whole.Y*whole.Y+whole.Z*whole.Z));}
}
