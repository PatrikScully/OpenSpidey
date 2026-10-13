namespace OpenSpidey.Core.Audio;

public enum FightIntroPlaylist { None, First, Second }

/// <summary>Actual music globals60D86C..60D890 and54D868, retained independently from the current CPlayer.</summary>
public sealed class SourceMusicState
{
    public int TrackClock { get; set; }
    public int TrackIndex { get; set; }
    public byte TrackEnding { get; set; }
    public int FightIntroClock { get; set; }
    public int FightIntroIndex { get; set; }
    public int Phase { get; set; }
    public uint LastVblank { get; set; }
    public FightIntroPlaylist SelectedIntro { get; set; }
    public int FightVariant { get; set; }
    public byte IntroCompleted { get; set; } = 1;
}

public interface ISourceMusicPlayback
{
    uint Play(uint sound, short volume, int pitchOffset);
    void SetVoiceVolume(uint handle, short volume);
}
