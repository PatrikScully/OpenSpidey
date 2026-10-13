using Godot;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Game.World;

/// <summary>Checks campaign metadata in the actual private executable used by the native game session.</summary>
internal static class CampaignCompletionImportChecks
{
    public static OriginalLevelCatalog Verify(ReadOnlyMemory<byte> originalExecutable)
    {
        OriginalLevelCatalog catalog = OriginalLevelCatalog.Read(originalExecutable);
        if (catalog.Levels.Count != 34 || catalog.Levels[0] is not { MissionName: "l1a1_t", ComicCoverUnlockMask: 1 } ||
            catalog.Levels[2] is not { MissionName: "l1a3_t", FlowFlags: 3 } ||
            catalog.Levels[11] is not { MissionName: "l4a1_t", CharacterViewerUnlockMask: 0x1000 } ||
            catalog.Levels[33] is not { MissionName: "l8a6_t", CharacterViewerUnlockMask: 0x800000 } ||
            catalog.FindIndex("L1A2a_T") != 1 || catalog.FindIndex("x1z3") != 2)
            throw new InvalidDataException("Native private campaign metadata or original chapter/area lookup differs from the PC source.");
        var ordinary = new CampaignProgress();
        CampaignCompletionResult next = CampaignCompletionRules.Complete(ordinary, catalog, "l1a3_t", 2, 0);
        if (next.CompletedLevelIndex != 1 || ordinary.CompletionCounts[1] != 1 || !next.SaveGameRequested || next.FinalLevelCompleted)
            throw new InvalidDataException("Actual target metadata did not select the previous area's counter and source bit2 save request.");
        var final = new CampaignProgress(Enumerable.Repeat((byte)1, 34).ToArray());
        CampaignCompletionResult finished = CampaignCompletionRules.Complete(final, catalog, "L8A7_T", 3, 0);
        if (finished.CompletedLevelIndex != 33 || !finished.FinalLevelCompleted || finished.SaveGameRequested ||
            final.CompletionCounts[33] != 2 || final.StoryboardUnlocked != 1 || finished.NewlyUnlockedCostume != 3 ||
            (final.CostumeUnlockFlags & 0xCC) != 0xCC || (final.CharacterViewerUnlockFlags & 0x1000000) == 0)
            throw new InvalidDataException("Original final-level metadata did not compose genuine completion/unlock policy.");
        GD.Print("PASS: actual private PC campaign34 metadata, source lookup aliases, completion counter selection, save-request metadata and final unlock/menu policy.");
        return catalog;
    }
}
