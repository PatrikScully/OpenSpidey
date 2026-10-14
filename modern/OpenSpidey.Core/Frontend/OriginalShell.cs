using System.Text;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Core.Saves;

namespace OpenSpidey.Core.Frontend;

/// <summary>PCSHELL_CheckTriggers 0x50C180 masks used by the shell.</summary>
public static class ShellTriggers
{
    public const int Up = 0x1001, Down = 0x2002, UpOrDown = 0x3003, Left = 0x4004, Right = 0x8008, LeftOrRight = 0xC00C;
    public const int Enter = 0x10, Accept = 0x10010, Back = 0x20220, Title = 0x40010, PauseToggle = 0x40040;
}

/// <summary>Shell_MainMenu 0x493990 keyboard/pad navigation, inline in the original and so ported from its source.</summary>
public sealed class OriginalMainMenu
{
    private readonly IReadOnlyList<OriginalMainMenuEntry> entries;
    private int moveRepeat;
    public int Line { get; private set; }
    public OriginalMainMenuEntry Selected => entries[Line];

    public OriginalMainMenu(IReadOnlyList<OriginalMainMenuEntry> entries, int initialType)
    {
        if (entries is not { Count: 8 }) throw new ArgumentException("The original main menu has eight entries.", nameof(entries));
        this.entries = entries;
        int line = 0;
        while (line < 8 && entries[line].Type != initialType) line++;
        Line = line >= 8 ? 2 : line;
    }

    /// <summary>Shell_DoShell's first request: continue (2) with a game, otherwise new game (1).</summary>
    public static int InitialType(OriginalSaveGame save) => save.HasGame != 0 ? 2 : 1;

    /// <summary>Shell_MainMenu entry: gSaveGame's level becomes the first lowest-count level on every menu visit.</summary>
    public static (int Index, bool AllComplete) PrepareContinue(OriginalSaveGame save, OriginalLevelCatalog levels)
    {
        (int index, bool all) = ContinueLevel(save.CompletionCounts);
        save.LevelName = levels.Levels[index].MissionName;
        return (index, all);
    }

    /// <summary>The continue label: the level display name unless every level is complete and the first is selected.</summary>
    public static (int Y, string Text) ContinueLabel(int index, bool allComplete, OriginalLevelCatalog levels) =>
        !allComplete || index != 0 ? (178, levels.Levels[index].DisplayName) : (162, "costume viewer");

    /// <summary>One frame of the nav: block; held masks repeat, triggered left/right step columns. Returns true for sound 0x29.</summary>
    public bool Navigate(Func<int, bool> held, Func<int, bool> triggered, bool hasGame, bool cardPresent)
    {
        int v20 = Line, v21 = Line, start = Line;
        if (held(ShellTriggers.UpOrDown))
        {
            if (moveRepeat == 0 || moveRepeat > 20 && (moveRepeat & 1) == 0)
            {
                if (held(ShellTriggers.Up))
                {
                    if (v20 == 0 || (--v20 == 0 && !hasGame)) v20 = 7;
                }
                if (held(ShellTriggers.Down))
                {
                    if (v20 >= 7) v20 = hasGame ? 0 : 1;
                    else v20++;
                }
            }
            moveRepeat++;
            v21 = start;
        }
        else moveRepeat = 0;
        if (triggered(ShellTriggers.Right) && v20 < 4) v20 += 4;
        if (triggered(ShellTriggers.Left) && v20 >= 4)
        {
            v20 -= 4;
            if (v20 == 2 && !cardPresent) v20++;
            else if (v20 == 0 && !hasGame) v20++;
        }
        bool sound = false;
        if (v20 != 0 || hasGame) sound = v21 != v20;
        else v20 = v21 != 0 ? v21 : 1;
        Line = v20;
        return sound;
    }

    public void Hover(int line)
    {
        if ((uint)line >= 8) throw new ArgumentOutOfRangeException(nameof(line));
        Line = line;
    }

    /// <summary>Accept rule at 0x4941xx: renderTest bit 0x80 is the demo build that only offers new game and continue.</summary>
    public bool Accepts(bool hasGame, uint renderTest)
    {
        int type = Selected.Type;
        if ((renderTest & 0x80) != 0) return type == 1 || type == 2 && hasGame;
        return type != 2 || hasGame;
    }

    /// <summary>Continue label: first level with the lowest completion count, or "costume viewer" once all are complete.</summary>
    public static (int Index, bool AllComplete) ContinueLevel(ReadOnlySpan<byte> counts)
    {
        if (counts.Length != OriginalLevelCatalog.CampaignLevelCount) throw new ArgumentException("34 completion counts are required.", nameof(counts));
        int lowest = 1000000;
        foreach (byte count in counts) lowest = Math.Min(lowest, count);
        int index = counts.IndexOf((byte)lowest);
        return (index, lowest != 0);
    }
}

public static class OriginalShellRules
{
    public const int AcceptSound = 0x1F, DenySound = 0x1B, BackSound = 0x23, CheatSound = 0x1D;
    public static readonly string[] DifficultyEntries = ["kid mode", "easy", "normal", "hard"];

    /// <summary>Shell_Difficulty 0x4A0F40 menu: CMenu(256,0,0,256,256,16), centred then forced to y93, normal selected.</summary>
    public static OriginalMenu DifficultyMenu(Func<string, int>? measure)
    {
        var menu = new OriginalMenu(256, 0, 0, 256, 256, 16) { MeasureText = measure };
        foreach (string entry in DifficultyEntries) menu.Add(entry);
        menu.CentreY();
        menu.SetLine(2);
        menu.Y = 93;
        return menu;
    }

    /// <summary>Shell_Difficulty warning gate: shown only when some completion count is nonzero.</summary>
    public static bool DifficultyWarning(OriginalSaveGame save) => save.CompletionCounts.IndexOfAnyExcept((byte)0) >= 0;

    /// <summary>Shell_Difficulty accept: DifficultyLevel, the save difficulty and the 0x60CFC7 kid-mode practice byte.</summary>
    public static (int Difficulty, bool KidMode) ApplyDifficulty(OriginalSaveGame save, int line)
    {
        if ((uint)line > 3) throw new ArgumentOutOfRangeException(nameof(line));
        save.Difficulty = (sbyte)line;
        return (line, line == 0);
    }

    /// <summary>Shell_Special 0x49E660 entries; level select appears with the cheat flag or any completed level.</summary>
    public static OriginalMenu SpecialMenu(OriginalSaveGame save, OriginalCheatFlags flags, Func<string, int>? measure)
    {
        var menu = new OriginalMenu(256, 0, 0, 256, 256, 16) { MeasureText = measure };
        menu.Add("costumes");
        menu.Add("view credits");
        menu.Add("cheats");
        if (flags.LevelSelect != 0 || DifficultyWarning(save)) menu.Add("level select");
        menu.CentreY();
        return menu;
    }

    /// <summary>Shell_Options 0x4970B0; joystick configuration is turned off without controller buttons.</summary>
    public static OriginalMenu OptionsMenu(bool controllerButtons, Func<string, int>? measure)
    {
        var menu = new OriginalMenu(254, 97, 0, 256, 256, 16) { MeasureText = measure };
        foreach (string entry in new[] { "keyboard configuration", "joystick configuration", "music and sound", "display options", "file system" })
            menu.Add(entry);
        if (!controllerButtons) menu.EntryOff("joystick configuration");
        return menu;
    }

    /// <summary>Shell_MemoryCard 0x49EBA0.</summary>
    public static OriginalMenu FileSystemMenu(Func<string, int>? measure)
    {
        var menu = new OriginalMenu(256, 0, 0, 256, 256, 16) { MeasureText = measure };
        menu.Add("load game data");
        menu.Add("Save game data");
        menu.CentreY();
        return menu;
    }

    /// <summary>Shell_SFXMusic 0x4977D0 entries, CMenu(270,90,2,256,256,20) without wrap.</summary>
    public static OriginalMenu SoundMenu(Func<string, int>? measure)
    {
        var menu = new OriginalMenu(270, 90, 2, 256, 256, 20) { MeasureText = measure, Wrap = false };
        foreach (string entry in new[] { "music and sfx level", "voice level", "movie level", "audio", "initial settings" }) menu.Add(entry);
        return menu;
    }

    /// <summary>Shell_LevelSelect 0x4A0AD0 menu: shown levels, 9 visible lines, no wrap, single-frame repeat.</summary>
    public static OriginalMenu LevelSelectMenu(OriginalSaveGame save, OriginalLevelCatalog levels, OriginalCheatFlags flags, Func<string, int>? measure)
    {
        var menu = new OriginalMenu(0, 0, 1, 256, 256, 13) { MeasureText = measure, WidthPadding = 10 };
        (int first, _) = OriginalMainMenu.ContinueLevel(save.CompletionCounts);
        for (int index = 0; index < OriginalLevelCatalog.CampaignLevelCount; index++)
            if (flags.LevelSelect != 0 || save.CompletionCounts[index] != 0 || index == first)
                menu.Add(levels.Levels[index].DisplayName);
        menu.RepeatRate = 1;
        menu.VisibleLines = 9;
        menu.Wrap = false;
        menu.CentreX();
        menu.CentreY();
        return menu;
    }

    /// <summary>Shell_LevelSelect accept 0x4A0E33: first matching display name, cleared restart and carried resources.</summary>
    public static void ApplyLevelSelection(OriginalSaveGame save, OriginalLevelCatalog levels, string displayName)
    {
        save.LevelName = "";
        foreach (OriginalLevelDefinition level in levels.Levels)
            if (OriginalCheats.CompareStrings(displayName, level.DisplayName)) { save.LevelName = level.MissionName; break; }
        save.RestartPointName = "";
        save.ArmourAmount = 0;
        save.ArmourUnlocked = 0;
        save.Webbing = 0;
        save.Cartridges = 0;
    }

    /// <summary>Shell_AddGameSlots 0x49F220: save name, "read error" or "---- empty ----", the latter two in dim red.</summary>
    public static void AddGameSlots(OriginalMenu menu, OriginalSaveFile file, string readErrorText, string emptyText)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(file);
        if (menu.Entries.Count != 0) throw new InvalidOperationException("Tried to add slots to non-empty menu");
        for (int index = 0; index < OriginalSaveFile.SlotCount; index++)
        {
            OriginalSaveGame slot = file.Slot(index);
            OriginalSaveSlotState state = slot.SlotState;
            OriginalMenuEntry entry = menu.Add(state switch
            {
                OriginalSaveSlotState.Valid => slot.SaveName,
                OriginalSaveSlotState.ReadError => readErrorText,
                _ => emptyText
            });
            if (state == OriginalSaveSlotState.Valid) continue;
            entry.SelectedTop = entry.SelectedBottom = new(80, 16, 36);
            entry.NormalTop = entry.NormalBottom = new(40, 8, 18);
        }
    }

    /// <summary>Shell_LoadGame 0x49F2F0 accept: only a valid slot loads; records merge into the file copy which becomes global.</summary>
    public static bool LoadSlot(OriginalSaveFile file, int slot, OriginalSaveGame current, OriginalHighScores globalRecords,
        IReadOnlyList<OriginalChallenge> challenges, out OriginalSaveGame loaded)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(current);
        OriginalSaveGame candidate = file.Slot(slot);
        loaded = current;
        if (candidate.SlotState != OriginalSaveSlotState.Valid) return false;
        loaded = candidate;
        OriginalHighScores records = file.Records;
        OriginalHighScores.MergeInto(records, globalRecords, challenges);
        file.Records = records;
        return true;
    }

    /// <summary>Shell_SaveGame 0x49FAF0 name-accepted path: seal gSaveGame, copy into the slot, merge records.</summary>
    public static void SaveSlot(OriginalSaveFile file, int slot, OriginalSaveGame current, OriginalHighScores globalRecords,
        IReadOnlyList<OriginalChallenge> challenges)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(current);
        current.Seal();
        file.SetSlot(slot, current);
        OriginalHighScores records = file.Records;
        OriginalHighScores.MergeInto(records, globalRecords, challenges);
        file.Records = records;
    }

    /// <summary>PShell_MoveTowards 0x47A570.</summary>
    public static int MoveTowards(int value, int target)
    {
        int delta = target - value;
        if (delta == 0) return value;
        if ((delta & ~3) == 0) return value + 1;
        return value + (delta >> 2);
    }
}

/// <summary>Shell_InputName 0x48DC10 buffer: 8 dot-padded characters, A-Z/0-9/space, and the rude-word replacement.</summary>
public sealed class OriginalNameEntry
{
    private readonly char[] name = new char[8];
    private readonly string original;
    public int Length { get; private set; }
    public string Display => new(name);

    public OriginalNameEntry(string initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        original = initial;
        Array.Fill(name, '.');
        initial.AsSpan(0, Math.Min(initial.Length, 8)).CopyTo(name);
        Length = initial.Length < 8 ? 0 : 8;
    }

    /// <summary>DXINPUT key names of length one; returns the sound to play, or null.</summary>
    public int? Type(char key)
    {
        if (key == ' ')
        {
            if (Length >= 8) return null;
            name[Length++] = ' ';
            return 0x29;
        }
        if (key is not (>= 'A' and <= 'Z' or >= '0' and <= '9') || Length >= 8) return null;
        name[Length++] = key;
        return 0x1F;
    }

    public int? Backspace()
    {
        if (Length == 0) return null;
        name[--Length] = '.';
        return 0x29;
    }

    public string Cancel() => original;

    /// <summary>
    /// Accept: a rude word yields a replacement (shown after the CRudeWordHitterSpidey animation); a leading dot keeps editing;
    /// otherwise the name is cut at the first dot.
    /// </summary>
    public (bool Finished, string? Result, string? Replacement) Accept(IReadOnlyList<string> encodedBadWords, IReadOnlyList<string> goodWords,
        Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(encodedBadWords);
        ArgumentNullException.ThrowIfNull(goodWords);
        ArgumentNullException.ThrowIfNull(random);
        string text = Display;
        foreach (string encoded in encodedBadWords)
        {
            string word = new(encoded.Take(8).Select(character => (char)(character - 1)).ToArray());
            if (text.Length != 0 && ContainsSubString(text, word))
            {
                string good = goodWords[random(goodWords.Count)];
                good = good.Length > 8 ? good[..8] : good;
                if (good.Length != 0) return (false, null, good.PadRight(8, '.'));
                break;
            }
        }
        if (text[0] == '.') return (false, null, null);
        int dot = text.IndexOf('.');
        return (true, dot < 0 ? text : text[..dot], null);
    }

    public void Replace(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        if (replacement.Length != 8) throw new ArgumentException("A replacement fills all eight name characters.", nameof(replacement));
        replacement.CopyTo(0, name, 0, 8);
    }

    /// <summary>Shell_ContainsSubString: case-folded ASCII search; an empty needle matches any nonempty text.</summary>
    public static bool ContainsSubString(string text, string needle)
    {
        for (int start = 0; start < text.Length; start++)
        {
            int index = 0;
            for (; index < needle.Length; index++)
            {
                char left = start + index < text.Length ? Lower(text[start + index]) : '\0';
                if (left != Lower(needle[index])) break;
            }
            if (index == needle.Length) return true;
        }
        return false;
        static char Lower(char value) => value is >= 'A' and <= 'Z' ? (char)(value + 32) : value;
    }

    public static (IReadOnlyList<string> Bad, IReadOnlyList<string> Good) ReadWordLists(ReadOnlyMemory<byte> image)
    {
        var data = new OriginalExecutableData(image);
        string Word(uint slot) => Encoding.Latin1.GetString(data.ReadString(data.ReadPointer(slot), 64).Span);
        return (Enumerable.Range(0, 29).Select(index => Word(0x552744u + (uint)index * 4)).ToArray(),
            Enumerable.Range(0, 30).Select(index => Word(0x5527B8u + (uint)index * 4)).ToArray());
    }
}
