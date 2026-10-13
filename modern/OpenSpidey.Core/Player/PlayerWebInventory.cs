using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Original web supply and pickup revision, retaining Q12 fill and source cartridge accounting.</summary>
public sealed class PlayerWebInventory
{
    public int Webbing { get; set; }
    public int Cartridges { get; set; }
    public byte FireWebbing { get; set; }
    public int PickupRevision { get; set; }
    public uint LastCartridgeClock { get; set; }
    // Source byte +0xE10 allows an empty supply attempt without consuming or clearing fire webbing.
    public byte EmptyUseOverride { get; set; }
}

public interface IPlayerWebInventoryHost
{
    short Health { get; }
    int Suit { get; }
    int Difficulty { get; }
    uint Clock { get; }
    bool SynthesizedInputActive { get; }
    bool ConsumptionDisabled { get; }
    bool SpeechBusy { get; }
    FixedVector3 Position { get; }
    int Random(int range);
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitchOffset);
    void PlaySpeech(SpeechRequest request);
}

public readonly record struct PlayerStartingResources(short Health, int Webbing, int Cartridges);

/// <summary>CPlayer constructor resource selection, Increase/DecreaseWebbing and SetFireWebbing.</summary>
public static class PlayerWebInventoryRules
{
    public static PlayerStartingResources StartingResources(int difficulty, int suit, int levelId, int savedWebbing = 0, int savedCartridges = 0)
    {
        (int health, int cartridges) = difficulty switch
        {
            0 => (600, 9), 1 => (200, 9), 2 => (100, 7), 3 => (80, 2),
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty), "Original playable difficulties are zero through three.")
        };
        bool limitedSuit = suit is 6 or 9 or 10;
        if (limitedSuit) cartridges = 2;
        int webbing = 4096;
        if (savedWebbing != 0 || savedCartridges != 0)
        {
            webbing = savedWebbing;
            cartridges = limitedSuit && savedCartridges > 2 ? 2 : savedCartridges;
        }
        if ((unchecked((uint)levelId) & 0xFFFFFF00) > 0x800) cartridges = 2;
        return new((short)health, webbing, cartridges);
    }

    public static bool Increase(PlayerWebInventory state, int amount, IPlayerWebInventoryHost host)
    {
        if (host.Health <= 0) return false;
        int maximum = host.Suit is 6 or 9 or 10 ? 2 : 10;
        if ((state.Webbing >= 4096 || state.FireWebbing != 0) && state.Cartridges >= maximum) return false;
        state.Webbing = unchecked(state.Webbing + amount);
        if (state.Webbing > 4096)
        {
            if (state.Cartridges < maximum)
            {
                state.Cartridges = unchecked(state.Cartridges + 1);
                state.Webbing = unchecked(state.Webbing - 4096);
                state.LastCartridgeClock = host.Clock;
                state.PickupRevision = unchecked(state.PickupRevision + 1);
                return true;
            }
            state.Webbing = 4096;
        }
        state.PickupRevision = unchecked(state.PickupRevision + 1);
        return true;
    }

    public static bool Decrease(PlayerWebInventory state, int amount, IPlayerWebInventoryHost host)
    {
        if (host.SynthesizedInputActive || host.ConsumptionDisabled || host.Suit is 3 or 4) return true;
        int cost = host.Difficulty switch { 0 => unchecked(amount << 7) >> 12, 1 => unchecked(amount << 11) >> 12, _ => amount };
        if (state.Webbing > cost)
        {
            state.Webbing = unchecked(state.Webbing - cost);
            return true;
        }
        if (state.Cartridges != 0)
        {
            state.Webbing = unchecked(state.Webbing - cost + 4096);
            state.Cartridges = unchecked(state.Cartridges - 1);
            host.PlayPositionSound(0x1E, host.Position, 0);
            state.FireWebbing = 0;
            return true;
        }
        if (state.EmptyUseOverride != 0) return true;
        if (!host.SpeechBusy) host.PlaySpeech(new(33, host.Random(3) + 2, 0));
        state.FireWebbing = 0;
        return false;
    }

    public static bool SetFireWebbing(PlayerWebInventory state)
    {
        state.FireWebbing = 1;
        state.Webbing = 4096;
        state.PickupRevision = unchecked(state.PickupRevision + 1);
        return true;
    }
}
