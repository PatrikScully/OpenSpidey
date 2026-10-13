namespace OpenSpidey.Core.Player;

[Flags]
public enum PlayerButton : ushort
{
    None = 0, Jump = 1, Kick = 2, Punch = 4, Web = 8, Target = 16, Zip = 32, Swing = 64
}

public readonly record struct PlayerButtonInput(PlayerButton Held, PlayerButton Triggered)
{
    public bool IsHeld(PlayerButton button) => (Held & button) != 0;
    public bool WasTriggered(PlayerButton button) => (Triggered & button) != 0;
}
