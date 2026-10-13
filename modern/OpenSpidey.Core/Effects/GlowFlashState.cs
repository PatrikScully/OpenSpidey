using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public readonly record struct GlowFlashParameters(FixedVector3 Position, int Points,
    byte CentreRed, byte CentreGreen, byte CentreBlue, int CentreFadeRate,
    byte SectionRed, byte SectionGreen, byte SectionBlue, int SectionFadeRate,
    int FringeWidth, int WidthDelta, int ReselectRadii, int OddRadius, int OddRange,
    int EvenRadius, int EvenRange, int RadiusDecay, int Lifetime)
{
    public static GlowFlashParameters FromSourceArguments(FixedVector3 position, IReadOnlyList<int> values)
    {
        if (values.Count != 18) throw new ArgumentException("CGlowFlash requires eighteen constructor arguments.", nameof(values));
        return new(position, values[0], unchecked((byte)values[1]), unchecked((byte)values[2]), unchecked((byte)values[3]), values[4],
            unchecked((byte)values[5]), unchecked((byte)values[6]), unchecked((byte)values[7]), values[8], values[9], values[10], values[11],
            values[12], values[13], values[14], values[15], values[16], values[17]);
    }
}

/// <summary>Recovered CGlowFlash construction, radius choices, per-section fade and lifetime.</summary>
public sealed class GlowFlashState : IGlowShape
{
    private readonly Func<int, int> random;
    private readonly GlowSection[] sections;
    private readonly GlowFringe[] fringes;
    public FixedVector3 Position { get; }
    public uint CentreCodeBgr { get; set; }
    public IReadOnlyList<GlowSection> Sections => sections;
    public IReadOnlyList<GlowFringe> Fringes => fringes;
    public bool SkipTriangles => false;
    public int FringeCount => 1;
    public short StepAngle { get; }
    public ushort Angle { get; set; }
    public uint Mask { get; set; } = uint.MaxValue;
    public short Age { get; set; }
    public ushort Lifetime { get; set; }
    public short CentreFadeRate { get; }
    public short SectionFadeRate { get; }
    public short WidthDelta { get; }
    public byte ReselectRadii { get; }
    public short OddRadius { get; private set; }
    public short OddRange { get; private set; }
    public short EvenRadius { get; private set; }
    public short EvenRange { get; private set; }
    public short RadiusDecay { get; }
    public bool IsDead { get; private set; }

    public GlowFlashState(GlowFlashParameters parameters, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (parameters.Points <= 0 || parameters.Points > int.MaxValue / 4)
            throw new ArgumentOutOfRangeException(nameof(parameters), "Source glow section allocation and step angle require a positive safe point count.");
        this.random = random;
        int count = checked(parameters.Points * 2);
        sections = new GlowSection[count];
        fringes = new GlowFringe[count];
        StepAngle = unchecked((short)(4096 / count));
        Position = parameters.Position;
        CentreCodeBgr = 0x32000000u | Rgb(parameters.CentreRed, parameters.CentreGreen, parameters.CentreBlue);
        uint sectionColour = Rgb(parameters.SectionRed, parameters.SectionGreen, parameters.SectionBlue);
        Array.Fill(sections, new(0, sectionColour));
        Array.Fill(fringes, new(unchecked((uint)parameters.FringeWidth), 0x3A000000));
        CentreFadeRate = unchecked((short)parameters.CentreFadeRate);
        SectionFadeRate = unchecked((short)parameters.SectionFadeRate);
        WidthDelta = unchecked((short)parameters.WidthDelta);
        ReselectRadii = unchecked((byte)parameters.ReselectRadii);
        OddRadius = unchecked((short)parameters.OddRadius);
        OddRange = unchecked((short)parameters.OddRange);
        EvenRadius = unchecked((short)parameters.EvenRadius);
        EvenRange = unchecked((short)parameters.EvenRange);
        RadiusDecay = unchecked((short)parameters.RadiusDecay);
        Lifetime = unchecked((ushort)parameters.Lifetime);
        ChooseRadii();
    }

    public void ChooseRadii()
    {
        for (int i = 0; i < sections.Length; i++)
        {
            int radius = (i & 1) != 0 ? OddRadius + random(OddRange) : EvenRadius + random(EvenRange);
            sections[i] = sections[i] with { Radius = unchecked((uint)radius) };
        }
        if (OddRadius >= RadiusDecay) OddRadius = unchecked((short)(OddRadius - RadiusDecay));
        if (OddRange >= RadiusDecay) OddRange = unchecked((short)(OddRange - RadiusDecay));
        if (EvenRadius >= RadiusDecay) EvenRadius = unchecked((short)(EvenRadius - RadiusDecay));
        if (EvenRange >= RadiusDecay) EvenRange = unchecked((short)(EvenRange - RadiusDecay));
    }

    public void Move()
    {
        for (int i = 0; i < sections.Length; i++)
        {
            sections[i] = sections[i] with { PadBgr = ReduceRgb(sections[i].PadBgr, SectionFadeRate) };
            // This reduction deliberately occurs once per section in the source.
            CentreCodeBgr = ReduceRgb(CentreCodeBgr, CentreFadeRate);
        }
        if (ReselectRadii != 0) ChooseRadii();
        if (Lifetime != 0)
        {
            Age = unchecked((short)(Age + 1));
            if (Age <= Lifetime) return;
        }
        else if ((CentreCodeBgr & 0xFFFFFF) != 0) return;
        IsDead = true;
    }

    private static uint Rgb(byte red, byte green, byte blue) => (uint)(red | green << 8 | blue << 16);
    private static uint ReduceRgb(uint colour, int amount)
    {
        byte red = (byte)(colour & 255), green = (byte)((colour >> 8) & 255), blue = (byte)((colour >> 16) & 255);
        red = red >= amount ? unchecked((byte)(red - amount)) : (byte)0;
        green = green >= amount ? unchecked((byte)(green - amount)) : (byte)0;
        blue = blue >= amount ? unchecked((byte)(blue - amount)) : (byte)0;
        return (colour & 0xFF000000) | Rgb(red, green, blue);
    }
}
