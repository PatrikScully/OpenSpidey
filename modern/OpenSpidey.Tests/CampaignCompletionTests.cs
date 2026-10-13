using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Core.Missions;

internal static class CampaignCompletionTests
{
    public static void Run(Action<bool, string> assert, string? assets = null)
    {
        using Stream stream = typeof(CampaignCompletionTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.campaign-completion.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement records = document.RootElement.GetProperty("levels");
        byte[] image = BuildImage(records);
        OriginalLevelCatalog catalog = OriginalLevelCatalog.Read(image);
        VerifyCatalog(catalog, records, assert);
        int index = 0;
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            string name = test.GetProperty("name").GetString()!;
            JsonElement expected = test.GetProperty("expected");
            int found = expected.GetProperty("lookup").GetInt32();
            int? expectedIndex = found < 0 ? null : found;
            assert(catalog.FindIndex(name) == found && catalog.Find(name)?.Index == expectedIndex,
                $"Campaign lookup {index}: original byte1/byte3 matching, first-record order and sentinel match both PC lookup functions.");
            var progress = new CampaignProgress(ReadBytes(test.GetProperty("counts")))
            {
                StoryboardUnlocked = test.GetProperty("story").GetByte(),
                LastUnlockedCostume = test.GetProperty("popup").GetByte(),
                CostumeUnlockFlags = test.GetProperty("costumes").GetUInt32(),
                CharacterViewerUnlockFlags = test.GetProperty("characters").GetUInt32(),
                CollectedComicMask = test.GetProperty("comics").GetUInt32()
            };
            int mode = test.GetProperty("mode").GetInt32();
            if (mode == 0)
            {
                CampaignCompletionResult result = CampaignCompletionRules.Complete(progress, catalog, name,
                    test.GetProperty("difficulty").GetInt32(), test.GetProperty("render").GetUInt32());
                int[] events = ReadInts(expected.GetProperty("events"));
                assert(result.FinalLevelCompleted == (expected.GetProperty("final").GetInt32() != 0) &&
                    result.SaveGameRequested == events.Contains(4) &&
                    result.NewlyUnlockedCostume == OptionalUnlock(expected),
                    $"Completion {index}: final-menu return, source save-confirmation gate and popup precedence match the actual PC status3 block.");
                int[] calls = result.SaveGameRequested ? [1, 0, 2, 3, 4] : [1, 0, 2, 3];
                assert(events.SequenceEqual(calls),
                    $"Completion {index}: source cleanup/fade precede actual unlock evaluation, and save confirmation follows it only on nonfinal eligible paths.");
            }
            else if (mode == 1)
                assert(CampaignCompletionRules.ApplyUnlocks(progress, catalog, test.GetProperty("difficulty").GetInt32()) == OptionalUnlock(expected),
                    $"Unlock {index}: full original PShell policy selects the same last newly unlocked costume.");
            assert(progress.CompletionCounts.SequenceEqual(ReadBytes(expected.GetProperty("counts"))) &&
                progress.StoryboardUnlocked == expected.GetProperty("story").GetByte() &&
                progress.LastUnlockedCostume == expected.GetProperty("popup").GetByte() &&
                progress.CostumeUnlockFlags == expected.GetProperty("costumes").GetUInt32() &&
                progress.CharacterViewerUnlockFlags == expected.GetProperty("characters").GetUInt32() &&
                progress.CollectedComicMask == expected.GetProperty("comics").GetUInt32(),
                $"Campaign {index}: all34 saturated counters, adjacent storyboard edge, raw popup byte and complete32-bit masks match actual PC writes.");
            assert(expected.GetProperty("guard").GetBoolean(), $"Campaign {index}: original policy preserves unrelated saved bytes.");
            index++;
        }
        VerifyRepeatedUnlocks(catalog, assert);
        Reject(() => OriginalLevelCatalog.Read(image.AsMemory(0, 64)));
        byte[] corrupt = image.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(corrupt.AsSpan(0x718 + 4), 0xFFFFFFFF);
        Reject(() => OriginalLevelCatalog.Read(corrupt));
        corrupt = image.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(corrupt.AsSpan(0x718 + 33 * 20 + 4), 0x550000);
        Reject(() => OriginalLevelCatalog.Read(corrupt));
        corrupt = image.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(corrupt.AsSpan(0x718), 0x550000 + 0x3000 - 1);
        corrupt[^1] = 1;
        Reject(() => OriginalLevelCatalog.Read(corrupt));
        if (assets is not null)
            VerifyCatalog(OriginalLevelCatalog.Read(File.ReadAllBytes(Path.Combine(assets, "SpideyPC.exe"))), records, assert);

        void Reject(Action read)
        {
            try { read(); assert(false, "Malformed original campaign metadata must be rejected before reading outside stored PE bytes."); }
            catch (Exception exception) when (exception is InvalidDataException or OverflowException)
            { assert(true, "Campaign import rejects truncated images, invalid pointers, changed campaign size and unterminated text."); }
        }
    }

    private static void VerifyCatalog(OriginalLevelCatalog catalog, JsonElement records, Action<bool, string> assert)
    {
        assert(catalog.Levels.Count == 34, "The original campaign has 34 records before its empty-name sentinel, independent of the source capacity41.");
        foreach (JsonElement reference in records.EnumerateArray())
        {
            int index = reference.GetProperty("index").GetInt32();
            var expected = new OriginalLevelDefinition(index,
                Encoding.Latin1.GetString(ReadBytes(reference.GetProperty("titleBytes"))),
                Encoding.Latin1.GetString(ReadBytes(reference.GetProperty("missionBytes"))), reference.GetProperty("flags").GetByte(),
                reference.GetProperty("comicCovers").GetUInt32(), reference.GetProperty("characters").GetUInt32());
            assert(catalog.Levels[index] == expected,
                $"Catalog {index}: original display text, mission name, flow byte and full comic/character masks match independently mapped PC metadata.");
        }
    }

    private static void VerifyRepeatedUnlocks(OriginalLevelCatalog catalog, Action<bool, string> assert)
    {
        byte[] input = Enumerable.Repeat((byte)2, 34).ToArray();
        var progress = new CampaignProgress(input) { LastUnlockedCostume = 187, CollectedComicMask = uint.MaxValue };
        input[0] = 0;
        assert(progress.CompletionCounts[0] == 2, "Campaign progress owns its counter storage independently of the imported save buffer.");
        assert(CampaignCompletionRules.ApplyUnlocks(progress, catalog, 3) == 4 && progress.LastUnlockedCostume == 4 &&
            progress.CostumeUnlockFlags == 0xDE && progress.CharacterViewerUnlockFlags == 0x1000000 && progress.StoryboardUnlocked == 1,
            "All-gold completion selects costume4 after newly eligible costumes2/3, retaining both milestone and all-comics costume flags.");
        assert(CampaignCompletionRules.ApplyUnlocks(progress, catalog, 3) is null && progress.LastUnlockedCostume == 4,
            "Repeated unlock evaluation preserves the last source popup byte and reports no repeated costume unlock.");
        CampaignCompletionResult final = CampaignCompletionRules.Complete(progress, catalog, "L8A7_T", 0, 0);
        assert(final.CompletedLevelIndex == 33 && final.FinalLevelCompleted && !final.SaveGameRequested && progress.CompletionCounts[33] == 3,
            "The final authored target uses l8a6's own counter and returns to menu before the ordinary easy-difficulty save request.");
        assert(catalog.FindIndex("L1A2a_T") == 1 && catalog.FindIndex("x1z3") == 2,
            "Original lookup aliases What-If and arbitrary prefix bytes by chapter/area key without modern full-name substitution.");
        var first = new CampaignProgress { StoryboardUnlocked = 254 };
        CampaignCompletionResult edge = CampaignCompletionRules.Complete(first, catalog, "l1a1_t", 1, 0);
        assert(edge.CompletedLevelIndex == -1 && first.StoryboardUnlocked == 255 && first.CompletionCounts.All(count => count == 0),
            "The release source's target-index0 edge saturates the adjacent storyboard byte without inventing a campaign counter.");
    }

    private static byte? OptionalUnlock(JsonElement expected)
    {
        int value = expected.GetProperty("newUnlock").GetInt32();
        return value < 0 ? null : (byte)value;
    }

    private static byte[] ReadBytes(JsonElement array) => array.EnumerateArray().Select(value => value.GetByte()).ToArray();
    private static int[] ReadInts(JsonElement array) => array.EnumerateArray().Select(value => value.GetInt32()).ToArray();

    private static byte[] BuildImage(JsonElement records)
    {
        byte[] image = new byte[0x4200];
        Half(0, 0x5A4D); Word(60, 0x80); Word(0x80, 0x4550); Half(0x84, 0x14C); Half(0x86, 2);
        Half(0x94, 0xE0); Half(0x98, 0x10B); Word(0xB4, 0x400000);
        Word(0x178 + 12, 0x14A000); Word(0x178 + 16, 0x1000); Word(0x178 + 20, 0x200);
        Word(0x1A0 + 12, 0x150000); Word(0x1A0 + 16, 0x3000); Word(0x1A0 + 20, 0x1200);
        int text = 8;
        foreach (JsonElement record in records.EnumerateArray())
        {
            int offset = 0x718 + record.GetProperty("index").GetInt32() * 20;
            Word(offset, String(record.GetProperty("titleBytes")));
            Word(offset + 4, String(record.GetProperty("missionBytes")));
            image[offset + 8] = record.GetProperty("flags").GetByte();
            Word(offset + 12, record.GetProperty("comicCovers").GetUInt32());
            Word(offset + 16, record.GetProperty("characters").GetUInt32());
        }
        Word(0x718 + 34 * 20 + 4, 0x550000);
        // Original records after the sentinel are unrelated data. Reading them would be an import error.
        Word(0x718 + 35 * 20 + 4, uint.MaxValue);
        return image;

        uint String(JsonElement values)
        {
            uint address = checked(0x550000u + (uint)text);
            byte[] bytes = ReadBytes(values); bytes.CopyTo(image, 0x1200 + text); text += bytes.Length + 1;
            return address;
        }
        void Half(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(offset), value);
        void Word(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset), value);
    }
}
