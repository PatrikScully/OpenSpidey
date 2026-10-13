using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Original CPlayer::CalculateTugWebPathPoints shrinking eight-point throw arc.</summary>
public static class PlayerTugWebPath
{
    public static IReadOnlyList<FixedVector3>? Calculate(FixedVector3 player, FixedVector3 target)
    {
        int vertical = unchecked(target.Y - player.Y);
        if (vertical < 0) vertical = unchecked(-vertical);
        if (vertical > 0x80000) return null;
        FixedVector3 direction = target - player;
        FixedVector3 whole = direction >> 12;
        int length = FixedVectorMath.OriginalSquareRoot(unchecked(whole.X * whole.X + whole.Y * whole.Y + whole.Z * whole.Z));
        direction = FixedVectorMath.Normalize(whole with { Y = 0 });
        int step = unchecked(length - 256) / 7;
        var points = new FixedVector3[8];
        for (int index = 0; index < points.Length; index++)
        {
            int angle = index * 256;
            var heading = GameAngle.Heading(angle);
            int radius = unchecked(heading.Cosine * length) >> 12;
            points[index] = new(unchecked(player.X + radius * direction.X),
                unchecked(player.Y - (heading.Sine << 8)), unchecked(player.Z + radius * direction.Z));
            if (angle < 1024) length = unchecked(length - step);
        }
        return points;
    }
}
