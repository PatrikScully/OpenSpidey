using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

/// <summary>Native owners for the PC suit-5 switch: keyboard new presses per source poll, pad LeftTwo, player brightness and CItem flags.</summary>
public sealed class GodotPlayerCostumeVisibility(GodotPlayer player) : IPlayerCostumeVisibilityHost
{
    private static readonly ushort[] actions = [0x40, 0x100];
    private readonly bool[] held = new bool[actions.Length];
    private readonly bool[] triggered = new bool[actions.Length];
    public Func<ushort, bool> SampleKeyboardAction { get; set; } = PlayerControls.KeyboardActionHeld;
    public int CurrentSuit => player.Combat.CurrentSuit;

    public ushort ItemFlags
    {
        get => player.ActorState.ItemFlags;
        set => player.ActorState.ItemFlags = value;
    }

    internal void Sample()
    {
        for (int index = 0; index < actions.Length; index++)
        {
            bool now = SampleKeyboardAction(actions[index]);
            triggered[index] |= now && !held[index];
            held[index] = now;
        }
    }

    internal void ConsumeTriggers() => Array.Clear(triggered);

    public bool KeyboardActionTriggered(uint action)
    {
        int index = Array.IndexOf(actions, checked((ushort)action));
        if (index < 0) throw new NotSupportedException($"The PC costume switch reads only actions0x40 and0x100, not 0x{action:X}.");
        return triggered[index];
    }

    public void ClearLeftTwoTrigger() => player.ClearPhysicalTrigger(5);

    public void ResetBrightness() =>
        (player.Controller.SurfaceBrightness as GodotPlayerBrightness ??
            throw new InvalidOperationException("The PC costume switch resets player570, which requires the native player brightness owner.")).State.Current = 0;

    public void PlayPositionSound(ushort sound, FixedVector3 position, int pitchOffset) => player.Combat.PlayPositionSound(sound, position, pitchOffset);
}
