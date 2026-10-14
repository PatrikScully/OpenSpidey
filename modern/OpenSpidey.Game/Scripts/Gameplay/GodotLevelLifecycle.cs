using Godot;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Game.Gameplay;

/// <summary>Source death/retry UI and deferred application navigation through the actual SceneTree.</summary>
public partial class GodotLevelLifecycle : CanvasLayer
{
    private bool retryShown;
    private bool navigationStarted;
    private object? navigationOwner;
    private GameSessionState? application;
    private Action<LevelExitReason>? prepareNavigation;
    private Func<CancellationToken, Task<bool>>? completeLevel;
    private readonly CancellationTokenSource navigationCancellation = new();
    private string levelScenePath = "";
    private string? menuScenePath;
    public bool Initialized { get; private set; } = true;
    public LevelExitReason ExitReason { get; private set; }
    public Exception? NavigationFailure { get; private set; }
    public Task? NavigationTask { get; private set; }
    public event Action<LevelExitReason>? Exited;
    public event Action<LevelExitReason>? Restarting;

    public void ResetInitialization() => Initialized = false;

    public void ConfigureMissionNavigation(object owner, GameSessionState application,
        Action<LevelExitReason> prepareNavigation, Func<CancellationToken, Task<bool>>? completeLevel, string levelScenePath, string? menuScenePath)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(prepareNavigation);
        ArgumentException.ThrowIfNullOrEmpty(levelScenePath);
        if (navigationOwner is not null)
            throw new InvalidOperationException("The level lifecycle already has a mission navigation owner.");
        navigationOwner = owner;
        this.application = application;
        this.prepareNavigation = prepareNavigation;
        this.completeLevel = completeLevel;
        this.levelScenePath = levelScenePath;
        this.menuScenePath = menuScenePath;
    }

    public void RemoveMissionNavigation(object owner)
    {
        if (!ReferenceEquals(owner, navigationOwner)) return;
        if (navigationStarted) navigationCancellation.Cancel();
        navigationOwner = null;
        application = null;
        prepareNavigation = null;
        completeLevel = null;
        levelScenePath = "";
        menuScenePath = null;
    }

    public void RequestExit(int sourceValue)
    {
        var reason = (LevelExitReason)sourceValue;
        if (reason is not (LevelExitReason.PlayerDied or LevelExitReason.RestartFromBeginning or
            LevelExitReason.RetryRequested or LevelExitReason.LevelCompleted or LevelExitReason.QuitLevel))
            throw new NotSupportedException($"Level exit {sourceValue} requires the remaining application flow.");
        if (ExitReason != LevelExitReason.Running) return;
        if (reason == LevelExitReason.LevelCompleted && (navigationOwner is null || completeLevel is null))
            throw new NotSupportedException("Level completion requires the original completion-count, unlock and save owners before campaign navigation can run.");
        ExitReason = reason;
        application?.SetExitReason(reason);
        Exited?.Invoke(reason);
        if (reason is LevelExitReason.LevelCompleted or LevelExitReason.QuitLevel) Callable.From(Navigate).CallDeferred();
        else Callable.From(ShowRetry).CallDeferred();
    }

    private void ShowRetry()
    {
        if (retryShown) return;
        retryShown = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        var center = new CenterContainer { Name = "CheckpointRetry" };
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer();
        center.AddChild(panel);
        var margin = new MarginContainer();
        foreach (string side in new[] { "left", "top", "right", "bottom" })
            margin.AddThemeConstantOverride("margin_" + side, 24);
        panel.AddChild(margin);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 16);
        margin.AddChild(content);
        content.AddChild(new Label { Text = "Try again", HorizontalAlignment = HorizontalAlignment.Center });
        var retry = new Button { Text = "Restart checkpoint" };
        content.AddChild(retry);
        retry.Pressed += () => Callable.From(Navigate).CallDeferred();
        retry.GrabFocus();
    }

    private void Navigate()
    {
        if (navigationStarted) return;
        navigationStarted = true;
        NavigationTask = NavigateAsync(navigationCancellation.Token);
    }

    private async Task NavigateAsync(CancellationToken cancellationToken)
    {
        try
        {
            LevelExitReason reason = ExitReason;
            SceneTree tree = GetTree();
            if (reason is not (LevelExitReason.LevelCompleted or LevelExitReason.QuitLevel)) Restarting?.Invoke(reason);
            prepareNavigation?.Invoke(reason);
            Error result;
            if (reason == LevelExitReason.QuitLevel)
                result = tree.ChangeSceneToFile(menuScenePath ?? throw new NotSupportedException("Quitting a level requires the shell menu scene."));
            else if (reason == LevelExitReason.LevelCompleted)
            {
                Func<CancellationToken, Task<bool>> completion = completeLevel ??
                    throw new NotSupportedException("The genuine level completion owner is unavailable.");
                bool finalLevelCompleted = await completion(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                string target = finalLevelCompleted
                    ? menuScenePath ?? throw new NotSupportedException("The final campaign completion requires an actual menu scene.")
                    : levelScenePath;
                result = tree.ChangeSceneToFile(target);
            }
            else
            {
                if (application is not null && application.SceneAsset is null)
                    throw new InvalidOperationException("The retry has no selected authored mission environment.");
                result = tree.ReloadCurrentScene();
            }
            if (result != Error.Ok) throw new IOException($"Could not navigate after source level exit {(int)reason}: {result}.");
        }
        catch (Exception exception)
        {
            NavigationFailure = exception;
            if (exception is not OperationCanceledException) GD.PushError(exception.ToString());
        }
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        navigationCancellation.Cancel();
        navigationCancellation.Dispose();
        navigationOwner = null;
        application = null;
        prepareNavigation = null;
        completeLevel = null;
        Exited = null;
        Restarting = null;
    }
}
