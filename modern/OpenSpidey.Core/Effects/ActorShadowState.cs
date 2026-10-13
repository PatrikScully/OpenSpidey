using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>CBody::UpdateShadow's protected CQuadBit, with the original fixed downward normal.</summary>
public sealed class ActorShadowState
{
    public bool Exists { get; private set; }
    public FixedVector3[] Corners { get; } = new FixedVector3[4];
    public byte Transparency { get; private set; }
    public uint Tint => (uint)(Transparency * 0x10101);
    public uint CodeBgr => 0x1C0341;
    public ushort FrigDeltaZ => 32;
    public bool Protected => true;
    public void Kill() => Exists = false;
    public void Update(uint bodyFlags, FixedVector3 position, ushort scale, short distance, short threshold)
    {
        if ((bodyFlags & 8) == 0) { Kill(); return; }
        if (threshold == 0) throw new DivideByZeroException("Original CBody shadow threshold cannot be zero.");
        Exists = true;
        int radius = scale << 12;
        // Utils_CalcPerps(0,-4096,0) produces perp1=(4096,0,0), perp2=(0,0,-4096).
        Corners[0] = position + new FixedVector3(-radius, 0, radius);
        Corners[1] = position + new FixedVector3(radius, 0, radius);
        Corners[2] = position + new FixedVector3(-radius, 0, -radius);
        Corners[3] = position + new FixedVector3(radius, 0, -radius);
        int transparency = unchecked((threshold - distance) << 7) / threshold;
        Transparency = unchecked((byte)Math.Max(0, transparency));
    }
    /// <summary>Original DisplayQuadBitList ignores code's subtraction selector and selects mix/opaque-alpha.</summary>
    public int PcBlendMode => 1;
    public byte PcAlpha => 255;
    public byte PcVertexChannel => Transparency == 0 ? (byte)0 : checked((byte)((Transparency >> 1) + 15));
}
