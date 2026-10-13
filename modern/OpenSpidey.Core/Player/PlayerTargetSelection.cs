using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public sealed record PlayerTargetCandidate(IPlayerMeleeTarget Target, ushort Radius, ushort BodyFlags, uint CachedPlayerDistance,
    uint BehaviorFlags, byte CanShowSenseIndicator);
public sealed record PlayerSwitchCandidate(object Identity, ushort Type);
/// <summary>Stable identity of the source switch interior point (+0x10C or +0x118), distinct from its owner.</summary>
public sealed record PlayerSwitchAimPoint(object Identity, FixedVector3 Position);
public readonly record struct PlayerSwitchTrace(object? Item, uint FaceFlags);

public interface IPlayerTargetSelectionHost
{
    IEnumerable<PlayerTargetCandidate> SourceBaddyOrder { get; }
    IEnumerable<PlayerSwitchCandidate> SourceControlOrder { get; }
    bool LineOfSight(FixedVector3 start, FixedVector3 end, int firstSourceArgument, int secondSourceArgument);
    PlayerSwitchAimPoint? GetSwitchAutoAimPoint(PlayerSwitchCandidate item);
    PlayerSwitchTrace LineToItem(FixedVector3 start, FixedVector3 end, int mode);
    PlayerKickHandle MakeSwitchHandle(PlayerSwitchCandidate? item);
}

/// <summary>Original PC enemy and switch scoring in the player's transposed source orientation (field89C).</summary>
public static class PlayerTargetSelection
{
    public static IPlayerMeleeTarget? SelectBaddy(FixedVector3 position, FixedJointTransform targetingRotation,
        IPlayerTargetSelectionHost host, int maximumDistance, int coneThreshold, int distanceWeight, int coneWeight)
    {
        IPlayerMeleeTarget? best = null;
        int bestScore = 0;
        foreach (PlayerTargetCandidate candidate in host.SourceBaddyOrder)
        {
            if (candidate.Radius == 0 || (candidate.BodyFlags & 0x40) != 0 || (candidate.BodyFlags & 0x10) == 0) continue;
            // Original PC uses JGE/IDIV even though the recovered CBody header calls this word u32.
            int distance = unchecked((int)candidate.CachedPlayerDistance);
            if (distance >= maximumDistance) continue;
            int score = DistanceScore(distance, maximumDistance, distanceWeight);
            if (coneWeight != 0)
            {
                FixedVector3 direction = FacingDirection(position, candidate.Target.Position, targetingRotation);
                if (unchecked(-direction.Z) < coneThreshold) continue;
                score = unchecked(score + ((coneWeight * (unchecked(4096 - direction.Z) / 2)) >> 12));
            }
            if (score > bestScore && host.LineOfSight(position, candidate.Target.Position, 0, 0))
            {
                bestScore = score;
                best = candidate.Target;
            }
        }
        return best;
    }

    public static PlayerKickSwitchSelection SelectSwitch(FixedVector3 position, FixedJointTransform targetingRotation,
        IPlayerTargetSelectionHost host, int maximumDistance, int minimumFacing, int distanceWeight, int facingWeight)
    {
        PlayerSwitchCandidate? best = null;
        PlayerSwitchAimPoint? bestPoint = null;
        int bestScore = 0;
        foreach (PlayerSwitchCandidate candidate in host.SourceControlOrder)
        {
            if (candidate.Type != 407) continue;
            PlayerSwitchAimPoint? target = host.GetSwitchAutoAimPoint(candidate);
            if (target is null) continue;
            FixedVector3 point = target.Position;
            int distance = Distance(position, point);
            if (distance >= maximumDistance) continue;
            int score = DistanceScore(distance, maximumDistance, distanceWeight);
            if (facingWeight != 0)
            {
                FixedVector3 direction = FacingDirection(position, point, targetingRotation);
                if (unchecked(-direction.Z) < minimumFacing) continue;
                score = unchecked(score + ((facingWeight * (unchecked(4096 - direction.Z) / 2)) >> 12));
            }
            if (score <= bestScore) continue;
            PlayerSwitchTrace trace = host.LineToItem(position, point, 1);
            if (trace.Item is null || (trace.FaceFlags & 0x02000000) != 0)
            {
                bestScore = score;
                best = candidate;
                bestPoint = target;
            }
        }
        return new(host.MakeSwitchHandle(best), bestPoint?.Position);
    }

    public static PlayerSwitchAimPoint? GetSwitchAutoAimPoint(int mode, PlayerSwitchAimPoint first, PlayerSwitchAimPoint second) =>
        mode switch { 1 => first, >= 2 and <= 5 => second, _ => null };

    private static int DistanceScore(int distance, int maximumDistance, int weight) =>
        unchecked((weight * (unchecked((maximumDistance - distance) << 12) / maximumDistance)) >> 12);

    internal static FixedVector3 Rotate(FixedVector3 point, FixedJointTransform rotation) => FixedRotation.Transform(rotation, point);

    internal static FixedVector3 FacingDirection(FixedVector3 position, FixedVector3 target, FixedJointTransform rotation)
    {
        FixedVector3 delta = new(unchecked(target.X - position.X) >> 12,
            unchecked(target.Y - position.Y) >> 12, unchecked(target.Z - position.Z) >> 12);
        FixedVector3 direction = Rotate(delta, rotation) with { Y = 0 };
        return FixedVectorMath.Normalize(direction);
    }

    internal static int Distance(FixedVector3 first, FixedVector3 second)
    {
        unchecked
        {
            int largest = GameWord.Absolute(first.X - second.X), middle = GameWord.Absolute(first.Y - second.Y), smallest = GameWord.Absolute(first.Z - second.Z);
            if (largest < middle) (largest, middle) = (middle, largest);
            if (middle < smallest) (middle, smallest) = (smallest, middle);
            if (largest < middle) (largest, middle) = (middle, largest);
            return (largest + (middle >> 1) + (smallest >> 2)) >> 12;
        }
    }
}
