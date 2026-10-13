using OpenSpidey.Core.Cinematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Assets.Scripting;

internal static class CinemaTests
{
    public static void Run(Action<bool, string> assert)
    {
        assert(CinemaCatalog.Movies.Count == 25 && CinemaCatalog.Cinemas.Count == 21,
            "All available PC movie tracks and story cinema dispatches are retained.");
        assert(CinemaCatalog.Cinemas[11] == new CinemaDefinition(11, 4, "L1Cov") && CinemaCatalog.GetMovie(4).Name == "L1M1",
            "First-level autoexec cinema uses the original cover and movie track.");
        assert(CinemaCatalog.Cinemas.Values.Select(c => c.MovieTrack).Order().SequenceEqual(Enumerable.Range(4, 21).Select(i => (byte)i)),
            "Story dispatch reaches every story movie once.");
        assert(MediaFrameLayout.Fit(1920, 1080, 320, 192) == new MediaRectangle(60, 0, 1800, 1080),
            "Widescreen playback preserves the movie's native 5:3 pixels and centered layout.");
        assert(MediaFrameLayout.Fit(640, 480, 320, 192) == new MediaRectangle(0, 48, 640, 384),
            "A 4:3 viewport retains the original movie aspect without stretching.");
        var backend = new Playback();
        var command = new RunCinemaCommandHandler(backend, restartedAfterDeath: true);
        var autoexec = new TriggerNode(0, 4, 0, new byte[] { 4, 0, 255, 255 });
        var instruction = new TriggerInstruction(190, 2, new byte[] { 11, 0 });
        command.Execute(new(autoexec, 0), instruction);
        assert(backend.Calls == 0 && !command.IsBlocking, "A death restart skips its first autoexec movie.");
        command.Execute(new(autoexec, 0), instruction);
        assert(backend.Calls == 1 && command.IsBlocking, "The death-restart skip is consumed once.");
    }

    private sealed class Playback : ICinemaPlayback
    {
        public bool IsPlaying { get; private set; }
        public int Calls { get; private set; }
        public void Play(CinemaDefinition cinema) { Calls++; IsPlaying = true; }
    }
}
