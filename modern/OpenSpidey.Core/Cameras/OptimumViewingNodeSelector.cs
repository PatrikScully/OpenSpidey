using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Core.Cameras;

public readonly record struct SourceViewingNode(int Id, ushort Kind, FixedVector3 Position);

public interface IOptimumViewingCameraHost : IVisibilityBoxHost
{
    IEnumerable<SourceViewingNode> SourceNodes { get; }
    bool CameraCollisionCheck { get; set; }
    bool CameraRayOccluded(FixedVector3 start, FixedVector3 end);
    void SetFixedCamera(FixedVector3 position, AimAngles angles);
}

/// <summary>Original two-point camera selection: source node order, signed yaw gates, occlusion penalties and successive winners.</summary>
public sealed class OptimumViewingNodeSelector(IOptimumViewingCameraHost host)
{
    public int Select(FixedVector3 first, FixedVector3 second)
    {
        ArgumentNullException.ThrowIfNull(host);
        var box = new SourceVisibilityBox(new(-int.MaxValue, -int.MaxValue, -int.MaxValue),
            new(int.MaxValue, int.MaxValue, int.MaxValue));
        foreach (SourceVisibilityList list in new[] { SourceVisibilityList.StaticEnvironment, SourceVisibilityList.Baddy })
            foreach (IVisibilityBoxItem item in host.SourceItems(list))
                if (item.RegionUsable && box.Selects(item.SourcePosition, inside: true)) item.SetVisible(true);

        FixedVector3 midpoint = first + ((second - first) >> 1);
        int bestNode = 0, bestScore = 0;
        foreach (SourceViewingNode node in host.SourceNodes)
        {
            if (node.Id < 1 || node.Kind != 13) continue;
            int distance = unchecked((int)ActorLifecycle.Distance(node.Position, midpoint));
            if (distance is < 512 or > 4096) continue;
            AimAngles aimMidpoint = FixedAim.Calculate(node.Position, midpoint);
            AimAngles aimFirst = FixedAim.Calculate(node.Position, first);
            // The PC branch compares the signed delta directly. Negative yaw differences are accepted.
            if (AngleDelta(aimMidpoint.Yaw, aimFirst.Yaw) > 341) continue;
            AimAngles aimSecond = FixedAim.Calculate(node.Position, second);
            if (AngleDelta(aimMidpoint.Yaw, aimSecond.Yaw) > 341) continue;
            int score = 2048 - Math.Abs(AngleDelta(aimFirst.Pitch, aimSecond.Pitch));
            host.CameraCollisionCheck = true;
            try
            {
                if (host.CameraRayOccluded(node.Position, first)) score -= 1024;
                if (host.CameraRayOccluded(node.Position, second)) score -= 1024;
            }
            finally { host.CameraCollisionCheck = false; }
            if (score <= bestScore) continue;
            bestNode = node.Id;
            bestScore = score;
            host.SetFixedCamera(node.Position, aimMidpoint);
        }
        return bestNode;
    }

    private static int AngleDelta(int first, int second)
    {
        int delta = (second & 4095) - (first & 4095);
        if (delta > 2048) delta -= 4096;
        if (delta < -2048) delta += 4096;
        return delta;
    }
}
