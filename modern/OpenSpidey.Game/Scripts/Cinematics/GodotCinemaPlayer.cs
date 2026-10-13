using Godot;
using OpenSpidey.Core.Cinematics;

namespace OpenSpidey.Game.Cinematics;

/// <summary>Original comic-cover/movie sequence using Godot's cross-platform Theora playback.</summary>
public partial class GodotCinemaPlayer : CanvasLayer, ICinemaPlayback
{
    private Control root = null!;
    private TextureRect cover = null!;
    private VideoStreamPlayer video = null!;
    private ImageTexture? coverTexture;
    private VideoStreamTheora? stream;
    private MovieDefinition? movie;
    private double coverElapsed;
    private bool showingCover;
    private int moviePolls;
    private SceneTree? pausedTree;
    private bool previousPause;

    public string CacheDirectory { get; init; } = "";
    public bool PauseGameplay { get; init; }
    public bool IsPlaying { get; private set; }
    public bool ShowingCover => showingCover;
    public bool CanSkipMovie => moviePolls >= 60;
    public double MoviePosition => video.StreamPosition;
    public VideoStreamPlayer Video => video;
    public event Action? Completed;

    public override void _Ready()
    {
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always;
        root = new Control { Name = "Cinema", MouseFilter = Control.MouseFilterEnum.Stop };
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var background = new ColorRect { Color = Colors.Black };
        root.AddChild(background);
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        cover = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        root.AddChild(cover);
        cover.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        video = new VideoStreamPlayer { Expand = true, Loop = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(video);
        video.Finished += Finish;
        root.Resized += UpdateMovieLayout;
        root.Hide();
    }

    public void Play(CinemaDefinition cinema)
    {
        if (!IsInsideTree()) throw new InvalidOperationException("Cinema player must belong to a scene before playback.");
        if (IsPlaying) throw new InvalidOperationException("A cinema sequence is already playing.");
        movie = CinemaCatalog.GetMovie(cinema.MovieTrack);
        string videoPath = System.IO.Path.Combine(CacheDirectory, movie.ConvertedName);
        if (!System.IO.File.Exists(videoPath)) throw new FileNotFoundException("Prepare the original media with tools/modern.py media first.", videoPath);
        ImageTexture? nextCover = null;
        if (cinema.CoverName is { } name)
        {
            string path = System.IO.Path.Combine(CacheDirectory, name + ".png");
            using Image image = Image.LoadFromFile(path);
            if (image.IsEmpty()) throw new InvalidDataException($"Could not load cinema cover: {path}");
            nextCover = ImageTexture.CreateFromImage(image);
        }
        coverTexture?.Dispose();
        coverTexture = nextCover;
        cover.Texture = nextCover;
        stream?.Dispose();
        stream = new VideoStreamTheora { File = videoPath };
        video.Stream = stream;
        showingCover = nextCover is not null;
        coverElapsed = 0;
        cover.Visible = showingCover;
        video.Hide();
        IsPlaying = true;
        if (PauseGameplay)
        {
            pausedTree = GetTree();
            previousPause = pausedTree.Paused;
            pausedTree.Paused = true;
        }
        root.Show();
        UpdateMovieLayout();
        if (!showingCover) BeginMovie();
    }

    public override void _Process(double delta)
    {
        if (!IsPlaying) return;
        if (!showingCover) { moviePolls++; return; }
        coverElapsed += delta;
        // Original cover timeout: 300 vblanks at 60 Hz.
        if (coverElapsed >= 5) BeginMovie();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        bool skip = @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space or Key.Enter or Key.Escape }
            or InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.A or JoyButton.Start };
        if (!IsPlaying || !skip) return;
        RequestSkip();
        GetViewport().SetInputAsHandled();
    }

    public void RequestSkip()
    {
        if (!IsPlaying) return;
        if (showingCover) BeginMovie();
        // PCMOVIE_NextFrame returns while BinkWait is pending too: this counts playback polls, not decoded frames.
        else if (CanSkipMovie) Finish();
    }

    private void BeginMovie()
    {
        showingCover = false;
        moviePolls = 0;
        cover.Hide();
        video.Show();
        video.Play();
    }

    private void UpdateMovieLayout()
    {
        if (movie is null) return;
        MediaRectangle rectangle = MediaFrameLayout.Fit(root.Size.X, root.Size.Y, movie.Width, movie.Height);
        video.Position = new(rectangle.X, rectangle.Y);
        video.Size = new(rectangle.Width, rectangle.Height);
    }

    private void Finish()
    {
        if (!IsPlaying) return;
        video.Stop();
        IsPlaying = false;
        showingCover = false;
        root.Hide();
        RestoreGameplay();
        Completed?.Invoke();
    }

    private void RestoreGameplay()
    {
        if (pausedTree is null) return;
        if (GodotObject.IsInstanceValid(pausedTree)) pausedTree.Paused = previousPause;
        pausedTree = null;
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        RestoreGameplay();
        if (GodotObject.IsInstanceValid(video))
        {
            video.Stop();
            video.Stream = null;
        }
        if (GodotObject.IsInstanceValid(cover)) cover.Texture = null;
        stream?.Dispose();
        coverTexture?.Dispose();
    }
}
