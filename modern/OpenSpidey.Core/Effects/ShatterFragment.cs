using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Original CShatterBit/CChunkBit tetrahedron, retaining whole-unit local and world corners.</summary>
public sealed class ShatterFragment
{
    public FixedVector3[] LocalCorners {get;}=new FixedVector3[4];
    public FixedVector3[] WorldCorners {get;}=new FixedVector3[4];
    public uint[] Colours {get;}=new uint[4];
    public (byte U,byte V)[] Uvs {get;}
    public uint TextureChecksum {get;}
    public uint ShardColour {get;}
    public FixedVector3 Position {get;private set;}
    public FixedVector3 Velocity {get;private set;}
    public ActorAngles Angles {get;private set;}
    public ActorAngles Spin {get;}
    public ushort Lifetime {get;private set;}
    public bool Alive {get;private set;}=true;

    public ShatterFragment(FixedVector3 a,FixedVector3 b,FixedVector3 c,FixedVector3 centre,
        FixedVector3 faceCentre,FixedVector3 faceNormal,uint rgb,uint texture,uint shardColour,
        (byte U,byte V)[] uvs,Func<int,int> random)
    {
        LocalCorners[0]=a;LocalCorners[1]=b;LocalCorners[2]=c;
        FixedVector3 ab=b-a,ac=c-a;
        var reversedCross=new FixedVector3(unchecked(ac.Y*ab.Z-ac.Z*ab.Y),unchecked(ac.Z*ab.X-ac.X*ab.Z),unchecked(ac.X*ab.Y-ac.Y*ab.X));
        LocalCorners[3]=FixedVectorMath.Normalize(reversedCross>>12)*(random(96)+128)>>12;
        Spin=random(2)!=0?new((short)(random(160)+80),0,0):new(0,(short)(random(160)+80),0);
        Lifetime=(ushort)(random(30)+45);Position=centre;
        FixedVector3 delta=centre-faceCentre;int distance=ShatterItem.Length(delta);
        if(distance!=0)Velocity=delta*random(48)/distance;else Alive=false;
        Velocity+=faceNormal*random(45);
        Velocity-=new FixedVector3(0,random(175)<<12,0);
        // Original PC 48BF20 calls vector <<= (4E7680), rather than recovered-source >>=.
        if(random(20)==0)Velocity<<=1;
        if(Velocity.Y>0)Velocity=new(Velocity.X,0,Velocity.Z);
        UpdateCorners();
        Colours[0]=rgb&0xFFFFFF;
        for(int index=1;index<4;index++)
        {
            int fraction=random(4096);
            Colours[index]=(uint)(((fraction*(byte)rgb)>>12)|(((fraction*(byte)(rgb>>8))>>12)<<8)|(((fraction*(byte)(rgb>>16))>>12)<<16));
        }
        TextureChecksum=texture;ShardColour=shardColour;Uvs=(ValueTuple<byte,byte>[])uvs.Clone();
    }

    public void Advance()
    {
        if(!Alive)return;
        Position+=Velocity;Velocity+=new FixedVector3(0,20500,0);
        Angles=new(unchecked((short)(Angles.X+Spin.X)),unchecked((short)(Angles.Y+Spin.Y)),unchecked((short)(Angles.Z+Spin.Z)));
        UpdateCorners();
        if(Lifetime==0)Alive=false;else Lifetime--;
    }

    private void UpdateCorners()
    {
        var basis=FixedJointTransform.FromJoint(new(Angles.X,Angles.Y,Angles.Z,0,0,0));
        for(int index=0;index<4;index++)WorldCorners[index]=FixedRotation.Transform(basis,LocalCorners[index])+(Position>>12);
    }
}
