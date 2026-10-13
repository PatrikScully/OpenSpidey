namespace OpenSpidey.Core.Cinematics;

public interface ICinemaPlayback
{
    bool IsPlaying { get; }
    void Play(CinemaDefinition cinema);
}
