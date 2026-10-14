using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.LizMan;

/// <summary>CLizMan::CalculateJumpPositionArray (0x44F2E0): 48-step sine arc into field_3B4.</summary>
public static class LizManJump
{
    public static FixedVector3[] Calculate(FixedVector3 origin, short surfaceClearance, FixedVector3 target, int groundHeight)
    {
        int xDelta = unchecked((target.X - origin.X) / 48);
        int yDelta = unchecked((groundHeight - (surfaceClearance << 12) - origin.Y) / 48);
        int zDelta = unchecked((target.Z - origin.Z) / 48);
        var points = new FixedVector3[48];
        int x = unchecked(origin.X + xDelta);
        int y = unchecked(origin.Y + yDelta);
        int z = unchecked(origin.Z + zDelta);
        int t = 0;
        for (int i = 0; t < 0x810; i++, t += 0x2B)
        {
            points[i] = new(x, unchecked(y - (GameAngle.Heading(t & 0xFFF).Sine << 8)), z);
            x = unchecked(x + xDelta);
            y = unchecked(y + yDelta);
            z = unchecked(z + zDelta);
        }
        return points;
    }
}
