using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Audio;
using OpenSpidey.Core.Audio;
using OpenSpidey.Game.Audio;

namespace OpenSpidey.Game.Frontend;

/// <summary>SFX_Play through the spidey/menu banks and Redbook_XAPlay through the converted XA clips; both run while the tree is paused.</summary>
public partial class FrontendAudio : Node
{
    private PkrArchive archive = null!;
    public GodotSoundChannels Sounds { get; private set; } = null!;
    public GodotSpeechPlayer? Speech { get; private set; }
    public int PlayedSounds { get; private set; }
    public int LastSound { get; private set; } = -1;

    public static FrontendAudio Create(PkrArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        var audio = new FrontendAudio { Name = "FrontendAudio", ProcessMode = ProcessModeEnum.Always, archive = archive };
        audio.Sounds = new GodotSoundChannels { Name = "ShellSounds", Archive = archive, LevelGroup = SoundCatalog.Find("menu"),
            ProcessMode = ProcessModeEnum.Always };
        audio.AddChild(audio.Sounds);
        if (System.Environment.GetEnvironmentVariable("OPENSPIDEY_MEDIA") is { Length: > 0 } media)
        {
            audio.Speech = new GodotSpeechPlayer { Name = "ShellXa", CacheDirectory = media, ProcessMode = ProcessModeEnum.Always };
            audio.AddChild(audio.Speech);
        }
        return audio;
    }

    public override void _Ready()
    {
        Sounds.Controller.PlayerBank = SoundDefinitionBank.Read(archive.Read("data/spidey.sfx"));
        Sounds.Controller.LevelBank = SoundDefinitionBank.Read(archive.Read("data/menu.sfx"), levelSpecific: true);
    }

    public void Play(int sound, int volume = 0x2000)
    {
        Sounds.Controller.Play((uint)sound, (short)volume);
        PlayedSounds++;
        LastSound = sound;
    }

    private GodotSpeechPlayer RequireSpeech() => Speech ??
        throw new InvalidOperationException("Redbook XA playback needs the converted clips: run tools/modern.py with --assets so OPENSPIDEY_MEDIA is prepared.");

    /// <summary>Redbook_XAPlay 0x479EE0: ignored unless the priority beats the playing clip's; the volume is gGameState[13].</summary>
    public bool XaPlay(int bank, int slot, int priority, short voiceLevel)
    {
        GodotSpeechPlayer speech = RequireSpeech();
        speech.Volume = (byte)Math.Clamp((int)voiceLevel, 0, 255);
        int completions = speech.NaturalCompletions;
        if (!speech.Play(bank, slot, priority)) return false;
        completionsAtPlay = completions;
        return true;
    }

    private int completionsAtPlay;
    public void XaStop() => RequireSpeech().Controller.Stop();
    public void XaPause(bool paused) => Speech?.Controller.Pause(paused);

    /// <summary>Redbook_XAStat 4: the clip started by the last XaPlay ran to its end.</summary>
    public bool XaFinished => RequireSpeech().NaturalCompletions != completionsAtPlay;
}
