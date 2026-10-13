using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public readonly record struct GlowSection(uint Radius, uint PadBgr);
public readonly record struct GlowFringe(uint Width, uint CodeBgr);

public interface IGlowShape
{
    FixedVector3 Position { get; }
    IReadOnlyList<GlowSection> Sections { get; }
    IReadOnlyList<GlowFringe> Fringes { get; }
    uint CentreCodeBgr { get; }
    short StepAngle { get; }
    ushort Angle { get; }
    uint Mask { get; }
    bool SkipTriangles { get; }
    int FringeCount { get; }
}

/// <summary>Original CGlow radial geometry and setters, verified against the PC executable.</summary>
public sealed class GlowState : IGlowShape
{
    private readonly GlowSection[] sections;
    private readonly GlowFringe[] fringes;
    public FixedVector3 Position { get; set; }
    public IReadOnlyList<GlowSection> Sections => sections;
    public IReadOnlyList<GlowFringe> Fringes => fringes;
    public uint CentreCodeBgr { get; set; } = 0x32000000;
    public short StepAngle { get; }
    public ushort Angle { get; set; }
    public uint Mask { get; set; } = uint.MaxValue;
    public bool SkipTriangles { get; set; }
    public bool Protected { get; set; }
    public int FringeCount { get; }

    public GlowState(int points, int fringeCount)
    {
        if (points <= 0) throw new ArgumentOutOfRangeException(nameof(points));
        if (fringeCount < 0) throw new ArgumentOutOfRangeException(nameof(fringeCount));
        sections = new GlowSection[checked(points * 2)];
        fringes = new GlowFringe[checked(sections.Length * fringeCount)];
        Array.Fill(fringes, new(0, 0x3A000000));
        StepAngle = unchecked((short)(4096 / sections.Length));
        FringeCount = fringeCount;
    }

    public GlowState(FixedVector3 position, int radius, int fringeWidth, uint centreRgb, uint sectionRgb) : this(4, 1)
    {
        Position = position;
        SetRadius(radius);
        CentreCodeBgr = 0x32000000 | (centreRgb & 0xFFFFFF);
        for (int i = 0; i < sections.Length; i++) sections[i] = sections[i] with { PadBgr = sectionRgb & 0xFFFFFF };
        SetFringeWidth(0, unchecked((uint)fringeWidth));
    }

    public void SetRadius(int radius)
    {
        for (int i = 0; i < sections.Length; i++) sections[i] = sections[i] with { Radius = unchecked((uint)radius) };
    }

    public void SetCentreRgb(byte red, byte green, byte blue) => CentreCodeBgr = 0x32000000 | Rgb(red, green, blue);

    public void SetRgb(byte red, byte green, byte blue)
    {
        uint colour = Rgb(red, green, blue);
        for (int i = 0; i < sections.Length; i++) sections[i] = sections[i] with { PadBgr = colour };
    }

    public void SetFringeWidth(int fringe, uint width)
    {
        ValidateFringe(fringe);
        int start = checked(fringe * sections.Length);
        for (int i = start; i < start + sections.Length; i++) fringes[i] = fringes[i] with { Width = width };
    }

    public void SetFringeRgb(int fringe, byte red, byte green, byte blue)
    {
        ValidateFringe(fringe);
        int start = checked(fringe * sections.Length);
        uint colour = 0x3A000000 | Rgb(red, green, blue);
        for (int i = start; i < start + sections.Length; i++) fringes[i] = fringes[i] with { CodeBgr = colour };
    }

    private void ValidateFringe(int fringe)
    {
        if ((uint)fringe >= (uint)FringeCount) throw new ArgumentOutOfRangeException(nameof(fringe));
    }
    private static uint Rgb(byte red, byte green, byte blue) => (uint)(red | green << 8 | blue << 16);
}
