namespace OpenSpidey.Core.Gameplay;

/// <summary>Shared original panel countdown, also used by level scripts and timed training.</summary>
public sealed class MissionCountdown
{
    public byte DisplayEnabled { get; set; }
    public byte Running { get; set; }
    public uint RemainingTicks { get; set; }
    public uint Rate { get; set; }
    public uint LastUpdateTick { get; set; }

    /// <summary>Panel_UpdateTimer/Panel_Display update once at the owning source clock; scripted player control pauses decay.</summary>
    public void Update(uint sourceClock, bool scriptedPlayerControl)
    {
        if (Running != 0)
        {
            uint decay = scriptedPlayerControl ? 0 : unchecked(Rate * (sourceClock - LastUpdateTick)) >> 12;
            RemainingTicks = RemainingTicks > decay ? RemainingTicks - decay : 0;
        }
        LastUpdateTick = sourceClock;
    }

    public void Stop(uint sourceClock)
    {
        DisplayEnabled = 0; Running = 0; LastUpdateTick = sourceClock;
    }
}
