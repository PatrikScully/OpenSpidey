namespace OpenSpidey.Core.Presentation;

/// <summary>Original SetFoggingParams, render-step transition and white reciprocal-depth vertex opacity.</summary>
public sealed class RenderFogState
{
    public uint FadeColour {get;set;}=0xFFFFFF;
    public int Start {get;private set;}
    public int End {get;private set;}
    public int RemainingSteps {get;private set;}
    public int StartRate {get;private set;}
    public int EndRate {get;private set;}
    public int RangeShift {get;private set;}
    public int StartCopy {get;private set;}
    public int EndCopy {get;private set;}
    public bool WhiteOpacityFade {get;private set;}
    public float PcStart {get;private set;}
    public float PcEnd {get;private set;}
    public uint PcColour {get;private set;}
    public RenderFogState()=>Set(0,6000,2048);
    public void Set(int duration,int minimum,uint range)
    {
        if(range==0 || (range&(range-1))!=0)throw new ArgumentOutOfRangeException(nameof(range),"Original fog range must be a nonzero power of two.");
        int currentStart,currentEnd;
        if(duration>0)
        {
            currentStart=Start;currentEnd=End;RemainingSteps=duration;
            StartRate=unchecked(minimum-Start)/duration;EndRate=unchecked(minimum+(int)range-End)/duration;
        }
        else {currentStart=minimum;currentEnd=unchecked(minimum+(int)range);RemainingSteps=0;Start=currentStart;End=currentEnd;}
        RangeShift=0;
        while(range<4096){range<<=1;RangeShift++;}
        while(range>4096){range>>=1;RangeShift--;}
        StartCopy=currentStart;EndCopy=currentEnd;
        ApplyPcColour(SwapRedBlue(FadeColour));
    }
    /// <summary>Once per M3d_RenderSetup, independent of simulation elapsed ticks.</summary>
    public void AdvanceRender()
    {
        if(RemainingSteps>0)
        {
            Start=unchecked(Start+StartRate);End=unchecked(End+EndRate);RemainingSteps--;
            // Retain the recovered transition packing, distinct from the initial SetFoggingParams swap.
            uint colour=FadeColour;
            uint packed=unchecked(((uint)(byte)(colour>>16)<<16)+((colour>>8)&255)+(colour&0xFF00FF00));
            ApplyPcColour(packed);
        }
        if(WhiteOpacityFade)
        {
            if(Start<10)Start=10;if(End<15)End=15;if(End<=Start)End=unchecked(Start+5);
        }
    }
    public byte VertexAlpha(byte alpha,float reciprocalDepth)
    {
        if(!WhiteOpacityFade || reciprocalDepth>=1f/Start || reciprocalDepth<=0)return alpha;
        float near=1f/End,scale=255f/(1f/Start-near);
        int remap=reciprocalDepth>near?(int)((reciprocalDepth-near)*scale):0;
        return unchecked((byte)((alpha*remap)>>8));
    }
    private void ApplyPcColour(uint packed)
    {
        WhiteOpacityFade=(packed&0xFFFFFF)==0xFFFFFF;
        float scale=WhiteOpacityFade?100f:0.98f;
        PcStart=(float)Start*scale;PcEnd=(float)End*scale;
        if((packed&0xFF000000)==0x80000000){packed&=0xFFFFFF;PcStart=PcEnd*0.88999999f;}
        PcColour=packed;
    }
    private static uint SwapRedBlue(uint colour)=>unchecked((colour&0xFF00FF00)+(byte)(colour>>16)+((colour&255)<<16));
}
