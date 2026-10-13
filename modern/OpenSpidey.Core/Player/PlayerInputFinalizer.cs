namespace OpenSpidey.Core.Player;

/// <summary>Actual SpideyAI0 final pad/mirror edge reset after lookaround camera and offscreen indicators (PC4B867F..4B86AF).</summary>
public static class PlayerInputFinalizer
{
    public static void ClearEdges(PlayerMeleeInput input)
    {
        for (int slot = 0; slot < 20; slot++)
            if (slot is not (14 or 15)) input.Triggered[slot] = input.Mirrors[slot] = 0;
    }
}
