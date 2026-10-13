using OpenSpidey.Core.Missions;

namespace OpenSpidey.Core.Player;

/// <summary>The five original carried save fields; saved flag bytes retain their original values.</summary>
public readonly record struct PlayerCarriedSaveState(
    int Webbing, int Cartridges, int ArmourAmount, byte ArmourUnlocked, byte ArmourOn);

/// <summary>The current save and the previous save backed up before a live player overwrites it.</summary>
public readonly record struct PlayerCarriedSaveHistory(PlayerCarriedSaveState Saved, PlayerCarriedSaveState Previous);

/// <summary>Constructor state and the real asset operations required by the original armour restore.</summary>
public readonly record struct PlayerCarriedConstructorState(
    PlayerStartingResources Resources,
    int ArmourAmount,
    byte ArmourOn,
    byte ArmourAnimationVariant,
    byte ArmourTexturesActive,
    byte ArmourTextureProcessing,
    bool LoadArmourAnimation,
    bool SwapArmourTextures,
    int ArmourAmountBeforeSavedRestore);

/// <summary>CPlayer's carried-save writes, no-player retry restoration and ordered constructor resource policy.</summary>
public static class PlayerCarriedSavePolicy
{
    public static PlayerCarriedSaveHistory OnPlayerDestroyed(PlayerCarriedSaveHistory history,
        LevelExitReason exitReason, PlayerCarriedSaveState live)
    {
        if (exitReason is LevelExitReason.Running or LevelExitReason.LevelCompleted)
        {
            PlayerCarriedSaveState saved = live with
            {
                ArmourUnlocked = Normalize(live.ArmourUnlocked),
                ArmourOn = Normalize(live.ArmourOn)
            };
            return new(saved, history.Saved);
        }
        return exitReason == LevelExitReason.PlayerDied
            ? history with { Saved = history.Saved with { Webbing = 0, Cartridges = 0 } }
            : history;
    }

    /// <summary>LoadNewTrg restores the backup only for a running same-mission transition without a player.</summary>
    public static PlayerCarriedSaveHistory OnMissionLoad(PlayerCarriedSaveHistory history,
        bool sameMission, LevelExitReason exitReason, bool hasPlayer) =>
        sameMission && exitReason == LevelExitReason.Running && !hasPlayer ? RestorePrevious(history) : history;

    public static PlayerCarriedSaveHistory RestorePrevious(PlayerCarriedSaveHistory history) =>
        history with { Saved = history.Previous };

    /// <summary>
    /// Inventory restore and suit/chapter clamps precede armour setup. A native host must load costarm first,
    /// perform the requested texture swap with the difficulty armour amount, then install saved armour.
    /// Saved armour-on is restored independently for suit 5, including when armour loading is suppressed.
    /// </summary>
    public static PlayerCarriedConstructorState CreatePlayer(PlayerCarriedSaveState saved,
        int difficulty, int suit, int levelId, bool armourSuppressed,
        byte armourTexturesActive = 0, byte armourTextureProcessing = 0, bool lowGraphics = false)
    {
        PlayerStartingResources resources = PlayerWebInventoryRules.StartingResources(
            difficulty, suit, levelId, saved.Webbing, saved.Cartridges);
        bool load = saved.ArmourUnlocked != 0 && !armourSuppressed;
        bool initializeTextures = load && armourTexturesActive == 0;
        bool swap = initializeTextures && lowGraphics && armourTextureProcessing == 0;
        return new(resources,
            load ? saved.ArmourAmount : 0,
            saved.ArmourOn != 0 && suit == 5 ? (byte)1 : (byte)0,
            initializeTextures ? (byte)1 : (byte)0,
            initializeTextures ? (byte)1 : armourTexturesActive,
            swap ? (byte)1 : armourTextureProcessing,
            load, swap, load ? resources.Health : 0);
    }

    private static byte Normalize(byte value) => value == 0 ? (byte)0 : (byte)1;
}
