using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Frontend;
using OpenSpidey.Core.Saves;
using OpenSpidey.Game.Gameplay;

namespace OpenSpidey.Game.Frontend;

/// <summary>
/// The application's main scene. Shell_DoShell 0x4A1A80: legal and title on first boot, main menu, difficulty and level
/// handoff; levels return here on quit or final completion. --workbench opens the asset workbench instead.
/// </summary>
public partial class ShellSession : Node
{
    public const string ScenePath = "res://Scenes/ShellSession.tscn";
    private const string LevelScene = "res://Scenes/LevelSession.tscn";
    private const string WorkbenchScene = "res://Scenes/AssetWorkbench.tscn";
    /// <summary>Shell_LegalScreen waits G_VBLANKS + 180.</summary>
    private const ulong LegalVblanks = 180;
    private FrontendCanvas canvas = null!;
    private FrontendInput? input;
    private FrontendFrames frames = null!;
    private FrontendAudio? audio;
    private ShellSmoke? smoke;

    public override void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();
        if (args.Contains("--workbench") || args.Contains("--smoke-test") || args.Contains("--verify-collision"))
        {
            Callable.From(() => GetTree().ChangeSceneToFile(WorkbenchScene)).CallDeferred();
            return;
        }
        if (args.Contains("--smoke-shell")) smoke = new(args.Contains("--smoke-shell-menu-only"));
        try
        {
            if (!System.Environment.Is64BitProcess) throw new PlatformNotSupportedException("OpenSpidey requires a 64-bit runtime.");
            if (GameFolder.Resolve(args) is null)
            {
                if (smoke is not null) throw new InvalidOperationException("Shell smoke requires --assets with your original game folder.");
                ShowFolderPrompt();
                return;
            }
            string defaultMedia = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                ".local/share/openspidey/media");
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("OPENSPIDEY_MEDIA")) && System.IO.Directory.Exists(defaultMedia))
                System.Environment.SetEnvironmentVariable("OPENSPIDEY_MEDIA", defaultMedia);
            FrontendState.Initialise();
            FrontendState.ReturnFromLevel(GetTree().Root.GetNode<GameSessionState>("GameSession"));
            FrontendResources resources = FrontendState.Resources;
            canvas = FrontendCanvas.Create(resources.NormalFont, resources.SmallFont, resources.BigFont);
            canvas.BindMenuBox(resources.MenuBoxFrames, resources.MenuBoxSlices, resources.Texture);
            AddChild(canvas);
            input = new FrontendInput();
            frames = new FrontendFrames();
            audio = FrontendAudio.Create(resources.Archive);
            AddChild(audio);
            _ = RunShellAsync();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private void ShowFolderPrompt()
    {
        var layer = new CanvasLayer();
        layer.AddChild(new Label
        {
            Position = new(20, 20),
            Text = "OpenSpidey needs your original Spider-Man (PC) game folder.\n" +
                "Start it once with --assets=/path/to/game (it is remembered) or set OPENSPIDEY_ASSETS.\n" +
                "The asset workbench is available with --workbench."
        });
        AddChild(layer);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key) input?.KeyEvent(key);
    }

    private async Task RunShellAsync()
    {
        try
        {
            await RunShellLoopAsync();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private FrontendInput Pad => input ?? throw new InvalidOperationException("The shell input is not initialised.");

    private async Task RunShellLoopAsync()
    {
        if (!FrontendState.LegalShown)
        {
            FrontendState.LegalShown = true;
            await LegalScreenAsync();
        }
        while (true)
        {
            if (FrontendState.ShowTitle)
            {
                FrontendState.ShowTitle = false;
                await TitleScreenAsync();
            }
            OriginalSaveGame save = FrontendState.Save;
            int initial = OriginalMainMenu.InitialType(save);
            var mainMenu = new OriginalMainMenu(FrontendState.Resources.Data.MainMenu, initial);
            OriginalMainMenu.PrepareContinue(save, FrontendState.Resources.Data.Levels);
            await MainMenuLoopAsync(mainMenu, save);
            int type = mainMenu.Selected.Type;
            smoke?.MainMenuAccepted(type);
            if (type == 1)
            {
                if (!await DifficultyLoopAsync(newGame: true)) continue;
                if (smoke is { MenuOnly: true })
                {
                    smoke.Finish(FrontendState.Save);
                    GetTree().Quit();
                    return;
                }
                EnterGameplay();
                return;
            }
            if (type == 2 && save.HasGame != 0)
            {
                EnterGameplay();
                return;
            }
            if (type == 6)
            {
                GetTree().Quit();
                return;
            }
            GD.PushWarning($"Shell menu type {type} is not wired yet.");
            await frames.Next();
        }
    }

    private void EnterGameplay()
    {
        GameSessionState application = GetTree().Root.GetNode<GameSessionState>("GameSession");
        smoke?.Finish(FrontendState.Save);
        FrontendState.EnterLevel(application);
        if (smoke is not null)
            GetTree().Root.AddChild(new ShellBootSmoke(FrontendState.Save.LevelName, FrontendState.Settings.DifficultyLevel) { Name = "ShellBootSmoke" });
        Error result = GetTree().ChangeSceneToFile(LevelScene);
        if (result != Error.Ok) throw new IOException($"Could not open the level scene: {result}.");
    }

    /// <summary>Shell_LegalScreen 0x4A1930: LegalPC.bmp until G_VBLANKS passes start + 180; input is not read.</summary>
    private async Task LegalScreenAsync()
    {
        Texture2D legal = FrontendState.Resources.Bitmap("LegalPC.bmp");
        ulong end = Vblanks() + LegalVblanks;
        while (true)
        {
            canvas.Begin();
            DrawScreenBitmap(legal);
            Pad.Poll();
            if (Vblanks() > end) break;
            await frames.Next();
        }
        smoke?.Reached(ShellSmoke.Stage.Legal);
        Pad.Clear();
    }

    /// <summary>
    /// Shell_TitleScreen 0x4A17A0: title.bmp and Redbook_XAPlay(0x43, 13, 0) until CheckTriggers(0x40010) fires.
    /// Front_MiniUpdate draws nothing on PC, whose pad type is always 0x41.
    /// </summary>
    private async Task TitleScreenAsync()
    {
        Texture2D title = FrontendState.Resources.Bitmap("title.bmp");
        audio?.XaPlay(0x43, 0xD, 0, FrontendState.Settings.VoiceLevel);
        Pad.Clear();
        while (true)
        {
            canvas.Begin();
            DrawScreenBitmap(title);
            if (smoke is not null && smoke.TitleFrames++ == 1)
            {
                await CaptureAsync("title");
                Pad.Inject(ShellTriggers.Enter);
            }
            Pad.Poll();
            if (Pad.Check(ShellTriggers.Title, true, true)) break;
            await frames.Next();
        }
        audio?.XaStop();
        smoke?.Reached(ShellSmoke.Stage.Title);
        Pad.Clear();
    }

    /// <summary>Shell_MainMenu 0x493990 loop: Redbook_XAPlay(78, 10, 0) on entry, replayed whenever Redbook_XAStat reports 4, stopped on exit.</summary>
    private async Task MainMenuLoopAsync(OriginalMainMenu menu, OriginalSaveGame save)
    {
        audio?.XaPlay(78, 10, 0, FrontendState.Settings.VoiceLevel);
        while (true)
        {
            MainMenuFrame(menu, save);
            if (smoke is not null && smoke.MenuFrames++ == 1)
            {
                await CaptureAsync("menu");
                Pad.Inject(ShellTriggers.Accept);
            }
            Pad.Poll();
            if (audio is { XaFinished: true })
            {
                audio.XaStop();
                audio.XaPlay(78, 10, 0, FrontendState.Settings.VoiceLevel);
            }
            if (menu.Navigate(mask => Pad.Check(mask, false), mask => Pad.Check(mask, true, true),
                    save.HasGame != 0, cardPresent: true))
                audio?.Play(OriginalShellRules.AcceptSound);
            if (Pad.Check(ShellTriggers.Accept, true, true) && menu.Accepts(save.HasGame != 0, 0))
            {
                audio?.XaStop();
                return;
            }
            Pad.Clear();
            await frames.Next();
        }
    }

    /// <summary>
    /// Shell_MainMenu 0x493990 frame: continue label, menubg, unselected entries in the small font without shadows, then
    /// Shell_DrawTitleBar for the selected line. The CDummy spidey / menu icon models in the oval are not ported yet.
    /// </summary>
    private void MainMenuFrame(OriginalMainMenu menu, OriginalSaveGame save)
    {
        canvas.Begin();
        canvas.DefaultText();
        canvas.Colour(0x6B, 0x5D, 0xA7);
        if (menu.Selected.Type == 2)
        {
            (int levelIndex, bool allComplete) = OriginalMainMenu.ContinueLevel(save.CompletionCounts);
            (int y, string label) = OriginalMainMenu.ContinueLabel(levelIndex, allComplete, FrontendState.Resources.Data.Levels);
            canvas.Text(256, y, label);
        }
        DrawBackground();
        canvas.Font = FrontendFont.Small;
        canvas.Shadows = false;
        IReadOnlyList<OriginalMainMenuEntry> table = FrontendState.Resources.Data.MainMenu;
        for (int index = 0; index < table.Count; index++)
        {
            OriginalMainMenuEntry entry = table[index];
            if (index != 0 || save.HasGame != 0)
            {
                canvas.Justify = 0;
                canvas.Colour(0x45, 0x3C, 0x6B);
            }
            else canvas.Colour(0x1A, 0x17, 0x29);
            if (index != menu.Line) canvas.Text(entry.X, entry.Y, entry.Text);
        }
        canvas.DefaultText();
        canvas.Shadows = true;
        OriginalMainMenuEntry selected = menu.Selected;
        DrawTitleBar(selected.X, selected.Y, selected.Text, menu.Line >= 4 ? 512 : 0, 80, -20, 29);
    }

    /// <summary>Shell_DrawTitleBar (normal font): centred white text and PShell_DrawHighlight from the screen edge a5.</summary>
    private void DrawTitleBar(int x, int y, string text, int edge, int margin, int yOffset, int height)
    {
        canvas.Justify = 0;
        canvas.Colour(0x80, 0x80, 0x80);
        canvas.Text(x, y, text);
        int halfWidth = canvas.TextWidth(text) / 2;
        int width = edge < x ? margin + x + halfWidth - edge : x - halfWidth - edge - margin;
        canvas.Highlight(FrontendState.Resources.Texture(FrontendResources.TitleBarTexture), edge, y + yOffset, width, height);
    }

    private async Task<bool> DifficultyLoopAsync(bool newGame)
    {
        OriginalMenu menu = OriginalShellRules.DifficultyMenu(canvas.TextWidth);
        menu.Zoom(0);
        int repeat = 0;
        while (true)
        {
            canvas.Begin();
            DrawBackground();
            menu.AdvanceZoomBox(canvas.DrawMenuBox);
            if (menu.FinishedZooming)
            {
                canvas.InstructionalText();
                canvas.Text(256, 190, "please select difficulty level");
                canvas.DefaultText();
                foreach (OriginalMenuLine line in menu.Lines())
                {
                    canvas.Colour(line.Top.R, line.Top.G, line.Top.B);
                    canvas.Text(menu.X, line.Y, line.Entry.Name);
                }
            }
            if (smoke is not null && menu.FinishedZooming)
            {
                if (smoke.DifficultyZoomed(canvas.QuadDraws)) await CaptureAsync("difficulty");
                Pad.Inject(ShellTriggers.Accept);
            }
            Pad.Poll();
            if (menu.Update(mask => Pad.Check(mask, false), ref repeat))
                audio?.Play(OriginalShellRules.AcceptSound);
            if (Pad.Check(ShellTriggers.Back, true, true)) return false;
            if (Pad.Check(ShellTriggers.Accept, true, true) && menu.FinishedZooming)
            {
                (int difficulty, bool kid) = OriginalShellRules.ApplyDifficulty(FrontendState.Save, menu.Line);
                FrontendState.Settings.DifficultyLevel = difficulty;
                FrontendState.Settings.PracticeDifficulty = kid;
                if (newGame) FrontendState.Save.BeginNewGame(FrontendState.Resources.Data.DefaultSave, difficulty);
                return true;
            }
            Pad.Clear();
            await frames.Next();
        }
    }

    /// <summary>Sprite2::draw(0, 0, 8, -1) of a 512x240 shell BMP: its pixels are the shell design space, filling the 4:3 frame.</summary>
    private void DrawScreenBitmap(Texture2D bitmap) => canvas.Sprite(bitmap, 0, 0, bitmap.GetWidth(), bitmap.GetHeight());

    private void DrawBackground()
    {
        SpriteAnimationFrame frame = FrontendState.Resources.Animation("menubg").Frames[0];
        canvas.Sprite(FrontendState.Resources.Texture(frame.TextureChecksum), 0, 0, 512, 240);
    }

    /// <summary>Rendered smoke captures (OPENSPIDEY_CAPTURE): one PNG per shell stage after the frame is drawn.</summary>
    private async Task CaptureAsync(string stage)
    {
        string? capture = System.Environment.GetEnvironmentVariable("OPENSPIDEY_CAPTURE");
        if (string.IsNullOrWhiteSpace(capture) || DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = System.IO.Path.ChangeExtension(capture, null) + "-" + stage + ".png";
        Error result = GetViewport().GetTexture().GetImage().SavePng(path);
        if (result != Error.Ok) throw new IOException($"Could not save {path}: {result}.");
    }

    private static ulong Vblanks() => Time.GetTicksMsec() * 60 / 1000;

    public override void _Process(double delta)
    {
        if (frames is { Waiting: true }) frames.Resume();
    }
}
