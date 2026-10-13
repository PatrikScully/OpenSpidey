using OpenSpidey.Assets.Executables;

namespace OpenSpidey.Core.Missions;

/// <summary>The source campaign completion counters and unlock fields, separate from live player resources.</summary>
public sealed class CampaignProgress
{
    private readonly byte[] counts;
    public IReadOnlyList<byte> CompletionCounts { get; }
    public byte StoryboardUnlocked { get; set; }
    public byte LastUnlockedCostume { get; set; }
    public uint CostumeUnlockFlags { get; set; }
    public uint CharacterViewerUnlockFlags { get; set; }
    public uint CollectedComicMask { get; set; }

    public CampaignProgress() : this(new byte[OriginalLevelCatalog.CampaignLevelCount]) { }

    public CampaignProgress(ReadOnlySpan<byte> completionCounts)
    {
        if (completionCounts.Length != OriginalLevelCatalog.CampaignLevelCount)
            throw new ArgumentException("The original campaign contains exactly 34 completion counters.", nameof(completionCounts));
        counts = completionCounts.ToArray();
        CompletionCounts = Array.AsReadOnly(counts);
    }

    internal void IncrementCompletion(int index)
    {
        if (index == -1)
        {
            // The release executable's index0 target decrements into the adjacent storyboard byte.
            if (StoryboardUnlocked != byte.MaxValue) StoryboardUnlocked++;
            return;
        }
        if ((uint)index >= counts.Length) throw new InvalidDataException("Completion index lies outside the original campaign.");
        if (counts[index] != byte.MaxValue) counts[index]++;
    }
}

/// <summary>Requests returned to actual application owners; SaveGameRequested means the original confirmation flow.</summary>
public readonly record struct CampaignCompletionResult(int? CompletedLevelIndex, bool FinalLevelCompleted,
    bool SaveGameRequested, byte? NewlyUnlockedCostume);

/// <summary>Original status3 counter selection, PShell_MaybeUnlockStuff and save-confirmation gates.</summary>
public static class CampaignCompletionRules
{
    public static CampaignCompletionResult Complete(CampaignProgress progress, OriginalLevelCatalog catalog,
        string targetMissionName, int difficulty, uint renderTest)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(targetMissionName);
        bool final = SourceNameEquals(targetMissionName, "l8a7_t");
        int found = catalog.FindIndex(final ? "l8a6_t" : targetMissionName);
        if (final && found < 0) throw new InvalidDataException("The original final mission's l8a6_t counter is missing.");
        int? completed = null;
        if (found >= 0) completed = final ? found : found - 1;
        if (completed is int index) progress.IncrementCompletion(index);
        byte? unlock = ApplyUnlocks(progress, catalog, difficulty);
        bool save = !final && (renderTest & 0x80) == 0 &&
            (difficulty == 0 || catalog.Find(targetMissionName) is { } level && (level.FlowFlags & 2) != 0);
        return new(completed, final, save, unlock);
    }

    public static byte? ApplyUnlocks(CampaignProgress progress, OriginalLevelCatalog catalog, int difficulty)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(catalog);
        byte? newlyUnlocked = null;
        if (progress.CompletionCounts.All(count => count != 0))
        {
            progress.StoryboardUnlocked = 1;
            if ((progress.CostumeUnlockFlags & 4) == 0) newlyUnlocked = 2;
            progress.CostumeUnlockFlags |= 4;
            progress.CharacterViewerUnlockFlags |= 0x1000000;
            if (difficulty == 3)
            {
                if ((progress.CostumeUnlockFlags & 8) == 0) newlyUnlocked = 3;
                progress.CostumeUnlockFlags |= 8;
            }
        }
        if (progress.CollectedComicMask == uint.MaxValue) progress.CostumeUnlockFlags |= 2;
        if (Counter("l4a1_t") != 0) progress.CostumeUnlockFlags |= 0x40;
        if (Counter("l6a4_t") != 0) progress.CostumeUnlockFlags |= 0x80;
        if (progress.CompletionCounts.All(count => count >= 2))
        {
            if ((progress.CostumeUnlockFlags & 0x10) == 0) newlyUnlocked = 4;
            progress.CostumeUnlockFlags |= 0x10;
        }
        if (newlyUnlocked is byte costume) progress.LastUnlockedCostume = costume;
        return newlyUnlocked;

        byte Counter(string mission)
        {
            int index = catalog.FindIndex(mission);
            if (index < 0) throw new InvalidDataException($"Original costume unlock milestone {mission} is missing.");
            return progress.CompletionCounts[index];
        }
    }

    private static bool SourceNameEquals(string left, string right)
    {
        if (left.Length != right.Length) return false;
        for (int index = 0; index < left.Length; index++)
            if (LowerAscii(left[index]) != LowerAscii(right[index])) return false;
        return true;
        static char LowerAscii(char character) => character is >= 'A' and <= 'Z' ? (char)(character + 32) : character;
    }
}
