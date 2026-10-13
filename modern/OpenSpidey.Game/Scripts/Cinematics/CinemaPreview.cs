using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Cinematics;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Game.Cinematics;

/// <summary>Runs the developing cinema adapter independently of unported mission actors.</summary>
public partial class CinemaPreview : Node
{
    private GodotCinemaPlayer cinema = null!;
    private MissionRuntime mission = null!;
    private bool smoke;
    private bool finishNaturally;
    private bool checkedEarlySkip;
    private bool checkedFrame;
    private bool captured;
    private double elapsed;
    private int completions;
    private bool verifyGameplayPause;
    private CinemaGameplayProbe? gameplayProbe;

    public override void _Ready()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            smoke = args.Contains("--smoke-cinema");
            finishNaturally = args.Contains("--finish-cinema");
            verifyGameplayPause = args.Contains("--pause-gameplay");
            if (verifyGameplayPause)
            {
                ProcessMode = ProcessModeEnum.Always;
                gameplayProbe = new() { ProcessMode = ProcessModeEnum.Pausable };
                AddChild(gameplayProbe);
            }
            ushort id = ushort.Parse(System.Environment.GetEnvironmentVariable("OPENSPIDEY_CINEMA") ?? "11",
                System.Globalization.CultureInfo.InvariantCulture);
            if (!CinemaCatalog.Cinemas.ContainsKey(id)) throw new ArgumentException($"Cinema {id} does not exist.");
            cinema = new GodotCinemaPlayer
            {
                CacheDirectory = System.Environment.GetEnvironmentVariable("OPENSPIDEY_MEDIA")
                    ?? throw new InvalidOperationException("Prepare and select the media cache with tools/modern.py cinema."),
                PauseGameplay = verifyGameplayPause
            };
            AddChild(cinema);
            cinema.Completed += () => completions++;
            byte[] data = [(byte)TriggerNodeKind.Autoexec, 0, (byte)MissionOpcode.RunCinema, 0,
                (byte)id, (byte)(id >> 8), 255, 255];
            var node = new TriggerNode(0, (ushort)TriggerNodeKind.Autoexec, 0, data);
            mission = new(new([node]), new NoActors(), [new RunCinemaCommandHandler(cinema)]);
            mission.QueueAutoexec();
            if (mission.ExecutePending() != 0 || !mission.IsBlocked || mission.PendingCount != 1)
                throw new InvalidDataException("The cinema command did not suspend its event.");
            if (verifyGameplayPause && !GetTree().Paused)
                throw new InvalidDataException("The actual cinema command did not pause gameplay.");
            GD.Print($"Playing original cinema {id}: {CinemaCatalog.GetMovie(CinemaCatalog.Cinemas[id].MovieTrack).Name}, 64-bit Godot.");
        }
        catch (Exception exception) { Fail(exception); }
    }

    public override void _Process(double delta)
    {
        if (mission is null) return;
        try
        {
            elapsed += delta;
            if (verifyGameplayPause && (gameplayProbe!.PhysicsUpdates != 0 || (cinema.IsPlaying && !GetTree().Paused)))
                throw new InvalidDataException("Gameplay advanced during the actual story cinema pause.");
            if (smoke && elapsed > (finishNaturally ? 240 : 20)) throw new TimeoutException("Cinema playback did not complete.");
            if (smoke && cinema.ShowingCover)
            {
                cinema.RequestSkip();
                if (cinema.ShowingCover || !cinema.IsPlaying) throw new InvalidDataException("Cover advance did not start the movie.");
            }
            if (smoke && !checkedEarlySkip && !cinema.ShowingCover)
            {
                cinema.RequestSkip();
                if (!cinema.IsPlaying) throw new InvalidDataException("A newly started movie accepted a skip before its polling guard.");
                checkedEarlySkip = true;
            }
            if (smoke && !checkedFrame && cinema.MoviePosition >= 2.2)
            {
                VerifyVideoFrame();
                checkedFrame = true;
                string? capture = System.Environment.GetEnvironmentVariable("OPENSPIDEY_CAPTURE");
                if (!string.IsNullOrWhiteSpace(capture)) { Capture(capture); return; }
                captured = true;
            }
            if (smoke && checkedFrame && captured && !finishNaturally && cinema.IsPlaying) cinema.RequestSkip();
            int completed = mission.ExecutePending();
            if (mission.PendingCount != 0) return;
            if (completed != 1 || completions != 1 || cinema.IsPlaying || (smoke && (!checkedEarlySkip || !checkedFrame)))
                throw new InvalidDataException("Cinema completion did not resume and finish its event exactly once.");
            if (smoke)
                GD.Print($"Verified cinema playback: decoded video texture, aspect layout, skip guard, " +
                    $"{(finishNaturally ? "natural finished signal" : "permitted skip")}, one completion and ordered mission resumption.");
            else GD.Print("Cinema completed; its mission event resumed.");
            if (verifyGameplayPause)
            {
                SetProcess(false);
                VerifyGameplayResumption();
                return;
            }
            GetTree().Quit();
            SetProcess(false);
        }
        catch (Exception exception) { Fail(exception); }
    }

    private void VerifyVideoFrame()
    {
        Texture2D texture = cinema.Video.GetVideoTexture();
        if (texture.GetWidth() != 320 || texture.GetHeight() != 192)
            throw new InvalidDataException("The story movie did not decode its native dimensions.");
        if (DisplayServer.GetName() != "headless")
        {
            using Image image = texture.GetImage();
            byte[] pixels = image.GetData();
            if (image.IsEmpty() || pixels.Distinct().Take(3).Count() < 3)
                throw new InvalidDataException("The video texture has no decoded image.");
        }
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        MediaRectangle expected = MediaFrameLayout.Fit(viewport.X, viewport.Y, 320, 192);
        if (cinema.Video.Size.DistanceTo(new(expected.Width, expected.Height)) > 0.01f ||
            cinema.Video.Position.DistanceTo(new(expected.X, expected.Y)) > 0.01f)
            throw new InvalidDataException("Native video controls did not follow the viewport's aspect layout.");
    }

    private async void Capture(string path)
    {
        SetProcess(false);
        cinema.Video.Paused = true;
        try
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using Image image = GetViewport().GetTexture().GetImage();
            Error error = image.SavePng(path);
            if (error != Error.Ok) throw new IOException($"Could not save cinema capture: {error}");
            GD.Print($"Saved cinema capture: {path}");
            captured = true;
            cinema.Video.Paused = false;
            SetProcess(true);
        }
        catch (Exception exception) { Fail(exception); }
    }

    private void Fail(Exception exception)
    {
        GD.PushError(exception.ToString());
        GetTree().Quit(1);
        SetProcess(false);
    }

    private async void VerifyGameplayResumption()
    {
        try
        {
            if (GetTree().Paused) throw new InvalidDataException("Finished cinema did not restore gameplay processing.");
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (gameplayProbe!.PhysicsUpdates == 0)
                throw new InvalidDataException("Gameplay physics did not resume after the cinema.");
            // Destruction must restore the pause state owned before this second sequence.
            GetTree().Paused = true;
            cinema.Play(CinemaCatalog.Cinemas[11]);
            var holder = new Node { Name = "CinemaReparentOwner" };
            AddChild(holder);
            ulong videoIdentity = cinema.Video.GetInstanceId();
            cinema.Reparent(holder);
            if (!GetTree().Paused || !cinema.IsPlaying || cinema.Video.GetInstanceId() != videoIdentity || cinema.Video.Stream is null)
                throw new InvalidDataException("Reparenting the live cinema disposed its resources or restored gameplay early.");
            cinema.Free();
            if (!GetTree().Paused)
                throw new InvalidDataException("Cinema destruction lost the application's preexisting pause.");
            GetTree().Paused = false;
            GD.Print("PASS: actual mission cinema freezes gameplay physics, resumes it on completion and preserves the prior pause on destruction.");
            GetTree().Quit();
        }
        catch (Exception exception) { Fail(exception); }
    }

    private partial class CinemaGameplayProbe : Node
    {
        public int PhysicsUpdates { get; private set; }
        public override void _PhysicsProcess(double delta) => PhysicsUpdates++;
    }

    private sealed class NoActors : IMissionActorFactory
    {
        public void Spawn(TriggerNode node) => throw new InvalidOperationException("Cinema preview cannot spawn mission actors.");
    }
}
