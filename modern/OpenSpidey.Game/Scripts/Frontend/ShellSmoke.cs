using Godot;
using OpenSpidey.Core.Saves;
using OpenSpidey.Game.Gameplay;

namespace OpenSpidey.Game.Frontend;

/// <summary>Headless shell-smoke driver: Enter on the title, accept new game and the default difficulty, then the level start.</summary>
public sealed class ShellSmoke(bool menuOnly)
{
    public enum Stage { None, Legal, Title }
    private Stage reached;
    public bool MenuOnly { get; } = menuOnly;
    public int TitleFrames { get; set; }
    public int MenuFrames { get; set; }
    private int difficultyQuads = -1;

    public void Reached(Stage stage)
    {
        if (stage != reached + 1) throw new InvalidOperationException($"Shell smoke reached {stage} after {reached}.");
        reached = stage;
    }

    public void MainMenuAccepted(int type)
    {
        if (reached != Stage.Title) throw new InvalidOperationException("Shell smoke reached the main menu without the legal and title screens.");
        if (type != 1) throw new InvalidOperationException($"Shell smoke expected the empty-save main menu to start on new game (1), got {type}.");
    }

    /// <summary>Background, eight border frames and the fill of PShell_DrawMenuBox.</summary>
    /// <returns>True the first time the zoom finishes.</returns>
    public bool DifficultyZoomed(int quads)
    {
        if (difficultyQuads >= 0) return false;
        if (quads < 10) throw new InvalidOperationException($"The difficulty zoom drew {quads} polys; the menubox needs eight frames and its fill.");
        difficultyQuads = quads;
        return true;
    }

    public void Finish(OriginalSaveGame save)
    {
        if (difficultyQuads < 0) throw new InvalidOperationException("The difficulty menubox never finished zooming.");
        if (save.LevelName != "l1a1_t" || save.HasGame == 0 || save.Difficulty != 2)
            throw new InvalidOperationException($"New game did not seal gSaveGame (level {save.LevelName}, difficulty {save.Difficulty}).");
    }
}

/// <summary>Survives the scene change and proves the shell-selected story level reached gameplay.</summary>
public partial class ShellBootSmoke : Node
{
    private readonly string missionName = "";
    private readonly int difficulty;
    private const ulong TimeoutMsec = 300_000;
    private const int FramesAfterStart = 60;
    private readonly ulong started = Time.GetTicksMsec();
    private StoryLevelSession? story;
    private int framesAfterStart = -1;

    public ShellBootSmoke() { }
    public ShellBootSmoke(string missionName, int difficulty) => (this.missionName, this.difficulty) = (missionName, difficulty);

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _PhysicsProcess(double delta)
    {
        try
        {
            if (Time.GetTicksMsec() - started > TimeoutMsec) throw new TimeoutException("The shell-selected level did not start gameplay.");
            if (story is null)
            {
                story = GetTree().CurrentScene?.GetNodeOrNull<StoryLevelSession>("StoryMission");
                if (story is null) return;
                story.GameplayStarted += Started;
            }
            if (story.Failure is { } failure) throw new InvalidOperationException("The shell-selected level failed.", failure);
            if (framesAfterStart < 0) return;
            if (++framesAfterStart < FramesAfterStart) return;
            GD.Print($"Shell boot reached {story.Application.MissionName} gameplay at difficulty {story.Application.Difficulty}.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private void Started()
    {
        GameSessionState application = story!.Application;
        if (!application.MissionName.Equals(missionName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(application.SceneAsset, "data/L1A1_G.psx", StringComparison.OrdinalIgnoreCase) ||
            application.Difficulty != difficulty || story.Player is null || story.Checkpoints.Current is null)
        {
            GD.PushError($"Shell boot started {application.MissionName} ({application.SceneAsset}) at difficulty {application.Difficulty}.");
            GetTree().Quit(1);
            return;
        }
        framesAfterStart = 0;
    }
}
