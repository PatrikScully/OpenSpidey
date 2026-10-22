using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Gameplay;

/// <summary>Training module globals: gTrainingStuff 0x6B36E8 (slot0 is the level ID) and gTrainingFlags 0x6B378C.</summary>
public sealed class TrainingState
{
    public int[] Stuff { get; } = new int[8];
    public byte[] Flags { get; } = new byte[3];
    /// <summary>Byte 0x60CFC6, cleared by speed training and raised by its finish line.</summary>
    public byte TimeAttackComplete { get; set; }
}

/// <summary>Shell-owned training selection and result: gTrainingSeconds 0x551288 and the score 0x55129C, with their static image values.</summary>
public sealed class TrainingResults
{
    public int Seconds { get; set; } = -1;
    public int Score { get; set; } = -1000;
}

public interface ITrainingMonitorHost
{
    bool PlayerPresent { get; }
    /// <summary>CPlayer+0xE2.</summary>
    short PlayerHealth { get; }
    /// <summary>CPlayer+0xE18.</summary>
    int PlayerFieldE18 { get; }
    /// <summary>CPlayer+0x568, read by target practice.</summary>
    int PlayerField568 { get; }
    /// <summary>CPlayer+0x5D0, read by item hunt and zip-line training.</summary>
    int PlayerField5D0 { get; }
    uint Clock { get; }
    /// <summary>CPlayer::NotifyKill call count 0x60CFBC.</summary>
    int KillCount { get; }
    /// <summary>gSaveGame+0x80.</summary>
    uint CostumeUnlockFlags { get; set; }
    /// <summary>Panel Y anchor 0x60F76C.</summary>
    int ScreenOffsetY { get; }
    int NodeCount { get; }
    ushort NodeKind(int node);
    FixedVector3 NodePosition(int node);
    void SendPulse(int node);
    void SetTextScale(int scale);
    void SetTextJustification(byte justification);
    void DrawText(int x, int y, ReadOnlyMemory<byte> text, int argument, uint depth);
    /// <summary>PShell_EndTrainingInit 0x47B720.</summary>
    void EndTraining();
}

/// <summary>Whole original training module: init 0x4DDC60, clear 0x4DDCC0, SpideyDied 0x4DDCE0 and MonitorLevel 0x4DDCF0.</summary>
public static class TrainingMonitor
{
    public const string ModuleName = "training";

    public static void Initialize(TrainingState state, MissionCountdown countdown, PlayerModuleHook playerHook, int levelId)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(countdown); ArgumentNullException.ThrowIfNull(playerHook);
        playerHook.Set(ModuleName, 1);
        state.Stuff[0] = levelId;
        Array.Clear(state.Stuff, 1, 7);
        countdown.Rate = 4096;
        state.Flags[2] = state.Flags[1] = state.Flags[0] = 0;
    }

    public static void Clear(MissionCountdown countdown, uint clock)
    {
        ArgumentNullException.ThrowIfNull(countdown);
        countdown.Running = 0; countdown.LastUpdateTick = clock; countdown.DisplayEnabled = 0;
    }

    public static void SpideyDied(TrainingState state) => state.Flags[0] = 1;

    public static void Monitor(TrainingState state, TrainingResults results, MissionCountdown countdown, ITrainingMonitorHost host)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(countdown); ArgumentNullException.ThrowIfNull(host);
        int[] stuff = state.Stuff;
        uint level = unchecked((uint)stuff[0]);
        if (level > 0x1303)
        {
            if (level == 0x1501) TargetPractice(state, results, countdown, host);
            else if (level == 0x1601) ItemHunt(state, results, countdown, host);
            else if (level == 0x1701) ZipLine(state, results, countdown, host);
            return;
        }
        if (level >= 0x1301) { SpeedTraining(state, results, countdown, host); return; }
        if (level is >= 0x1101 and <= 0x1104 or >= 0x1201 and <= 0x1204) { Survival(state, results, countdown, host); return; }
        if (level is >= 0x901 and <= 0x904) TimeAttack(state, results, countdown, host);
    }

    private static void End(MissionCountdown countdown, ITrainingMonitorHost host)
    {
        countdown.Running = 0; countdown.LastUpdateTick = host.Clock; countdown.DisplayEnabled = 0;
        host.EndTraining();
    }

    private static void StartTimedCountdown(TrainingState state, MissionCountdown countdown, uint remaining, uint clock)
    {
        countdown.DisplayEnabled = 1; countdown.Running = 1;
        countdown.RemainingTicks = remaining; countdown.LastUpdateTick = clock;
        state.Stuff[1] = 1;
    }

    private static void TimeAttack(TrainingState state, TrainingResults results, MissionCountdown countdown, ITrainingMonitorHost host)
    {
        if (host.PlayerPresent && host.PlayerHealth <= 0)
        {
            results.Score = host.KillCount; End(countdown, host);
        }
        else if (state.Stuff[1] != 0)
        {
            if (countdown.RemainingTicks == 0) { results.Score = host.KillCount; End(countdown, host); }
        }
        else StartTimedCountdown(state, countdown, unchecked((uint)(60 * results.Seconds)), host.Clock);
        int kills = host.KillCount;
        byte[] text = "Enemies killed: 00\0"u8.ToArray();
        text[16] = unchecked((byte)(kills / 10 + 48));
        text[17] = unchecked((byte)(kills % 10 + 48));
        host.SetTextJustification(0);
        host.DrawText(256, 220, Terminated(text), 0, 0x1000);
    }

    private static void Survival(TrainingState state, TrainingResults results, MissionCountdown countdown, ITrainingMonitorHost host)
    {
        int[] stuff = state.Stuff;
        if (stuff[1] != 0)
        {
            if (host.PlayerPresent)
            {
                if (host.PlayerHealth <= 0) { Finish(); return; }
                if (host.PlayerFieldE18 == 0) countdown.RemainingTicks = unchecked(host.Clock - (uint)stuff[1]);
            }
        }
        else
        {
            state.Flags[0] = 0; countdown.RemainingTicks = 0; countdown.DisplayEnabled = 1;
            if (!host.PlayerPresent) return;
            if (host.PlayerFieldE18 == 0)
            {
                stuff[1] = unchecked((int)host.Clock); countdown.Running = 0; countdown.LastUpdateTick = host.Clock;
            }
            if (host.PlayerHealth <= 0) { Finish(); return; }
        }
        if (state.Flags[0] != 0) Finish();

        void Finish() { results.Score = unchecked((int)countdown.RemainingTicks); End(countdown, host); }
    }

    private static void SpeedTraining(TrainingState state, TrainingResults results, MissionCountdown countdown, ITrainingMonitorHost host)
    {
        int[] stuff = state.Stuff;
        int started = stuff[1];
        if (stuff[1] != 0)
        {
            if (!host.PlayerPresent) { Died(); return; }
            if (host.PlayerHealth > 0 && host.PlayerFieldE18 == 0)
            {
                countdown.RemainingTicks = unchecked(host.Clock - (uint)stuff[1]);
                if (unchecked(host.Clock - (uint)stuff[1]) > 0x8CA0) { results.Score = -1000; End(countdown, host); return; }
            }
        }
        else
        {
            countdown.RemainingTicks = 0; countdown.DisplayEnabled = 1; countdown.Running = 0;
            countdown.LastUpdateTick = host.Clock; state.Flags[0] = 0; state.TimeAttackComplete = 0;
            if (!host.PlayerPresent) { Died(); return; }
            if (host.PlayerFieldE18 == 0) { started = unchecked((int)host.Clock); stuff[1] = started; }
        }
        if (started != 0 && state.TimeAttackComplete != 0)
        {
            results.Score = unchecked((int)countdown.RemainingTicks); End(countdown, host);
        }
        if (host.PlayerPresent && host.PlayerHealth <= 0) { results.Score = -1000; End(countdown, host); return; }
        Died();

        void Died()
        {
            if (state.Flags[0] == 0) return;
            results.Score = -1000; End(countdown, host);
        }
    }

    private static void TargetPractice(TrainingState state, TrainingResults results, MissionCountdown countdown, ITrainingMonitorHost host)
    {
        int score = host.PlayerPresent ? results.Score = host.PlayerField568 : results.Score;
        if (state.Stuff[1] != 0)
        {
            if (state.Stuff[1] == 1 && countdown.RemainingTicks == 0) { End(countdown, host); return; }
        }
        else if (host.PlayerPresent && host.PlayerFieldE18 == 0) StartTimedCountdown(state, countdown, 3600, host.Clock);
        byte[] text = "Score:  00\0"u8.ToArray();
        text[7] = (byte)(score < 0 ? '-' : ' ');
        int magnitude = score == int.MinValue ? score : Math.Abs(score);
        text[8] = unchecked((byte)(magnitude / 10 + 48));
        text[9] = unchecked((byte)(magnitude % 10 + 48));
        host.SetTextScale(256); host.SetTextJustification(1);
        host.DrawText(348, unchecked(host.ScreenOffsetY + 36), Terminated(text), 0, 0x1000);
    }

    private static void ItemHunt(TrainingState state, TrainingResults results, MissionCountdown countdown, ITrainingMonitorHost host)
    {
        int[] stuff = state.Stuff;
        uint elapsed;
        if (stuff[1] != 0) countdown.RemainingTicks = elapsed = unchecked(host.Clock - (uint)stuff[1]);
        else
        {
            elapsed = 0; countdown.DisplayEnabled = 1; countdown.RemainingTicks = 0; countdown.Running = 0;
            countdown.LastUpdateTick = host.Clock;
            if (host.PlayerPresent && host.PlayerFieldE18 == 0) stuff[1] = unchecked((int)host.Clock);
        }
        bool timedOut = false;
        if (host.PlayerPresent)
        {
            if (host.PlayerField5D0 >= 40 && stuff[2] == 0)
            {
                stuff[2] = 1; stuff[3] = unchecked((int)host.Clock); results.Score = unchecked((int)elapsed);
            }
            if (host.PlayerHealth <= 0 && stuff[2] == 0)
            {
                results.Score = -1000; stuff[2] = 1; stuff[3] = unchecked((int)host.Clock); timedOut = true;
            }
        }
        if (timedOut || stuff[2] == 1)
        {
            if (unchecked(host.Clock - (uint)stuff[3]) > 0xF)
            {
                End(countdown, host);
                elapsed = countdown.RemainingTicks;
            }
        }
        // Original 0x4DE4F5 dereferences the player without a null check.
        if (!host.PlayerPresent) throw new InvalidOperationException("Original item-hunt training dereferences a missing player.");
        if (host.PlayerField5D0 >= 40 || elapsed < 0x4650) return;
        results.Score = -1000; End(countdown, host);
    }

    private static void ZipLine(TrainingState state, TrainingResults results, MissionCountdown countdown, ITrainingMonitorHost host)
    {
        int[] stuff = state.Stuff;
        if (stuff[1] != 0)
        {
            if (stuff[1] == 1 && countdown.RemainingTicks == 0)
            {
                results.Score = RequirePlayer().PlayerField5D0; End(countdown, host); return;
            }
        }
        else if (host.PlayerPresent && host.PlayerFieldE18 == 0)
            StartTimedCountdown(state, countdown, unchecked((uint)(60 * results.Seconds)), host.Clock);
        if (RequirePlayer().PlayerField5D0 > 7500 && (host.CostumeUnlockFlags & 0x100) == 0)
        {
            for (int node = 1; node < host.NodeCount; node++)
            {
                if (host.NodeKind(node) != 1) continue;
                if (host.NodePosition(node) != default) continue;
                host.SendPulse(node);
                break;
            }
            host.CostumeUnlockFlags |= 0x100;
        }
        int score = RequirePlayer().PlayerField5D0;
        byte[] text = "Score: 00000\0"u8.ToArray();
        text[7] = unchecked((byte)(score / 10000 + 48)); score %= 10000;
        text[8] = unchecked((byte)(score / 1000 + 48)); score %= 1000;
        text[9] = unchecked((byte)(score / 100 + 48));
        text[10] = unchecked((byte)(score % 100 / 10 + 48));
        text[11] = unchecked((byte)(score % 100 % 10 + 48));
        if (text[7] == '0')
        {
            int index = 7;
            do text[index++] = (byte)' ';
            while (index != 11 && text[index] == '0');
        }
        host.SetTextScale(256); host.SetTextJustification(1);
        host.DrawText(348, unchecked(host.ScreenOffsetY + 36), Terminated(text), 0, 0x1000);
        if (!host.PlayerPresent || host.PlayerHealth > 0 || stuff[2] != 0)
        {
            if (stuff[2] != 1) return;
        }
        else { stuff[2] = 1; stuff[3] = unchecked((int)host.Clock); }
        if (unchecked(host.Clock - (uint)stuff[3]) > 0x1E)
        {
            results.Score = RequirePlayer().PlayerField5D0; End(countdown, host);
        }

        ITrainingMonitorHost RequirePlayer() => host.PlayerPresent ? host :
            throw new InvalidOperationException("Original zip-line training dereferences a missing player.");
    }

    private static ReadOnlyMemory<byte> Terminated(byte[] text) => text.AsMemory(0, Array.IndexOf(text, (byte)0));
}
