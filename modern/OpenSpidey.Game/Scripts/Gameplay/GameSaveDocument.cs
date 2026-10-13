using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Gameplay;

public sealed record GameSavedPlayerState(int Webbing, int Cartridges, int ArmourAmount, byte ArmourUnlocked, byte ArmourOn)
{
    public PlayerCarriedSaveState ToState() => new(Webbing, Cartridges, ArmourAmount, ArmourUnlocked, ArmourOn);
    public static GameSavedPlayerState FromState(PlayerCarriedSaveState value) =>
        new(value.Webbing, value.Cartridges, value.ArmourAmount, value.ArmourUnlocked, value.ArmourOn);
}

/// <summary>Portable durable source fields. Asset paths, runtime backup history and transient application state are excluded.</summary>
public sealed record GameSaveDocument(int Version, string MissionName, string RestartPointName,
    GameSavedPlayerState CarriedPlayer, int Difficulty, int[] CompletionCounts,
    byte StoryboardUnlocked, byte LastUnlockedCostume, uint CostumeUnlockFlags,
    uint CharacterViewerUnlockFlags, uint CollectedComicMask, uint ComicCoverUnlockFlags, uint MovieViewerUnlockFlags)
{
    public const int CurrentVersion = 1;

    public static GameSaveDocument Capture(GameSessionState application)
    {
        ArgumentNullException.ThrowIfNull(application);
        CampaignProgress progress = application.Campaign;
        var document = new GameSaveDocument(CurrentVersion, application.MissionName, application.SavedRestartPointName,
            GameSavedPlayerState.FromState(application.CarriedHistory.Saved), application.Difficulty,
            progress.CompletionCounts.Select(value => (int)value).ToArray(), progress.StoryboardUnlocked,
            progress.LastUnlockedCostume, progress.CostumeUnlockFlags, progress.CharacterViewerUnlockFlags,
            progress.CollectedComicMask, application.ComicCoverUnlockFlags, application.MovieViewerUnlockFlags);
        document.Validate();
        return document;
    }

    public void Validate()
    {
        if (Version != CurrentVersion) throw new InvalidDataException($"Unsupported OpenSpidey save version {Version}.");
        ValidateName(MissionName, 9, false);
        ValidateName(RestartPointName, 49, true);
        if (CarriedPlayer is null || Difficulty is < 0 or > 3 || CompletionCounts is null ||
            CompletionCounts.Length != 34 || CompletionCounts.Any(count => count is < 0 or > 255))
            throw new InvalidDataException("The save has invalid source difficulty, carried state or campaign counters.");
    }

    /// <summary>Restores genuine campaign/player fields; the native bootstrap resolves the returned authored mission/restart.</summary>
    public void RestoreProgress(GameSessionState application)
    {
        ArgumentNullException.ThrowIfNull(application);
        Validate();
        var progress = new CampaignProgress(CompletionCounts.Select(count => (byte)count).ToArray())
        {
            StoryboardUnlocked = StoryboardUnlocked,
            LastUnlockedCostume = LastUnlockedCostume,
            CostumeUnlockFlags = CostumeUnlockFlags,
            CharacterViewerUnlockFlags = CharacterViewerUnlockFlags,
            CollectedComicMask = CollectedComicMask
        };
        application.RestoreCampaign(progress, Difficulty, ComicCoverUnlockFlags, MovieViewerUnlockFlags);
        application.RestoreSavedRestartPointName(RestartPointName);
        // A disk load replaces the saved fields; the process-owned destructor backup remains independent.
        application.CarriedHistory = new(CarriedPlayer.ToState(), application.CarriedHistory.Previous);
    }

    private static void ValidateName(string name, int maximumLength, bool allowEmpty)
    {
        if (name is null || !allowEmpty && name.Length == 0 || name.Length > maximumLength ||
            name.Any(character => character == 0 || character > 255))
            throw new InvalidDataException("A saved mission or restart name must fit its original Latin1 source field.");
    }
}
