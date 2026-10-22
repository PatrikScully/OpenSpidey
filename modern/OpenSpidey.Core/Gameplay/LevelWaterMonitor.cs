using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Gameplay;

public interface ILevelWaterMonitorHost
{
    FixedVector3 PlayerPosition { get; }
    int PlayerElapsedTicks { get; }
    CampaignSceneWaterState SceneWater { get; }
    int DrownTimer { get; set; }
    TriggerFile Triggers { get; }
    void SendDrownPulse(ushort node);
    void TeleportPlayer(FixedVector3 position);
    int ObtainPoolWaterLevelL6A1(int poolIndex);
    int ObtainPoolWaterLevelL5A6(int poolIndex);
    int ObtainPoolWaterLevelL5A7();
}

/// <summary>Authored l5a6lsc / l5a7lsc / l6a1lsc / l6a2lsc / l6a3lsc MonitorSpideyinWater logic from the recovered PC level scripts.</summary>
public static class LevelWaterMonitor
{
    private static readonly FixedVector3 L6A1DrowningPosition = new(unchecked((int)0xFEAB8000), 0x532000, unchecked((int)0xFE330000));
    private static readonly FixedVector3 L6A2DrowningPosition = new(-22315008, 0x532000, -30212096);

    public static void MonitorL5A6(ILevelWaterMonitorHost host)
    {
        CampaignSceneWaterState water = host.SceneWater;
        water.PlayerInWaterVolume = false;
        FixedVector3 position = host.PlayerPosition;
        int x = position.X >> 12;
        int y = position.Y >> 12;
        int z = position.Z >> 12;

        if (x > -1140 && x < 2000 && z > 4000 && z < 4640)
        {
            if (y <= 2700 || y >= 3400) { host.DrownTimer = 0; return; }
            if (!EnterPoolL5A6(host, y, 0)) return;
            AccumulateDrown(host, L6A1DrowningPosition, graceTicks: 20);
            return;
        }

        if (x > 2000 && x < 5900 && z > 4000 && z < 4640)
        {
            if (y <= 2700 || y >= 3400) { host.DrownTimer = 0; return; }
            if (!EnterPoolL5A6(host, y, 1)) return;
            AccumulateDrown(host, L6A1DrowningPosition, graceTicks: 20);
            return;
        }

        if (x > -360 && x < 1600)
        {
            if (z > 6800 && z < 10900)
            {
                if (y <= 1800 || y >= 3700) { host.DrownTimer = 0; return; }
                if (!EnterPoolL5A6(host, y, 2)) return;
                AccumulateDrown(host, L6A1DrowningPosition, graceTicks: 20);
                return;
            }

            if (x < 1600 && z > -2100 && z < 2000)
            {
                if (y <= 1800 || y >= 3700) { host.DrownTimer = 0; return; }
                if (!EnterPoolL5A6(host, y, 3)) return;
                AccumulateDrown(host, L6A1DrowningPosition, graceTicks: 20);
                return;
            }
        }

        host.DrownTimer = 0;
    }

    public static void MonitorL5A7(ILevelWaterMonitorHost host)
    {
        CampaignSceneWaterState water = host.SceneWater;
        water.PlayerInWaterVolume = false;
        FixedVector3 position = host.PlayerPosition;
        int x = position.X >> 12;
        int y = position.Y >> 12;
        int z = position.Z >> 12;
        if (x <= -3600 || x >= -900 || z <= 3300 || z >= 5000 || y <= 2700 || y >= 4700)
        {
            host.DrownTimer = 0;
            return;
        }

        water.PlayerInWaterVolume = true;
        int waterLevel = host.ObtainPoolWaterLevelL5A7();
        water.SceneWaterLevelY = waterLevel << 12;
        if (y <= waterLevel)
        {
            host.DrownTimer = 0;
            return;
        }

        AccumulateDrown(host, L6A1DrowningPosition, graceTicks: 20);
    }

    public static void MonitorL6A1(ILevelWaterMonitorHost host)
    {
        CampaignSceneWaterState water = host.SceneWater;
        water.PlayerInWaterVolume = false;
        FixedVector3 position = host.PlayerPosition;
        int x = position.X >> 12;
        int y = position.Y >> 12;
        int z = position.Z >> 12;

        if (x > -29500 && x < -12250 && z > 1100 && z < 1700)
        {
            if (y > 2460 && y < 4000)
            {
                if (!EnterPool(host, y, 0)) return;
                AccumulateDrown(host, L6A1DrowningPosition, graceTicks: 20);
                return;
            }
            host.DrownTimer = 0;
            return;
        }

        if (x > -40000 && x < -32000 && z > -800 && z < 3600)
        {
            if (y > 2500 && y < 6000)
            {
                if (!EnterPool(host, y, 1)) return;
                AccumulateDrown(host, L6A1DrowningPosition, graceTicks: 20);
                return;
            }
            host.DrownTimer = 0;
            return;
        }

        host.DrownTimer = 0;
    }

    public static void MonitorL6A2(ILevelWaterMonitorHost host) => MonitorSimpleDepth(host, surfaceY: 6800, graceTicks: 10, L6A2DrowningPosition);
    public static void MonitorL6A3(ILevelWaterMonitorHost host) => MonitorSimpleDepth(host, surfaceY: 6800, graceTicks: 10, L6A2DrowningPosition);

    private static bool EnterPool(ILevelWaterMonitorHost host, int playerY, int poolIndex)
    {
        host.SceneWater.PlayerInWaterVolume = true;
        int waterLevel = host.ObtainPoolWaterLevelL6A1(poolIndex);
        host.SceneWater.SceneWaterLevelY = waterLevel << 12;
        if (playerY <= waterLevel)
        {
            host.DrownTimer = 0;
            return false;
        }
        return true;
    }

    private static bool EnterPoolL5A6(ILevelWaterMonitorHost host, int playerY, int poolIndex)
    {
        host.SceneWater.PlayerInWaterVolume = true;
        int waterLevel = host.ObtainPoolWaterLevelL5A6(poolIndex);
        host.SceneWater.SceneWaterLevelY = waterLevel << 12;
        if (playerY <= waterLevel)
        {
            host.DrownTimer = 0;
            return false;
        }
        return true;
    }

    private static void MonitorSimpleDepth(ILevelWaterMonitorHost host, int surfaceY, int graceTicks, FixedVector3 drowningPosition)
    {
        CampaignSceneWaterState water = host.SceneWater;
        water.PlayerInWaterVolume = false;
        int y = host.PlayerPosition.Y >> 12;
        if (y <= surfaceY)
        {
            host.DrownTimer = 0;
            return;
        }

        water.PlayerInWaterVolume = true;
        water.SceneWaterLevelY = surfaceY << 12;
        AccumulateDrown(host, drowningPosition, graceTicks);
    }

    private static void AccumulateDrown(ILevelWaterMonitorHost host, FixedVector3 drowningPosition, int graceTicks)
    {
        host.DrownTimer = unchecked(host.DrownTimer + host.PlayerElapsedTicks);
        if (host.DrownTimer <= graceTicks) return;

        if (TryFindDrowningNode(host, drowningPosition, out ushort node))
        {
            host.DrownTimer = 0;
            host.SendDrownPulse(node);
            host.TeleportPlayer(drowningPosition);
            return;
        }

        throw new InvalidOperationException("No TRG_Drowning node");
    }

    private static bool TryFindDrowningNode(ILevelWaterMonitorHost host, FixedVector3 expected, out ushort nodeId)
    {
        TriggerFile triggers = host.Triggers;
        for (int index = 1; index < triggers.Nodes.Count; index++)
        {
            TriggerNode node = triggers.Nodes[index];
            if (node.TypeCode != (ushort)TriggerNodeKind.Object) continue;
            TriggerPosition placement = TriggerPlacement.Read(node).Position;
            FixedVector3 position = new FixedVector3(placement.X, placement.Y, placement.Z) << 12;
            if (position == expected)
            {
                nodeId = node.Id;
                return true;
            }
        }

        nodeId = 0;
        return false;
    }
}
