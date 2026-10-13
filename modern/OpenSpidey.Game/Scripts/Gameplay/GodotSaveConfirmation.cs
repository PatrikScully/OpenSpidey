using Godot;
using OpenSpidey.Core.Audio;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Gameplay;

/// <summary>Real native save confirmation; held source accept input must be released before either choice is accepted.</summary>
public partial class GodotSaveConfirmation : Node
{
    private GameSessionState application = null!;
    private GameSaveStore store = null!;
    private SoundChannelController sounds = null!;
    private TaskCompletionSource<bool>? choice;
    private bool requestActive;
    private bool inputReady;
    public ConfirmationDialog? Dialog { get; private set; }
    public bool InputReady => inputReady;
    public Exception? LastSaveFailure { get; private set; }

    public static GodotSaveConfirmation Create(GameSessionState application, GameSaveStore store, SoundChannelController sounds)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(sounds);
        return new() { application = application, store = store, sounds = sounds,
            Name = "SaveConfirmation", ProcessMode = ProcessModeEnum.Always };
    }

    public override void _Ready()
    {
        Dialog = new ConfirmationDialog
        {
            Name = "SaveProgressDialog", Title = "Save progress", DialogText = "Save your progress?",
            OkButtonText = "Save", CancelButtonText = "Don't save", DialogHideOnOk = false,
            Exclusive = true, ProcessMode = ProcessModeEnum.Always
        };
        AddChild(Dialog);
        Dialog.Confirmed += Accept;
        Dialog.Canceled += Decline;
        SetProcess(false);
    }

    public async Task<bool> RequestSaveAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInsideTree() || Dialog is null) throw new InvalidOperationException("Save confirmation requires its actual native dialog.");
        if (requestActive) throw new InvalidOperationException("A save confirmation is already active.");
        cancellationToken.ThrowIfCancellationRequested();
        requestActive = true;
        LastSaveFailure = null;
        try
        {
            while (true)
            {
                Dialog.DialogText = LastSaveFailure is null ? "Save your progress?" : "Your progress could not be saved. Try again?";
                inputReady = false;
                var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                choice = pending;
                Dialog.GetOkButton().Disabled = true;
                Dialog.GetCancelButton().Disabled = true;
                Input.MouseMode = Input.MouseModeEnum.Visible;
                Dialog.PopupCentered(new(420, 140));
                SetProcess(true);
                bool accepted;
                using (CancellationTokenRegistration registration = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken)))
                    accepted = await pending.Task;
                choice = null;
                SetProcess(false);
                Dialog.Hide();
                // Original PShell_MaybeSaveGame clears its triggering input and plays source sound1F for either choice.
                sounds.Play(0x1F, 0x2000);
                if (!accepted) return false;
                try
                {
                    await store.SaveAsync(GameSaveDocument.Capture(application), cancellationToken);
                    return true;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    LastSaveFailure = exception;
                    // The original failed-write result returns to the same genuine confirmation loop.
                }
            }
        }
        finally
        {
            choice = null;
            requestActive = false;
            if (GodotObject.IsInstanceValid(this)) SetProcess(false);
            if (Dialog is not null && GodotObject.IsInstanceValid(Dialog)) Dialog.Hide();
        }
    }

    public override void _Process(double delta)
    {
        if (inputReady || Dialog is null) return;
        string? jump = PlayerControls.SourceActionName(0x10);
        if (Input.IsActionPressed("ui_accept") || jump is not null && Input.IsActionPressed(jump)) return;
        inputReady = true;
        Dialog.GetOkButton().Disabled = false;
        Dialog.GetCancelButton().Disabled = false;
        Dialog.GetOkButton().GrabFocus();
    }

    private void Accept()
    {
        if (inputReady) choice?.TrySetResult(true);
    }

    private void Decline()
    {
        if (inputReady) choice?.TrySetResult(false);
        else if (choice is not null && Dialog is not null)
            Callable.From(() => Dialog?.PopupCentered(new(420, 140))).CallDeferred();
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        choice?.TrySetCanceled();
        choice = null;
        if (Dialog is not null && GodotObject.IsInstanceValid(Dialog))
        {
            Dialog.Confirmed -= Accept;
            Dialog.Canceled -= Decline;
        }
        Dialog = null;
    }
}
