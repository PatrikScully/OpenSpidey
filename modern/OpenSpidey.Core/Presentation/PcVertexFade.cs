namespace OpenSpidey.Core.Presentation;

/// <summary>DCModel_RenderModel 0x4774CD/0x477F3D and DC_PSXModel_RenderModel 0x478FE0 reciprocal-depth vertex fade over D3D ARGB colours.</summary>
public static class PcVertexFade
{
    /// <summary>M3d_RenderSetup 0x4737AE: flt_550078=1/End, flt_55007C=1/Start, flt_550080=255/(far-near) at 53-bit precision.</summary>
    public static (float Near,float Far,float Scale) Limits(int start,int end)
    {
        float near=1f/end,far=1f/start;
        return (near,far,(float)(255.0/((double)far-near)));
    }
    /// <summary>0x4774F8: 0 &lt; rhw &lt; flt_55007C; NaN never fades.</summary>
    public static bool InZone(float rhw,float far)=>rhw<far && rhw>0f;
    /// <summary>__ftol((rhw-flt_550078)*flt_550080) under the MSVC 53-bit control word, else 0 at or below near.</summary>
    public static uint Factor(float rhw,float near,float scale)=>rhw>near?unchecked((uint)(long)(((double)rhw-near)*scale)):0u;
    /// <summary>k*(c&gt;&gt;8) supplies the alpha byte; the low 24 colour bits are kept.</summary>
    public static uint Merge(uint colour,uint factor)
    {
        uint blended=unchecked(factor*(colour>>8));
        return blended^(0xFFFFFFu&(colour^blended));
    }
    /// <summary>0x4774CD: byte_660FE1 or (dword_54D384, !gLowGraphics and any vertex in the zone) selects blend mode 1 for the whole model.</summary>
    public static bool ModelFaded(bool superItemFade,bool fogEnabled,bool lowGraphics,ReadOnlySpan<float> rhw,float far)
    {
        if(superItemFade)return true;
        if(!fogEnabled || lowGraphics)return false;
        foreach(float value in rhw)if(InZone(value,far))return true;
        return false;
    }
    /// <summary>0x477F3D: byte_660FE1 applies dword_550060 to every vertex, else a faded model remaps only in-zone vertices.</summary>
    public static void ApplyModel(Span<uint> colours,ReadOnlySpan<float> rhw,bool superItemFade,uint superItemFactor,bool modelFaded,float near,float far,float scale)
    {
        if(rhw.Length!=colours.Length)throw new ArgumentException("Every vertex needs a reciprocal depth.",nameof(rhw));
        if(superItemFade)
        {
            for(int index=0;index<colours.Length;index++)colours[index]=Merge(colours[index],superItemFactor);
            return;
        }
        if(!modelFaded)return;
        for(int index=0;index<colours.Length;index++)
            if(InZone(rhw[index],far))colours[index]=Merge(colours[index],Factor(rhw[index],near,scale));
    }
    /// <summary>0x478FE0: per-face remap of up to four (repeated) vertex slots; true selects blend mode 1 for that face.</summary>
    public static bool ApplyFace(Span<uint> colours,ReadOnlySpan<float> rhw,ReadOnlySpan<byte> corners,bool fogEnabled,bool lowGraphics,float near,float far,float scale)
    {
        if(corners.Length!=4)throw new ArgumentException("DC_PSXModel faces always walk four corner slots.",nameof(corners));
        if(!fogEnabled || lowGraphics)return false;
        bool faded=false;
        foreach(byte corner in corners)
        {
            if(!InZone(rhw[corner],far))continue;
            faded=true;
            colours[corner]=Merge(colours[corner],Factor(rhw[corner],near,scale));
        }
        return faded;
    }
}
