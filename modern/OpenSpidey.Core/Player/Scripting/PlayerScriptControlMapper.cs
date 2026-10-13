namespace OpenSpidey.Core.Player.Scripting;

/// <summary>Recovered pad slots used by the modern locomotion/action controllers.</summary>
public static class PlayerScriptControlMapper
{
    private static readonly (int Slot, PlayerButton Button)[] bindings =
        [(4, PlayerButton.Target), (6, PlayerButton.Zip), (7, PlayerButton.Swing), (16, PlayerButton.Jump),
         (17, PlayerButton.Web), (18, PlayerButton.Kick), (19, PlayerButton.Punch)];

    public static PlayerControlFrame Map(PlayerScriptInput input)
    {
        if (input.Buttons.Count != 20) throw new InvalidDataException("Script input needs twenty pad slots.");
        PlayerButton held = PlayerButton.None, triggered = PlayerButton.None;
        foreach ((int slot, PlayerButton button) in bindings)
        {
            if (input.Buttons[slot].Pressed != 0) held |= button;
            if (input.Buttons[slot].Triggered != 0) triggered |= button;
        }
        return new(new(false, default, false, false, false, false, input.Buttons[4].Pressed != 0,
            input.Buttons[5].Triggered != 0, input.Axes), new(held, triggered));
    }
}
