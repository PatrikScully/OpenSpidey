using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Player owners touched by the PC-only suit-5 switch at the top of SpideyAI0.</summary>
public interface IPlayerCostumeVisibilityHost
{
    int CurrentSuit { get; } // Global5559DC.
    /// <summary>PCINPUT_GetKeyboardMappingForAction(action), then PCINPUT_IsKeyPressed(key, 1): a keyboard-only new press.</summary>
    bool KeyboardActionTriggered(uint action);
    ushort ItemFlags { get; set; }
    void ClearLeftTwoTrigger(); // SControl+0x51, the LeftTwo record's triggered byte.
    void ResetBrightness(); // Player+0x570.
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitchOffset);
}

/// <summary>Original4B141E: actions0x40 and0x100 toggle player57C with suit5; 57C then drives CItem flag800 every frame.</summary>
public static class PlayerCostumeVisibility
{
    public static void Update(PlayerCombatState state, IPlayerCostumeVisibilityHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (host.CurrentSuit == 5 && host.KeyboardActionTriggered(0x40) && host.KeyboardActionTriggered(0x100) &&
            !state.SynthesizedInputActive && state.FieldE18 == 0)
        {
            host.ClearLeftTwoTrigger();
            state.SuitArmorEnabled = state.SuitArmorEnabled == 0 ? (byte)1 : (byte)0;
            host.ResetBrightness();
            host.PlayPositionSound(0x1F, state.Position, 0);
        }
        host.ItemFlags = state.SuitArmorEnabled != 0 ? unchecked((ushort)(host.ItemFlags | 0x800)) : unchecked((ushort)(host.ItemFlags & 0xF7FF));
    }
}
