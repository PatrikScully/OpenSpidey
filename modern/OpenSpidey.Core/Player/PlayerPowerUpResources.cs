namespace OpenSpidey.Core.Player;

public interface IPlayerArmorResourceHost
{
    int Difficulty { get; }
    void ClearArmorHealthIcon();
    void LoadArmorHealthIcon();
    void ProcessArmorTextures(bool active);
}

public sealed class PlayerArmorTextureState
{
    public bool LowGraphics { get; set; }
    public bool Processing { get; set; } // Original shared6A9041, independent of armor-active6A9040.
    public int Suit { get; set; }
}

public interface IPlayerArmorTextureHost
{
    void SwapSuitTextures(int sourceSuit, int targetSuit);
}

/// <summary>Entire source IncHealth4BD750 and SetArmor4BAEC0, sharing the live health, pickup revision and armor fields.</summary>
public static class PlayerPowerUpResources
{
    /// <summary>The original low-graphics-only Spidey_DoArmorVRAMProcessing boundary in SetArmor.</summary>
    public static void ProcessArmorTextures(PlayerArmorTextureState state, bool active, IPlayerArmorTextureHost host)
    {
        if (!state.LowGraphics || active == state.Processing) return;
        if (active) host.SwapSuitTextures(state.Suit, 0);
        else host.SwapSuitTextures(0, state.Suit);
        state.Processing = !state.Processing;
    }

    public static bool IncreaseHealth(PlayerCombatState combat, PlayerWebInventory inventory,
        PlayerPowerUpCollectionState pickups, int amount, uint clock)
    {
        if (combat.Health >= combat.MaxHealth || combat.Health <= 0) return false;
        combat.Health = unchecked((short)(combat.Health + amount));
        if (combat.Health > combat.MaxHealth) combat.Health = unchecked((short)combat.MaxHealth);
        pickups.LastHealthPickupClock = clock;
        inventory.PickupRevision = unchecked(inventory.PickupRevision + 1);
        return true;
    }

    public static bool SetArmor(PlayerCombatState combat, bool active, IPlayerArmorResourceHost host)
    {
        combat.ArmorAnimationVariant = 0; host.ClearArmorHealthIcon();
        if (active)
        {
            host.LoadArmorHealthIcon(); combat.ArmorAnimationVariant = 1;
            combat.ArmorAmount = host.Difficulty switch
            {
                0 => 600, 1 => 200, 2 => 100, 3 => 80, _ => combat.ArmorAmount
            };
        }
        if (active == combat.ArmorTexturesActive) return true;
        host.ProcessArmorTextures(active);
        combat.ArmorActive = active; combat.ArmorTexturesActive = active;
        return true;
    }
}
