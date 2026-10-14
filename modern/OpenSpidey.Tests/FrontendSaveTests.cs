using System.Text.Json;
using OpenSpidey.Core.Frontend;
using OpenSpidey.Core.Saves;

internal static class FrontendSaveTests
{
    public static void Run(Action<bool, string> assert, string? assets = null)
    {
        using Stream stream = typeof(FrontendSaveTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.frontend-save.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        Checksums(root.GetProperty("checksum"), assert);
        Slots(root.GetProperty("slots"), assert);
        Writes(root.GetProperty("write"), assert);
        Loads(root.GetProperty("load"), assert);
        Merges(root.GetProperty("merge"), assert);
        Menus(root.GetProperty("menu"), assert);
        Names(assert);
        MainMenuRules(assert);
        ExpandingBox(assert);
        MenuZoom(assert);
        if (assets is null) return;
        byte[] image = File.ReadAllBytes(Path.Combine(assets, "SpideyPC.exe"));
        OriginalFrontendData data = OriginalFrontendData.Read(image);
        Cheats(root.GetProperty("cheat"), data, assert);
        ExecutableTables(data, image, assert);
        RoundTrip(data, assert);
    }

    private static void Checksums(JsonElement cases, Action<bool, string> assert)
    {
        int index = 0;
        foreach (JsonElement test in cases.EnumerateArray())
        {
            var save = new OriginalSaveGame(Convert.FromHexString(test.GetProperty("save").GetString()!));
            assert(save.CalculateChecksum() == test.GetProperty("expected").GetUInt32(),
                $"Save checksum {index++}: the 0x49F12D checksum loop: rotate-add over 46 dwords with the low bit set.");
        }
    }

    private static void Slots(JsonElement cases, Action<bool, string> assert)
    {
        int index = 0;
        foreach (JsonElement test in cases.EnumerateArray())
        {
            byte[] head = new byte[OriginalSaveFile.HeadSize];
            Convert.FromHexString(test.GetProperty("slots").GetString()!).CopyTo(head, OriginalSaveFile.SlotOffset);
            OriginalSaveFile file = OriginalSaveFile.FromHead(head, new byte[32], new byte[128]);
            var menu = new OriginalMenu(90, 70, 0, 256, 256, 15);
            OriginalShellRules.AddGameSlots(menu, file, "read error", "---- empty ----");
            int[] kinds = Ints(test.GetProperty("expected").GetProperty("kinds"));
            int[] dim = Ints(test.GetProperty("expected").GetProperty("dim"));
            bool same = menu.Entries.Count == kinds.Length;
            for (int slot = 0; same && slot < kinds.Length; slot++)
            {
                OriginalMenuEntry entry = menu.Entries[slot];
                int kind = entry.Name == "read error" ? 1 : entry.Name == "---- empty ----" ? 2 : 0;
                same = kind == kinds[slot] && entry.SelectedTop.R == (dim[slot] == 0 ? 0x80 : dim[slot]) &&
                    (kind != 0 || entry.Name == file.Slot(slot).SaveName);
            }
            assert(same, $"Save slots {index++}: Shell_AddGameSlots 0x49F220 labels and dim colours match the original slot list.");
        }
    }

    private static void Writes(JsonElement cases, Action<bool, string> assert)
    {
        int index = 0;
        foreach (JsonElement test in cases.EnumerateArray())
        {
            JsonElement expected = test.GetProperty("expected");
            byte[] after = Convert.FromHexString(expected.GetProperty("head").GetString()!);
            OriginalSaveFile file = OriginalSaveFile.FromHead(Convert.FromHexString(test.GetProperty("head").GetString()!),
                after[0x60..0x80], after[0x80..0x100]);
            byte[] written = file.Write();
            byte[] original = Convert.FromHexString(expected.GetProperty("file").GetString()!);
            int stored = 4 + OriginalSaveFile.HeadSize;
            assert(expected.GetProperty("result").GetInt32() == 0 && expected.GetProperty("blocks").GetInt32() * 512 == written.Length &&
                original.Length == written.Length && original.AsSpan(0, stored).SequenceEqual(written.AsSpan(0, stored)) &&
                file.Head.SequenceEqual(after),
                $"Card write {index++}: Card_Write 0x46C500 header refresh, size word and six-block file match the original bytes.");
        }
    }

    private static void Loads(JsonElement cases, Action<bool, string> assert)
    {
        int index = 0;
        foreach (JsonElement test in cases.EnumerateArray())
        {
            JsonElement expected = test.GetProperty("expected");
            byte[] before = Convert.FromHexString(test.GetProperty("before").GetString()!);
            byte[] after = Convert.FromHexString(expected.GetProperty("head").GetString()!);
            byte[] clut = new byte[32], icon = new byte[128];
            if (expected.GetProperty("result").GetInt32() == 0) { clut = after[0x60..0x80]; icon = after[0x80..0x100]; }
            OriginalSaveFile file = OriginalSaveFile.FromHead(before, clut, icon);
            byte[] data = Convert.FromHexString(test.GetProperty("file").GetString()!);
            bool exists = test.GetProperty("exists").GetInt32() != 0;
            int result = expected.GetProperty("result").GetInt32();
            if (data.Length is > 0 and < 4)
            {
                bool threw = false;
                try { file.Load(data, exists); }
                catch (InvalidDataException) { threw = true; }
                assert(threw && result == 2 && file.Head.SequenceEqual(before),
                    $"Card load {index++}: a size word shorter than four bytes depends on uninitialized memory in the original and is rejected explicitly.");
                continue;
            }
            OriginalCardLoadResult loaded = file.Load(data, exists);
            assert((int)loaded == result && file.Head.SequenceEqual(after),
                $"Card load {index++}: Card_Load 0x46C7F0 result code and head copy/header refresh match the original.");
        }
    }

    private static void Merges(JsonElement cases, Action<bool, string> assert)
    {
        int index = 0;
        foreach (JsonElement test in cases.EnumerateArray())
        {
            byte[] left = Convert.FromHexString(test.GetProperty("left").GetString()!);
            byte[] right = Convert.FromHexString(test.GetProperty("right").GetString()!);
            OriginalHighScores.Merge(left, right, test.GetProperty("lower").GetInt32());
            assert(left.AsSpan().SequenceEqual(Convert.FromHexString(test.GetProperty("expected").GetProperty("merged").GetString()!)),
                $"Record merge {index++}: Merge 0x49EFF0 duplicate skip, signed compare direction and shift-down insert match.");
        }
    }

    private static void Menus(JsonElement cases, Action<bool, string> assert)
    {
        int index = 0;
        foreach (JsonElement test in cases.EnumerateArray())
        {
            int lines = test.GetProperty("lines").GetInt32();
            uint enabled = test.GetProperty("enabled").GetUInt32(), blocked = test.GetProperty("blocked").GetUInt32();
            var menu = new OriginalMenu(256, 40, 0, 256, 200, 16);
            for (int line = 0; line < lines; line++)
            {
                OriginalMenuEntry entry = menu.Add("entry");
                entry.Enabled = (enabled >> line & 1) != 0;
                entry.Blocked = (int)(blocked >> line & 1);
            }
            menu.VisibleLines = test.GetProperty("visible").GetInt32();
            menu.Wrap = test.GetProperty("wrap").GetInt32() != 0;
            menu.RepeatRate = test.GetProperty("rate").GetInt32();
            menu.SetLine(test.GetProperty("start").GetInt32());
            int timer = test.GetProperty("timer").GetInt32();
            var frames = new List<int>();
            foreach (JsonElement input in test.GetProperty("input").EnumerateArray())
            {
                int held = input.GetInt32();
                bool sound = menu.Update(mask => (held & mask) != 0, ref timer);
                frames.AddRange([menu.Line, menu.CursorLine, timer, sound ? 1 : 0]);
            }
            var scales = new List<int>();
            foreach (OriginalMenuEntry entry in menu.Entries) scales.AddRange([entry.Value, entry.Target]);
            JsonElement expected = test.GetProperty("expected");
            assert(frames.SequenceEqual(Ints(expected.GetProperty("frames"))) && scales.SequenceEqual(Ints(expected.GetProperty("scales"))),
                $"Menu update {index++} got {string.Join(",", frames)} / {string.Join(",", scales)} want {expected}: CMenu::Update 0x440600 skip, wrap, repeat, scroll window, move sound and scale easing match.");
        }
    }

    private static void Cheats(JsonElement cases, OriginalFrontendData data, Action<bool, string> assert)
    {
        int index = 0;
        foreach (JsonElement test in cases.EnumerateArray())
        {
            string code = test.GetProperty("code").GetString()!.Replace('_', ' ');
            var save = new OriginalSaveGame(Convert.FromHexString(test.GetProperty("save").GetString()!));
            int[] input = Ints(test.GetProperty("flags"));
            var flags = new OriginalCheatFlags
            {
                LevelSelect = input[0], UnlimitedWebbing = input[1], PulsatingHead = input[2], Invulnerable = input[3],
                StickSpidey = input[4], ToonSpidey = input[5], WhatIf = (byte)input[6], JoelJewett = input[7]
            };
            int result = OriginalCheats.Activate(code, data.Cheats, save, flags, null);
            JsonElement expected = test.GetProperty("expected");
            int[] after = [flags.LevelSelect, flags.UnlimitedWebbing, flags.PulsatingHead, flags.Invulnerable,
                flags.StickSpidey, flags.ToonSpidey, flags.WhatIf, flags.JoelJewett];
            assert(result == expected.GetProperty("result").GetInt32() && after.SequenceEqual(Ints(expected.GetProperty("flags"))) &&
                save.Bytes.SequenceEqual(Convert.FromHexString(expected.GetProperty("save").GetString()!)),
                $"Cheat {index++}: PShell_ActivateCheat 0x47C440 lookup, RULUR rejection and ActivateCheat 0x47C240 effects match.");
        }
    }

    private static void Names(Action<bool, string> assert)
    {
        var entry = new OriginalNameEntry("BOB");
        entry.Type('S');
        entry.Type('a');
        entry.Type(' ');
        entry.Backspace();
        assert(entry.Display == "S.B....." && entry.Length == 1,
            "Name entry: Shell_InputName 0x48DC10 pads short names with dots, overwrites from column 0 keeping later letters, and accepts only A-Z/0-9/space.");
        (bool finished, string? result, string? replacement) = entry.Accept(["dbu"], ["flower"], _ => 0);
        assert(finished && result == "S" && replacement is null, "Name entry: accept trims at the first dot.");
        var rude = new OriginalNameEntry("");
        foreach (char key in "XCATX") rude.Type(key);
        (finished, _, replacement) = rude.Accept(["dbu"], ["flower"], _ => 0);
        assert(!finished && replacement == "flower..", "Name entry: encoded rude words (letters +1) are replaced by a dot-padded good word.");
        assert(OriginalNameEntry.ContainsSubString("ab", "") && !OriginalNameEntry.ContainsSubString("ab", "abc"),
            "Name entry: Shell_ContainsSubString matches an empty needle and never reads past the text.");
    }

    private static void MainMenuRules(Action<bool, string> assert)
    {
        OriginalMainMenuEntry[] table =
        [
            new(105, 46, "continue", 2), new(86, 98, "new game", 1), new(86, 153, "options", 4), new(120, 202, "quit", 18),
            new(400, 46, "training", 5), new(430, 98, "High Scores", 6), new(430, 153, "special", 13), new(400, 202, "gallery", 7)
        ];
        var menu = new OriginalMainMenu(table, 1);
        bool up = menu.Navigate(mask => (mask & 0x1001) != 0, _ => false, false, true);
        assert(up && menu.Line == 7, "Main menu: up from new game skips continue without a game and wraps to gallery.");
        menu.Navigate(_ => false, mask => mask == ShellTriggers.Left, false, true);
        assert(menu.Line == 3, "Main menu: left steps one column back.");
        menu.Hover(0);
        assert(!menu.Accepts(false, 0) && menu.Accepts(true, 0), "Main menu: continue is accepted only with field_78 set.");
        (int level, bool all) = OriginalMainMenu.ContinueLevel(new byte[34] { 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 });
        assert(level == 2 && !all, "Main menu: continue shows the first level with the lowest completion count.");
    }

    private static void ExecutableTables(OriginalFrontendData data, byte[] image, Action<bool, string> assert)
    {
        assert(data.MainMenu.Select(entry => entry.Text).SequenceEqual(["continue", "new game", "options", "quit", "training",
            "High Scores", "special", "gallery"]) && data.MainMenu.Select(entry => entry.Type).SequenceEqual([2, 1, 4, 18, 5, 6, 13, 7]),
            "Executable: Shell_MainMenu table 0x552AA8 labels and EShellResult types.");
        assert(data.Cheats.Count == 24 && data.Cheats[4].Code == "RULUR" && data.Cheats[0].Code == "WEAKNESS",
            "Executable: gCheats 0x5513E0 has 24 code/description pairs with RULUR at index 4.");
        assert(data.DefaultSave.LevelName == "l1a1_t" && data.DefaultSave.RestartPointName == "Re_Start_death" &&
            data.Challenges[0].HigherIsBetter && !data.Challenges[16].HigherIsBetter && data.Challenges.Count == OriginalHighScores.ChallengeCount &&
            data.Challenges.All(challenge => challenge.Title.Length != 0),
            "Executable: default gSaveGame 0x550E10 and gChallenges 0x551118 decode.");
        assert(data.Text[OriginalFrontendData.ReadErrorText] == "read error" && data.Text[OriginalFrontendData.EmptySlotText] == "---- empty ----" &&
            data.Text[OriginalFrontendData.ReallyQuitText].Length != 0,
            "Executable: localized slot and pause menu text slots decode.");
        (IReadOnlyList<string> bad, IReadOnlyList<string> good) = OriginalNameEntry.ReadWordLists(image);
        assert(bad.Count == 29 && good.Count == 30 && good[0] == "flower" && bad.All(word => word.Length != 0),
            "Executable: Shell_InputName word lists 0x552744 (29 encoded) and 0x5527B8 (30).");
    }

    private static void RoundTrip(OriginalFrontendData data, Action<bool, string> assert)
    {
        OriginalSaveGame save = data.DefaultSave.Clone();
        save.BeginNewGame(data.DefaultSave, 2);
        save.SaveName = "PETER";
        OriginalSaveFile file = data.CreateSaveFile();
        OriginalHighScores records = data.DefaultRecords.Clone();
        OriginalShellRules.SaveSlot(file, 3, save, records, data.Challenges);
        byte[] bytes = file.Write();
        OriginalSaveFile reread = data.CreateSaveFile();
        bool loaded = reread.Load(bytes, true) == OriginalCardLoadResult.Loaded &&
            OriginalShellRules.LoadSlot(reread, 3, data.DefaultSave.Clone(), data.DefaultRecords.Clone(), data.Challenges, out OriginalSaveGame back);
        assert(loaded && reread.Slot(3).SlotState == OriginalSaveSlotState.Valid && reread.Slot(0).SlotState == OriginalSaveSlotState.Empty &&
            reread.Slot(3).Bytes.SequenceEqual(save.Bytes) && reread.Slot(3).LevelName == "l1a1_t" && reread.Slot(3).Difficulty == 2,
            "Save round trip: new game sealed into slot 3 reloads through Card_Load with a valid checksum.");
    }

    private static void ExpandingBox(Action<bool, string> assert)
    {
        var box = OriginalExpandingBox.Create(32, 50, 45, 34, 0, 0, 30, 15, 0);
        box.Display();
        assert(!box.Finished && box.CurrentWidth == 30 && box.CurrentHeight == 15 &&
            box.LastDraw == new MenuBoxDrawCall(39, 60, 30, 15, 1, 0, 0, 28),
            "CExpandingBox::Display 0x47AF10 grows width and height toward the ctor targets each frame.");
        box.Display();
        assert(!box.Finished && box.CurrentWidth == 45 && box.CurrentHeight == 30,
            "CExpandingBox::Display clamps growth at the target width and height separately.");
        box.Display();
        assert(box.Finished && box.CurrentWidth == 45 && box.CurrentHeight == 34 &&
            box.LastDraw == new MenuBoxDrawCall(32, 50, 45, 34, 1, 0, 0, 28),
            "CExpandingBox::Display sets field_30 once both dimensions reach their targets.");
        assert(box.ScrollBarHitTest(32, 50) == 0,
            "CExpandingBox::ScrollBarHitTest 0x47AFB0 returns 0 while the scrollbar flag is clear.");
        box.Scrollbar = true;
        assert(box.ScrollBarHitTest(10, 50) == 0 && box.ScrollBarHitTest(25, 47) == 1,
            "CExpandingBox::ScrollBarHitTest uses anchor x/y and current height once the zoom finished.");
    }

    private static void MenuZoom(Action<bool, string> assert)
    {
        var menu = new OriginalMenu(256, 100, 0, 256, 256, 16);
        menu.Add("one");
        menu.Add("two");
        menu.CentreY();
        menu.Zoom(0);
        assert(!menu.FinishedZooming && !menu.Lines().Any(),
            "CMenu::Display hides entries until CMenu::FinishedZooming (front.cpp 0x4401b0).");
        int typeZeroFrames = 0;
        while (!menu.FinishedZooming && typeZeroFrames++ < 64) menu.AdvanceZoomBox();
        assert(menu.FinishedZooming && menu.Lines().Count() == 2 && typeZeroFrames is >= 2 and <= 64,
            "CMenu::FinishedZooming reads CExpandingBox field_30; type-0 width jumps in one step but height still eases in.");
        menu.KillBox();
        assert(menu.FinishedZooming, "CMenu::KillBox clears ptr_to so FinishedZooming is true without faking field_30.");
        menu.Zoom(1);
        int frames = 0;
        while (!menu.FinishedZooming && frames++ < 32) menu.AdvanceZoomBox();
        assert(menu.FinishedZooming && frames is > 1 and <= 32,
            "CMenu::Zoom type 1 uses the 30-by-15 expanding steps from front.cpp.");
        menu.Zoom(2);
        menu.AdvanceZoomBox();
        assert(menu.ZoomBox!.Scrollbar,
            "CMenu::Display sets field_24 on the zoom box when mZoomBoxType is 2 (description panel).");
    }

    private static int[] Ints(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt32()).ToArray();
}
