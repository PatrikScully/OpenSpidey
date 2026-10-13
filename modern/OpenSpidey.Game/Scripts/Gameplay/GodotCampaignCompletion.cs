using OpenSpidey.Assets.Executables;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Game.Gameplay;

/// <summary>Genuine campaign counter/unlock writes followed by source fade and, when requested, actual durable-save confirmation.</summary>
public sealed class GodotCampaignCompletion
{
    private readonly GameSessionState application;
    private readonly OriginalLevelCatalog catalog;
    private readonly GodotCampaignFade fade;
    private readonly GodotSaveConfirmation saves;
    private bool started;
    public CampaignCompletionResult? Result { get; private set; }
    public Exception? Failure { get; private set; }

    public GodotCampaignCompletion(GameSessionState application, OriginalLevelCatalog catalog,
        GodotCampaignFade fade, GodotSaveConfirmation saves)
    {
        this.application = application ?? throw new ArgumentNullException(nameof(application));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.fade = fade ?? throw new ArgumentNullException(nameof(fade));
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
    }

    public async Task<bool> CompleteAsync(CancellationToken cancellationToken)
    {
        if (started) throw new InvalidOperationException("This source level's completion cannot be replayed.");
        started = true;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task fading = fade.BeginSepiaAsync(cancellationToken);
            CampaignCompletionResult result = CampaignCompletionRules.Complete(application.Campaign, catalog,
                application.MissionName, application.Difficulty, application.RenderTest);
            Result = result;
            // Original final-area branch returns to the menu before the ordinary fade/save wait.
            if (result.FinalLevelCompleted) return true;
            await fading;
            fade.HideSnapshot();
            if (result.SaveGameRequested) await saves.RequestSaveAsync(cancellationToken);
            return false;
        }
        catch (Exception exception) { Failure = exception; throw; }
    }
}
