using OpenSpidey.Core.Saves;

namespace OpenSpidey.Core.Frontend;

/// <summary>One gCheats 0x5513E0 entry (code, description), stride 8.</summary>
public sealed record OriginalCheat(int Index, string Code, string Description);

/// <summary>Process-global cheat flags written by ActivateCheat outside the save record.</summary>
public sealed class OriginalCheatFlags
{
    /// <summary>0x60CFD8; enables Special "level select" (Shell_DoShell case 25).</summary>
    public int LevelSelect { get; set; }
    /// <summary>0x60CFE8.</summary>
    public int UnlimitedWebbing { get; set; }
    /// <summary>0x60CFF0.</summary>
    public int PulsatingHead { get; set; }
    /// <summary>0x60CFC8.</summary>
    public int Invulnerable { get; set; }
    /// <summary>0x2E09BF4.</summary>
    public int StickSpidey { get; set; }
    /// <summary>0x2E09BF0.</summary>
    public int ToonSpidey { get; set; }
    /// <summary>gWhatIf 0x60CFC5.</summary>
    public byte WhatIf { get; set; }
    /// <summary>JoelJewtCheatCode 0x60D004; unreachable from PShell_ActivateCheat, which rejects RULUR first.</summary>
    public int JoelJewett { get; set; }
}

public static class OriginalCheats
{
    public const int Count = 24;
    public const int Rulur = 4;

    /// <summary>PShell_ActivateCheat 0x47C440: Utils_CompareStrings 0x4E6560 lookup, RULUR rejected, then ActivateCheat 0x47C240.</summary>
    public static int Activate(string code, IReadOnlyList<OriginalCheat> cheats, OriginalSaveGame save, OriginalCheatFlags flags,
        Action? restorePlayerHealth)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(cheats);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(flags);
        if (cheats.Count != Count) throw new ArgumentException("The original cheat table has 24 entries.", nameof(cheats));
        int found = -1;
        for (int index = 0; index < Count; index++)
            if (CompareStrings(code, cheats[index].Code)) { found = index; break; }
        if (found == Rulur) return -1;
        return Apply(found, save, flags, restorePlayerHealth) ? found : -1;
    }

    /// <summary>ActivateCheat 0x47C240 switch; WEAKNESS only touches a live MechList player.</summary>
    public static bool Apply(int cheat, OriginalSaveGame save, OriginalCheatFlags flags, Action? restorePlayerHealth)
    {
        switch (cheat)
        {
            case 0: restorePlayerHealth?.Invoke(); break;
            case 1:
                save.HasGame = 1;
                save.CostumeUnlockFlags = save.CharacterViewerUnlockFlags = save.MovieViewerUnlockFlags =
                    save.CollectedComicMask = save.ComicCoverUnlockFlags = uint.MaxValue;
                save.StoryboardUnlocked = 1;
                flags.LevelSelect = 1;
                break;
            case 2: flags.UnlimitedWebbing ^= 1; break;
            case 3: flags.PulsatingHead ^= 1; break;
            case 4: flags.JoelJewett = flags.JoelJewett == 0 ? 1 : 0; break;
            case 5: save.CostumeUnlockFlags |= 4; break;
            case 6: save.CostumeUnlockFlags |= 2; break;
            case 7: save.CostumeUnlockFlags |= 8; break;
            case 8: save.CostumeUnlockFlags |= 0x10; break;
            case 9: save.CostumeUnlockFlags |= 0x40; break;
            case 10: save.CostumeUnlockFlags |= 0x20; break;
            case 11: save.CostumeUnlockFlags |= 0x200; break;
            case 12: save.CostumeUnlockFlags |= 0x100; break;
            case 13: save.StoryboardUnlocked = save.StoryboardUnlocked == 0 ? (byte)1 : (byte)0; break;
            case 14: save.ComicCoverUnlockFlags = 0x3F; break;
            case 15: flags.LevelSelect = 1; break;
            case 16: save.CollectedComicMask = uint.MaxValue; break;
            case 17: save.MovieViewerUnlockFlags = uint.MaxValue; break;
            case 18: save.CharacterViewerUnlockFlags = uint.MaxValue; break;
            case 19: flags.WhatIf = flags.WhatIf == 0 ? (byte)1 : (byte)0; break;
            case 20: flags.Invulnerable ^= 1; break;
            case 21: save.CostumeUnlockFlags |= 0x80; break;
            case 22:
                flags.StickSpidey = flags.StickSpidey == 0 ? 1 : 0;
                if (flags.StickSpidey != 0) flags.ToonSpidey = 0;
                break;
            case 23:
                flags.ToonSpidey = flags.ToonSpidey == 0 ? 1 : 0;
                if (flags.ToonSpidey != 0) flags.StickSpidey = 0;
                break;
            default: return false;
        }
        return true;
    }

    /// <summary>Utils_CompareStrings 0x4E6560: ASCII A-Z folded to lower case, exact length.</summary>
    public static bool CompareStrings(string left, string right)
    {
        if (left.Length != right.Length) return false;
        for (int index = 0; index < left.Length; index++)
            if (Lower(left[index]) != Lower(right[index])) return false;
        return true;
        static char Lower(char value) => value is >= 'A' and <= 'Z' ? (char)(value + 32) : value;
    }
}
