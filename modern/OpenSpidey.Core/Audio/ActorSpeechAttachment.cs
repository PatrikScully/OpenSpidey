namespace OpenSpidey.Core.Audio;

/// <summary>Recovered CBody speech ownership; recent matching bank/slot playback may be stopped by its actor.</summary>
public sealed class ActorSpeechAttachment
{
    public uint AttachedClock { get; private set; }
    public int Bank { get; private set; }
    public int Slot { get; private set; }

    public void Attach(uint clock, int bank, int slot)
    {
        AttachedClock = clock;
        Bank = bank;
        Slot = slot;
    }

    public bool CanStop(uint clock, SpeechRequest? current) =>
        unchecked(clock - AttachedClock) < 300 && current is { } request && request.Bank == Bank && request.Slot == Slot;
}
