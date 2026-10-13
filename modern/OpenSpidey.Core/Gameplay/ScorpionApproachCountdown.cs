using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Gameplay;

/// <summary>Persistent original l2a1lsc warning flags; shared panel countdown belongs to MissionCountdown.</summary>
public sealed class ScorpionApproachState
{
    public byte[] PrimaryWarnings { get; } = new byte[5];
    public byte[] SecondaryWarnings { get; } = new byte[5];
}

public interface IScorpionApproachCountdownHost
{
    int Difficulty { get; }
    uint Clock { get; }
    int NodeCount { get; }
    ushort NodeKind(int node);
    FixedVector3 NodePosition(int node);
    void SendPulse(int node);
    void ReportMissingFailureNode();
}

/// <summary>Whole original L2A1LSC_MonitorTimer4476B0 over genuine panel globals and ascending authored actor-node search.</summary>
public static class ScorpionApproachCountdown
{
    private static readonly uint[] durations = [0x3F84, 0x3F84, 0x3174, 0x2364];
    public static uint Duration(int difficulty) => (uint)difficulty < durations.Length ? durations[difficulty] :
        throw new ArgumentOutOfRangeException(nameof(difficulty));

    public static void Clear(MissionCountdown countdown, uint clock, Action<int> releaseTexture)
    {
        ArgumentNullException.ThrowIfNull(countdown); ArgumentNullException.ThrowIfNull(releaseTexture);
        countdown.DisplayEnabled = countdown.Running = 0;
        countdown.LastUpdateTick = clock;
        for (int texture = 0; texture < 5; texture++) releaseTexture(texture);
    }

    public static void Monitor(MissionCountdown countdown, ScorpionApproachState state, IScorpionApproachCountdownHost host)
    {
        ArgumentNullException.ThrowIfNull(countdown); ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(host);
        countdown.Rate = 4096;
        if (countdown.Running == 0)
        {
            countdown.DisplayEnabled = countdown.Running = 1;
            countdown.RemainingTicks = Duration(host.Difficulty);
            countdown.LastUpdateTick = host.Clock;
            Array.Clear(state.PrimaryWarnings); Array.Clear(state.SecondaryWarnings);
        }
        if (countdown.RemainingTicks != 0 || state.PrimaryWarnings[4] != 0) return;
        state.PrimaryWarnings[4] = 1;
        for (int node = 1; node < host.NodeCount; node++)
        {
            if (host.NodeKind(node) != 1) continue;
            FixedVector3 position = host.NodePosition(node);
            if (position.X >> 12 != -419 || position.Y >> 12 != -2730 || position.Z >> 12 != -3826) continue;
            host.SendPulse(node);
            return;
        }
        host.ReportMissingFailureNode();
    }
}
