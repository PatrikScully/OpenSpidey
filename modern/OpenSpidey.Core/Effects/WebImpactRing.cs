using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Effects;

/// <summary>Original CImpactRing; growth and fading run once per global Bit_Move.</summary>
public sealed class WebImpactRing
{
    public FixedVector3 Position { get; }
    public short Scale { get; private set; }
    public short Angle => 0;
    public int TargetScale { get; }
    public int Growth { get; }
    public int ColourDecay { get; }
    public uint CodeRgb { get; private set; }
    public uint PostScale => 0x0A001000;
    public ushort FrigDeltaZ => 100;
    public int AnimationIndex => 9;
    public int Frame => 1;
    public bool Alive { get; private set; } = true;

    public WebImpactRing(ImpactWebRingRequest request)
    {
        Position = request.Position;
        TargetScale = request.Size;
        Growth = request.Growth;
        if (Growth == 0) throw new ArgumentOutOfRangeException(nameof(request), "Original ring color decay divides by growth.");
        int frames = TargetScale / Growth;
        if (unchecked(Growth * frames) < TargetScale) frames++;
        if (frames == 0) throw new ArgumentOutOfRangeException(nameof(request), "Original ring color decay divides by its frame count.");
        ColourDecay = Math.Max(request.Red, Math.Max(request.Green, request.Blue)) / frames;
        CodeRgb = 0x2E000000u | (uint)(request.Red | request.Green << 8 | request.Blue << 16);
    }

    public void AdvanceBits()
    {
        if (!Alive) return;
        Scale = unchecked((short)(Scale + unchecked((short)Growth)));
        if (Scale > TargetScale) Alive = false;
        int red = (byte)CodeRgb, green = (byte)(CodeRgb >> 8), blue = (byte)(CodeRgb >> 16);
        red = red >= ColourDecay ? unchecked((byte)(red - ColourDecay)) : 0;
        green = green >= ColourDecay ? unchecked((byte)(green - ColourDecay)) : 0;
        blue = blue >= ColourDecay ? unchecked((byte)(blue - ColourDecay)) : 0;
        CodeRgb = (CodeRgb & 0xFF000000u) | (uint)(red | green << 8 | blue << 16);
    }
}
