using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Original CSplat: static surface quad with delayed tint fading and source transparency flags.</summary>
public sealed class WebSurfaceSplat
{
    public const uint TextureChecksum = 0x8C21C2C7;
    public const string TextureName = "WebBall_Crater_01";
    public IReadOnlyList<FixedVector3> Corners { get; }
    public byte Mode { get; private set; }
    public short Age { get; private set; }
    public uint Tint { get; private set; } = 0x161616;
    public bool Alive { get; private set; } = true;
    public int Type => 38;

    public WebSurfaceSplat(FixedVector3 position, ActorAngles normal)
    {
        var direction = new FixedVector3(normal.X, normal.Y, normal.Z);
        int x = Math.Abs((int)normal.X), y = Math.Abs((int)normal.Y), z = Math.Abs((int)normal.Z);
        FixedVector3 perpendicular = x > y || x > z
            ? y > x || y > z ? new(-normal.Y, normal.X, 0) : new(normal.Z, 0, -normal.X)
            : new(0, -normal.Z, normal.Y);
        // Utils_CalcPerps uses source vector cross inputs and Q12 output.
        var other = new FixedVector3(unchecked(direction.Y * perpendicular.Z - direction.Z * perpendicular.Y) >> 12,
            unchecked(direction.Z * perpendicular.X - direction.X * perpendicular.Z) >> 12,
            unchecked(direction.X * perpendicular.Y - direction.Y * perpendicular.X) >> 12);
        perpendicular *= 50;
        other *= 50;
        Corners = Array.AsReadOnly(new[] { position - other - perpendicular, position + other - perpendicular,
            position - other + perpendicular, position + other + perpendicular });
    }

    public void AdvanceBits()
    {
        if (!Alive) return;
        switch (Mode)
        {
            case 0: Mode = 1; break;
            case 1: Age = unchecked((short)(Age + 1)); if (Age > 30) Mode = 2; break;
            case 2:
                int red = Math.Max((byte)Tint - 3, 0), green = Math.Max((byte)(Tint >> 8) - 3, 0), blue = Math.Max((byte)(Tint >> 16) - 3, 0);
                Tint = (Tint & 0xFF000000u) | (uint)(red | green << 8 | blue << 16);
                if ((Tint & 0xFFFFFF) == 0) Alive = false;
                break;
        }
    }
}
