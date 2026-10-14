using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

public partial class GodotPlayer
{
    /// <summary>These arrays and accumulated edges alias the source's twenty1C0 button records.</summary>
    internal void ClearSourceScriptInput()
    {
        Array.Clear(PhysicalPad);
        Array.Clear(pendingPhysicalTriggers);
        pendingButtons = PlayerButton.None;
        if (Melee is not { } melee) return;
        Array.Clear(melee.State.Input.Pressed);
        Array.Clear(melee.State.Input.Triggered);
        Array.Clear(melee.State.Input.Mirrors);
    }
}
