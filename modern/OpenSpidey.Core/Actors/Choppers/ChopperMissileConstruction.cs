using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Core.Actors.Choppers;

/// <summary>CChopperMissile constructor fields inlined in CChopper::FireMissileAtWaypoint (0x4259E0).</summary>
public static class ChopperMissileConstruction
{
    public static int AdvanceMissileLaunchIndex(int current) => current == 9 ? 2 : current + 1;

    /// <summary>Inlined link walk before field_104 assignment (not the out-of-line GetFinalTargetNode helper).</summary>
    public static int ResolveField104(TriggerFile triggers, int waypoint)
    {
        if ((uint)waypoint >= triggers.Nodes.Count) throw new InvalidDataException($"Chopper missile references missing node {waypoint}.");
        int resolved = waypoint;
        IReadOnlyList<ushort> links = MissionRuntime.ReadLinks(triggers.Nodes[waypoint]);
        if (links.Count == 0) return 0;
        while (true)
        {
            int next = links[0];
            if ((uint)next >= triggers.Nodes.Count) throw new InvalidDataException($"Chopper missile link references missing node {next}.");
            if (triggers.Nodes[next].TypeCode != 1002) break;
            resolved = next;
            links = MissionRuntime.ReadLinks(triggers.Nodes[next]);
            if (links.Count == 0) return 0;
        }
        return resolved;
    }

    public static ChopperMissileState Create(FixedVector3 spawnPosition, int waypointNode, int phase, int difficulty, TriggerFile triggers)
    {
        FixedVector3 target = MissionWaypointPosition.Read(triggers, waypointNode);
        return new()
        {
            Position = spawnPosition,
            TargetPosition = target,
            WaypointNode = waypointNode,
            FinalTargetNode = ResolveField104(triggers, waypointNode),
            Phase = phase,
            DelayTicks = difficulty == 3 ? 20 : 0
        };
    }

    public static FixedVector3 WaypointPosition(TriggerFile triggers, int node) => MissionWaypointPosition.Read(triggers, node);
}

internal static class MissionWaypointPosition
{
    internal static FixedVector3 Read(TriggerFile triggers, int node)
    {
        if ((uint)node >= triggers.Nodes.Count) throw new InvalidDataException($"Mission node {node} is missing.");
        TriggerPosition position = TriggerPlacement.Read(triggers.Nodes[node]).Position;
        return new FixedVector3(position.X, position.Y, position.Z) << 12;
    }
}
