using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.LizMan;

public readonly record struct LizManJumpCandidate(int NodeId, FixedVector3 Position);

/// <summary>CLizMan::ScanNearbyNodesForJumpTarget (0x44F190) distance filter; occupancy stays with the scene host.</summary>
public static class LizManJumpScan
{
    public static int Select(FixedVector3 self, FixedVector3 player, IReadOnlyList<LizManJumpCandidate> candidates,
        Func<FixedVector3, bool> isOccupied)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(isOccupied);
        int bestDist = ApproximateDistance(self, player) - 0x100;
        if (bestDist <= 0) return 0;
        int result = 0;
        foreach (LizManJumpCandidate candidate in candidates)
        {
            int d = ApproximateDistance(candidate.Position, self);
            if (d < 0x100 || d > 0xC00) continue;
            int distToPlayer = ApproximateDistance(candidate.Position, player);
            if (distToPlayer >= bestDist) continue;
            if (isOccupied(candidate.Position)) continue;
            result = candidate.NodeId;
            bestDist = distToPlayer;
        }
        return result;
    }

    /// <summary>Utils_CrapDist-style three-axis approximate distance used by the original Scan.</summary>
    public static int ApproximateDistance(FixedVector3 first, FixedVector3 second)
    {
        int largest = Abs(unchecked(first.X - second.X));
        int middle = Abs(unchecked(first.Y - second.Y));
        int smallest = Abs(unchecked(first.Z - second.Z));
        if (largest < middle) (largest, middle) = (middle, largest);
        if (middle < smallest) (middle, smallest) = (smallest, middle);
        if (largest < middle) (largest, middle) = (middle, largest);
        return unchecked(largest + (middle >> 1) + (smallest >> 2)) >> 12;
    }

    private static int Abs(int value) => value < 0 ? unchecked(-value) : value;
}
