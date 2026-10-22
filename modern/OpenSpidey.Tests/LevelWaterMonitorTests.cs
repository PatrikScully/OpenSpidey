using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;

internal static class LevelWaterMonitorTests
{
    public static void Run(Action<bool, string> assert)
    {
        var host = new Host();
        LevelWaterMonitor.MonitorL6A2(host);
        assert(host.DrownTimer == 0 && !host.SceneWater.PlayerInWaterVolume,
            "Above the l6a2 surface clears the drown timer and water-volume flag.");
        host.PlayerPosition = new FixedVector3(0, 6801 << 12, 0);
        LevelWaterMonitor.MonitorL6A2(host);
        assert(host.SceneWater.PlayerInWaterVolume && host.SceneWater.SceneWaterLevelY == 6800 << 12 && host.DrownTimer == 2,
            "Below the authored surface arms the monitor and accumulates elapsed ticks.");
    }

    private sealed class Host : ILevelWaterMonitorHost
    {
        public FixedVector3 PlayerPosition { get; set; }
        public int PlayerElapsedTicks => 2;
        public CampaignSceneWaterState SceneWater { get; } = new();
        public int DrownTimer { get; set; }
        public TriggerFile Triggers { get; } = new([new TriggerNode(0, 255, 0, ReadOnlyMemory<byte>.Empty)]);
        public void SendDrownPulse(ushort node) => throw new NotSupportedException();
        public void TeleportPlayer(FixedVector3 position) => PlayerPosition = position;
        public int ObtainPoolWaterLevelL6A1(int poolIndex) => 3000;
        public int ObtainPoolWaterLevelL5A6(int poolIndex) => 3000;
        public int ObtainPoolWaterLevelL5A7() => 3000;
    }
}
