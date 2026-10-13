using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

public readonly record struct CameraShakeAxes(short X,short Y,short Z);
public readonly record struct CameraShakeDecay(byte X,byte Y,byte Z);

/// <summary>CCamera::Shake and the post-camera-AI quaternion/short-amplitude decay stage.</summary>
public sealed class CameraShakeState
{
    public CameraShakeAxes Amplitude {get;set;}
    public CameraShakeAxes Speed {get;set;}
    public CameraShakeDecay Decay {get;set;}
    public bool Active=>Amplitude.X!=0 || Amplitude.Y!=0 || Amplitude.Z!=0;
    public FixedQuaternion DisplayRotation {get;private set;}=FixedQuaternion.Identity;
    /// <summary>The original method does not read position or attenuate by distance.</summary>
    public void Shake(FixedVector3 position,int magnitude)
    {
        CameraShakeAxes amplitude=magnitude switch
        {
            0=>new(25,0,50),1=>new(0,0,30),2=>new(0,0,25),3=>new(0,0,16),
            _=>throw new ArgumentOutOfRangeException(nameof(magnitude),"Original EShakeType must be0..3.")
        };
        Amplitude=amplitude;Decay=new(3,0,3);Speed=new(600,0,600);
    }
    /// <summary>Pure display evaluation, preserving the source quaternion product order and each Q12 intermediate.</summary>
    public FixedQuaternion Evaluate(FixedQuaternion baseline,uint totalTime)
    {
        if(!Active)return baseline;
        var qz=Axis(Angle(Amplitude.Z,Speed.Z,totalTime),2);
        var qx=Axis(Angle(Amplitude.X,Speed.X,totalTime),0);
        var qy=Axis(Angle(Amplitude.Y,Speed.Y,totalTime),1);
        return Multiply(Multiply(Multiply(qy,baseline),qx),qz);
    }
    /// <summary>One source CameraAI update. Decay is not multiplied by elapsed ticks.</summary>
    public FixedQuaternion Advance(FixedQuaternion baseline,uint totalTime)
    {
        DisplayRotation=Evaluate(baseline,totalTime);
        if(Active)Amplitude=new(Decrease(Amplitude.X,Decay.X),Decrease(Amplitude.Y,Decay.Y),Decrease(Amplitude.Z,Decay.Z));
        return DisplayRotation;
    }
    private static int Angle(short amplitude,short speed,uint time)=>
        unchecked(amplitude*GameAngle.Heading(unchecked((int)time*speed)).Sine)/4096;
    private static FixedQuaternion Axis(int angle,int axis)
    {
        var heading=GameAngle.Heading(angle>>1);
        return axis switch {0=>new(heading.Sine,0,0,heading.Cosine),1=>new(0,heading.Sine,0,heading.Cosine),_=>new(0,0,heading.Sine,heading.Cosine)};
    }
    private static short Decrease(short old,byte decay)
    {
        short value=unchecked((short)(old<0?old+decay:old-decay));
        return unchecked((short)(old^value))<0?(short)0:value;
    }
    private static FixedQuaternion Multiply(FixedQuaternion a,FixedQuaternion b)=>new(
        unchecked(b.X*a.W+b.Y*a.Z+b.W*a.X-b.Z*a.Y)>>12,
        unchecked(b.Y*a.W+b.W*a.Y+b.Z*a.X-b.X*a.Z)>>12,
        unchecked(b.Z*a.W+b.W*a.Z+b.X*a.Y-b.Y*a.X)>>12,
        unchecked(b.W*a.W-b.Y*a.Y-b.X*a.X-b.Z*a.Z)>>12);
}
