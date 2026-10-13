using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Recovered surface-oriented splat quad, integer growth and delayed tint fade.</summary>
public sealed class WebImpactSplat
{
    public const uint TextureChecksum = 0x3AF6DFF3;
    private readonly FixedVector3 center;
    private readonly FixedVector3 widthAxis;
    private readonly FixedVector3 heightAxis;
    private readonly FixedVector3[] corners = new FixedVector3[4];
    public IReadOnlyList<FixedVector3> Corners { get; }
    public int Scale { get; private set; }
    public int Age { get; private set; }
    public uint ColourTint { get; private set; } = 0x808080;
    public byte Tint => (byte)ColourTint;
    public bool Finished => (ColourTint & 0xFFFFFF) == 0;

    public WebImpactSplat(FixedVector3 position, FixedVector3 normal, int roll)
    {
        center = position + normal * 10;
        normal = new(unchecked((short)normal.X), unchecked((short)normal.Y), unchecked((short)normal.Z));
        FixedVector3 perpendicular;
        int x = Math.Abs(normal.X), y = Math.Abs(normal.Y), z = Math.Abs(normal.Z);
        if (x > y || x > z)
            perpendicular = y > x || y > z ? new(-normal.Y, normal.X, 0) : new(normal.Z, 0, -normal.X);
        else perpendicular = new(0, -normal.Z, normal.Y);
        var cross = new FixedVector3(unchecked(normal.Y * perpendicular.Z - normal.Z * perpendicular.Y) >> 12,
            unchecked(normal.Z * perpendicular.X - normal.X * perpendicular.Z) >> 12,
            unchecked(normal.X * perpendicular.Y - normal.Y * perpendicular.X) >> 12);
        (short sine, short cosine) = GameAngle.Heading(roll);
        widthAxis = (cross * sine + perpendicular * cosine) >> 12;
        heightAxis = (cross * cosine + perpendicular * -sine) >> 12;
        Corners = Array.AsReadOnly(corners);
        Step();
    }

    public void Step()
    {
        if (Finished) return;
        Scale = unchecked(Scale + ((32 - Scale) >> 1));
        FixedVector3 a = widthAxis * Scale, b = heightAxis * Scale;
        corners[0] = center - a - b;
        corners[1] = center + a - b;
        corners[2] = center - a + b;
        corners[3] = center + a + b;
        Age = unchecked((short)(Age + 1));
        if (Age > 30)
        {
            int red = Math.Max((byte)ColourTint - 5, 0);
            int green = Math.Max((byte)(ColourTint >> 8) - 5, 0);
            int blue = Math.Max((byte)(ColourTint >> 16) - 5, 0);
            ColourTint = (ColourTint & 0xFF000000u) | (uint)(red | green << 8 | blue << 16);
        }
    }

    public void SetTint(byte red, byte green, byte blue) => ColourTint = (uint)(red | green << 8 | blue << 16);
}
