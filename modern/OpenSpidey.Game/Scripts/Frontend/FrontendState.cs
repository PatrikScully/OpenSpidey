using Godot;
using OpenSpidey.Core.Frontend;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Saves;
using OpenSpidey.Game.Gameplay;

namespace OpenSpidey.Game.Frontend;

/// <summary>
/// Process globals the shell shares across scenes: gSaveGame 0x682858, gGlobalRecords, gSaveGameSlots (the card head),
/// the cheat flags, gGameState levels, gDoShellShowTitle and gInLegalScreen.
/// </summary>
public static class FrontendState
{
    private static FrontendResources? resources;
    public static FrontendResources Resources => resources ?? throw new InvalidOperationException("The shell has not been initialised.");
    public static OriginalSaveGame Save { get; private set; } = null!;
    public static OriginalHighScores Records { get; set; } = null!;
    public static OriginalSaveFile Card { get; private set; } = null!;
    public static OriginalCheatFlags Cheats { get; } = new();
    public static OriginalShellSettings Settings { get; } = new();
    /// <summary>gDoShellShowTitle, set for the first boot.</summary>
    public static bool ShowTitle { get; set; } = true;
    /// <summary>gInLegalScreen.</summary>
    public static bool LegalShown { get; set; }
    /// <summary>A level ran since the shell last handed over; its session state flows back into gSaveGame.</summary>
    public static bool LevelEntered { get; private set; }
    public static int CardWrites { get; private set; }

    public static string SavePath => System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("OPENSPIDEY_SAVE_DIR") is { Length: > 0 } directory
        ? directory : ProjectSettings.GlobalizePath("user://save"), OriginalSaveFile.FileName);

    /// <summary>Front_Init 0x440950: gSaveGame from the default save, then PShell_ApplyGameState.</summary>
    public static void Initialise()
    {
        if (resources is not null) return;
        resources = FrontendResources.Load();
        Save = resources.Data.DefaultSave.Clone();
        Records = resources.Data.DefaultRecords.Clone();
        Card = resources.Data.CreateSaveFile();
        Settings.ApplyGameState(Save);
    }

    /// <summary>Card_Load 0x46C7F0 over the PC save file.</summary>
    public static OriginalCardLoadResult CardLoad()
    {
        string path = SavePath;
        bool exists = System.IO.File.Exists(path);
        return Card.Load(exists ? System.IO.File.ReadAllBytes(path) : [], exists);
    }

    /// <summary>Card_Write 0x46C500: 0 on success.</summary>
    public static int CardWrite()
    {
        try
        {
            string path = SavePath;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            System.IO.File.WriteAllBytes(path, Card.Write());
            CardWrites++;
            return 0;
        }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
        {
            GD.PushError(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// game_flow_actions selectLevel after the shell: the WhatIf l1a2_t rewrite, then the session takes gSaveGame's
    /// level, restart point, carried resources, campaign counters and DifficultyLevel.
    /// </summary>
    public static void EnterLevel(GameSessionState application)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (OriginalCheats.CompareStrings("l1a2_t", Save.LevelName) && Cheats.WhatIf != 0) Save.LevelName = "l1a2a_t";
        application.WhatIf = Cheats.WhatIf != 0;
        application.AlternativeAutoexec = Cheats.JoelJewett != 0;
        var progress = new CampaignProgress(Save.CompletionCounts)
        {
            StoryboardUnlocked = Save.StoryboardUnlocked, LastUnlockedCostume = Save.LastUnlockedCostume,
            CostumeUnlockFlags = Save.CostumeUnlockFlags, CharacterViewerUnlockFlags = Save.CharacterViewerUnlockFlags,
            CollectedComicMask = Save.CollectedComicMask
        };
        application.RestoreCampaign(progress, Settings.DifficultyLevel, Save.ComicCoverUnlockFlags, Save.MovieViewerUnlockFlags);
        application.RestoreSavedRestartPointName(Save.RestartPointName);
        application.CarriedHistory = new(new(Save.Webbing, Save.Cartridges, Save.ArmourAmount, Save.ArmourUnlocked, Save.ArmourOn),
            application.CarriedHistory.Previous);
        application.SelectMission(Save.LevelName,
            GodotMissionLevelTransitions.ResolveAuthoredGeometry(Resources.Archive, Save.LevelName, application.AlternativeAutoexec));
        LevelEntered = true;
    }

    /// <summary>The level wrote gSaveGame directly in the original; the session's copies return on re-entering the shell.</summary>
    public static void ReturnFromLevel(GameSessionState application)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (!LevelEntered) return;
        LevelEntered = false;
        if (application.MissionName.Length != 0) Save.LevelName = application.MissionName;
        Save.RestartPointName = application.SavedRestartPointName;
        var carried = application.CarriedHistory.Saved;
        Save.Webbing = carried.Webbing;
        Save.Cartridges = carried.Cartridges;
        Save.ArmourAmount = carried.ArmourAmount;
        Save.ArmourUnlocked = carried.ArmourUnlocked;
        Save.ArmourOn = carried.ArmourOn;
        CampaignProgress progress = application.Campaign;
        for (int index = 0; index < progress.CompletionCounts.Count; index++) Save.CompletionCounts[index] = progress.CompletionCounts[index];
        Save.StoryboardUnlocked = progress.StoryboardUnlocked;
        Save.LastUnlockedCostume = progress.LastUnlockedCostume;
        Save.CostumeUnlockFlags = progress.CostumeUnlockFlags;
        Save.CharacterViewerUnlockFlags = progress.CharacterViewerUnlockFlags;
        Save.CollectedComicMask = progress.CollectedComicMask;
        Save.ComicCoverUnlockFlags = application.ComicCoverUnlockFlags;
        Save.MovieViewerUnlockFlags = application.MovieViewerUnlockFlags;
    }

    /// <summary>Shell_LoadGame: the loaded slot replaces gSaveGame.</summary>
    public static void Replace(OriginalSaveGame save) => Save = save ?? throw new ArgumentNullException(nameof(save));
}
