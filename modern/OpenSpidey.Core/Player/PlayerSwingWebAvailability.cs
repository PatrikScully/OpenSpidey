using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public sealed class PlayerSwingWebAvailabilityState
{
    public FixedVector3 TargetPosition { get; set; }
    public FixedVector3 PlaneNormal { get; set; }
    public FixedVector3 NearCentre { get; set; }
    public FixedVector3 FarCentre { get; set; }
    public byte FarFromWall { get; set; }
}

public readonly record struct PlayerSwingWebAvailabilityFrame(FixedVector3 PlayerPosition, FixedVector3 HitPosition,
    short NormalY, int Distance, uint FaceFlags, uint Action, bool Ceiling);

/// <summary>Actual CheckSwingWebAvailability geometry, including partial target writes on rejected high-angle hits.</summary>
public static class PlayerSwingWebAvailability
{
    public static bool Check(PlayerSwingWebAvailabilityState state, PlayerSwingWebAvailabilityFrame frame)
    {
        if (frame.NormalY > 3400 || frame.Distance <= 512 || frame.Distance >= 4096 ||
            (frame.FaceFlags & 0x40000) != 0 || frame.Ceiling || (frame.Action & 0x4000F) == 0) return false;
        state.TargetPosition = frame.HitPosition;
        FixedVector3 adjusted = frame.PlayerPosition;
        if ((frame.Action & 1) != 0)
        {
            FixedVector3 direction = (frame.HitPosition - frame.PlayerPosition) >> 12;
            direction = FixedVectorMath.Normalize(direction with { Y = 0 });
            adjusted = adjusted with { Y = unchecked(frame.PlayerPosition.Y - 2375680) };
            adjusted += direction * 320;
        }
        FixedVector3 line = frame.HitPosition - adjusted;
        if (frame.NormalY is >= -2600 and <= 3400) line += line / 32;
        int length = Length(line);
        // Preserve the original partial target write while avoiding its divide-by-zero on coincident/degenerate hits.
        if (length == 0) return false;
        int vertical = line.Y / length;
        line /= 2;
        int halfLength = length / 2;
        int absoluteVertical = vertical < 0 ? unchecked(-vertical) : vertical;
        if (absoluteVertical >= 2896) return false;
        FixedVector3 shifted = line >> 8;
        FixedVector3 plane = new(unchecked(shifted.Y * shifted.X) >> 8,
            unchecked(shifted.X * unchecked(-shifted.X) - shifted.Z * shifted.Z) >> 8,
            unchecked(shifted.Z * shifted.Y) >> 8);
        int normalLength = Length(plane);
        if (normalLength == 0) return false;
        state.PlaneNormal = plane / normalLength;
        int radius = unchecked(halfLength * 2);
        if (frame.Distance <= 3072)
        {
            state.NearCentre = adjusted + line + state.PlaneNormal * radius;
            state.FarFromWall = 0;
        }
        else
        {
            state.NearCentre = adjusted + line / 2 + state.PlaneNormal * radius;
            state.FarCentre = state.NearCentre + line;
            state.FarFromWall = 1;
        }
        return true;
    }
    private static int Length(FixedVector3 vector)
    {
        FixedVector3 whole = vector >> 12;
        return FixedVectorMath.OriginalSquareRoot(unchecked(whole.X * whole.X + whole.Y * whole.Y + whole.Z * whole.Z));
    }
}
