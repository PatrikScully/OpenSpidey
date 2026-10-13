using System.Text.Json;
using Godot;
using OpenSpidey.Core.Audio;

namespace OpenSpidey.Game.Audio;

/// <summary>Single original speech channel backed by native cross-platform WAV playback.</summary>
public partial class GodotSpeechPlayer : Node, ISpeechPlayback
{
    private readonly Dictionary<string, AudioStreamWav> streams = [];
    private HashSet<string> absentSourceMedia = new(StringComparer.OrdinalIgnoreCase);
    private bool finished = true;
    private bool disposed;
    public string CacheDirectory { get; init; } = "";
    public SpeechController Controller { get; private set; } = null!;
    public AudioStreamPlayer Player { get; private set; } = null!;
    public Func<SpeechEnvironment> Environment { get; set; } = () => default;
    public SpeechClip? CurrentClip { get; private set; }
    public byte Volume { get; set; } = 255;
    public int PlayCount { get; private set; }
    public int NaturalCompletions { get; private set; }
    public event Action<Exception>? Faulted;
    internal (ulong Id, string Source, RefCounted Owner)[] ReadOwnedResources() => streams
        .Select(entry => (entry.Value.GetInstanceId(), entry.Key + ": speech PCM", (RefCounted)entry.Value)).ToArray();
    bool ISpeechPlayback.Finished => finished;

    public override void _Ready()
    {
        ProcessPhysicsPriority = -10;
        Player = new AudioStreamPlayer { Name = "SpeechChannel" };
        AddChild(Player);
        Player.Finished += () => { finished = true; NaturalCompletions++; };
        Controller = new(this);
        string manifest = System.IO.Path.Combine(CacheDirectory, "voice", "missing.json");
        if (System.IO.File.Exists(manifest)) absentSourceMedia = new(
            JsonSerializer.Deserialize<string[]>(System.IO.File.ReadAllText(manifest)) ?? [], StringComparer.OrdinalIgnoreCase);
    }

    public bool Play(int bank, int slot, int priority) => Controller.Play(bank, slot, priority, Environment());
    public override void _PhysicsProcess(double delta)
    {
        try { Controller.UpdateVblank(Environment()); }
        catch (Exception exception)
        {
            SetPhysicsProcess(false);
            Controller.Stop();
            if (Faulted is null) GD.PushError(exception.ToString());
            else Faulted(exception);
        }
    }

    void ISpeechPlayback.Play(SpeechClip clip)
    {
        AudioStreamWav? stream = null;
        if (clip.HasMedia && !absentSourceMedia.Contains(clip.ArchivePath))
        {
            if (!streams.TryGetValue(clip.ConvertedName, out stream))
            {
                string path = System.IO.Path.Combine(CacheDirectory, clip.ConvertedName);
                if (!System.IO.File.Exists(path)) throw new FileNotFoundException("Prepare speech with tools/modern.py voices first.", path);
                stream = AudioStreamWav.LoadFromFile(path);
                if (stream is null || stream.GetLength() <= 0) throw new InvalidDataException($"Could not decode voice: {path}");
                streams.Add(clip.ConvertedName, stream);
            }
        }
        Player.Stop();
        Player.Stream = stream;
        Player.StreamPaused = false;
        Player.VolumeLinear = SpeechController.BinkVolume(Volume) / 32768f;
        CurrentClip = clip;
        PlayCount++;
        finished = stream is null;
        if (stream is not null) Player.Play();
    }

    void ISpeechPlayback.Stop() { Player.Stop(); finished = true; }
    void ISpeechPlayback.Pause(bool paused) => Player.StreamPaused = paused;

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        if (Player is not null && GodotObject.IsInstanceValid(Player))
        {
            Player.Stop();
            Player.Stream = null;
        }
        foreach (AudioStreamWav stream in streams.Values) stream.Dispose();
        streams.Clear();
    }
}
