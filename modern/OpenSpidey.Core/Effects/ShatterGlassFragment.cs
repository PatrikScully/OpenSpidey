using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Original textureless additive CGlassBit triangle, bounce/flicker and delayed colour fade.</summary>
public sealed class ShatterGlassFragment
{
    private readonly Func<int,int> random;
    public FixedVector3[] Corners {get;}=new FixedVector3[3];
    public FixedVector3 Position {get;}
    public FixedVector3 Velocity {get;private set;}
    public int GroundY {get;}
    public byte DefaultRed {get;private set;}
    public byte DefaultGreen {get;private set;}
    public byte DefaultBlue {get;private set;}
    public byte Red {get;private set;}
    public byte Green {get;private set;}
    public byte Blue {get;private set;}
    public ushort Age {get;private set;}
    public ushort Lifetime {get;private set;}
    public bool Alive {get;private set;}=true;
    public ShatterGlassFragment(FixedVector3 position,FixedVector3 velocity,int groundY,byte red,byte green,byte blue,Func<int,int> random)
    {
        this.random=random;Position=position;Velocity=velocity;GroundY=groundY;
        for(int index=0;index<3;index++)Corners[index]=position+new FixedVector3((random(201)-100)<<12,(random(201)-100)<<12,(random(201)-100)<<12);
        DefaultRed=Red=(byte)(red>>2);DefaultGreen=Green=(byte)(green>>2);DefaultBlue=Blue=(byte)(blue>>2);
        Lifetime=(ushort)(random(30)+30);
    }
    public void Advance()
    {
        if(!Alive)return;
        for(int index=0;index<3;index++)Corners[index]+=Velocity;
        Velocity+=new FixedVector3(0,10000,0);
        if(Corners[0].Y>GroundY)
        {
            if(Age<2)
            {
                for(int index=0;index<3;index++)Corners[index]=new(Corners[index].X,unchecked(GroundY-(random(50)<<12)),Corners[index].Z);
                Velocity=new(Velocity.X,unchecked(-Velocity.Y)>>2,Velocity.Z);Age++;
            }
            else Velocity=new(Velocity.X,0,Velocity.Z);
            Velocity=new(Velocity.X>>1,Velocity.Y,Velocity.Z>>1);
        }
        if(Age>=2 || random(5)!=0){Red=DefaultRed;Green=DefaultGreen;Blue=DefaultBlue;}
        else {Red=48;Green=48;Blue=64;}
        if(Lifetime!=0)Lifetime--;
        if(Lifetime==0)
        {
            DefaultRed=(byte)Math.Max(DefaultRed-4,0);DefaultGreen=(byte)Math.Max(DefaultGreen-4,0);DefaultBlue=(byte)Math.Max(DefaultBlue-4,0);
        }
        if((DefaultRed|DefaultGreen|DefaultBlue)==0)Alive=false;
    }
}
