namespace OpenSpidey.Core.Audio;

public readonly record struct SpeechRequest(int Bank, int Slot, int Priority);
public readonly record struct PositionSpeechStart(int Bank, int Slot, uint Vblank);
public readonly record struct SpeechEnvironment(bool FileIoBusy = false, bool MovieActive = false, bool PostWaterEffect = false);

public interface ISpeechPlayback
{
    bool Finished { get; }
    void Play(SpeechClip clip);
    void Stop();
    void Pause(bool paused);
}

/// <summary>Recovered speech priorities, one pending request and thirty-vblank cooldown.</summary>
public sealed class SpeechController(ISpeechPlayback playback)
{
    public bool Busy { get; private set; }
    public bool Paused { get; private set; }
    public bool Allowed { get; set; } = true;
    public int Priority { get; private set; } = -1;
    public int Cooldown { get; private set; }
    public SpeechRequest? Pending { get; private set; }
    public SpeechRequest? Current { get; private set; }
    public PositionSpeechStart LastPositionStart { get; private set; }

    public bool Play(int bank, int slot, int priority, SpeechEnvironment environment = default)
    {
        SpeechClip? clip = SpeechCatalog.Get(bank, slot);
        if (clip is null || !clip.Available || priority <= Priority || environment.FileIoBusy || environment.MovieActive) return false;
        var request = new SpeechRequest(bank, slot, priority);
        if (Cooldown != 0)
        {
            if (Pending is null && request != default) Pending = request;
            return false;
        }
        // Redbook_XAPlay itself does not test Allowed; preserve the recovered caller-owned flag.
        playback.Play(clip);
        Priority = priority;
        Busy = true;
        Current = request;
        return true;
    }

    /// <summary>PC Redbook_XAPlayPos retains bank/slot and reads vblank after an accepted XAPlay; it does not spatialize the voice.</summary>
    public bool PlayAtPosition(int bank, int slot, int priority, Func<uint> readVblank, SpeechEnvironment environment = default)
    {
        ArgumentNullException.ThrowIfNull(readVblank);
        if (!Play(bank, slot, priority, environment)) return false;
        LastPositionStart = new(bank, slot, readVblank());
        return true;
    }

    public void UpdateVblank(SpeechEnvironment environment = default)
    {
        if (Busy)
        {
            if (playback.Finished) { Busy = false; Cooldown = 30; Priority = -1; }
            return;
        }
        if (environment.PostWaterEffect) return;
        if (Cooldown != 0)
        {
            if (--Cooldown != 0) return;
            if (Pending is { } pending) Play(pending.Bank, pending.Slot, pending.Priority, environment);
            Pending = null;
        }
        else Reset();
    }

    public void Stop()
    {
        playback.Stop();
        if (!Busy) Pending = null;
        Paused = false;
        Busy = false;
        Priority = -1;
        Cooldown = 30;
    }

    public void Pause(bool paused)
    {
        if (!Busy || paused == Paused) return;
        Paused = paused;
        playback.Pause(paused);
    }

    public void Reset()
    {
        Current = null; Pending = null; Priority = -1; Cooldown = 0;
        Busy = false; Paused = false; Allowed = true;
    }

    public static int BinkVolume(byte volume)
    {
        int attenuation = volume == 0 ? -999 : -(int)((256 - volume) * 0.7f);
        attenuation = Math.Max(attenuation, -179);
        return (int)(((float)attenuation + 179.0) / 179.0 * 49152.0);
    }
}
