namespace OpenSpidey.Core.Actors.Simby;

/// <summary>CSimbyBase::AI (0x4ACF00), excluding the trailing CItem transform rebuild (0x475F50) the host performs.</summary>
public static class SimbyBaseBrain
{
    /// <param name="ownerRecovered">Mem_RecoverPointer(field_32C) (0x4583F0); only queried while following.</param>
    public static void Advance(SimbyBaseState state, Func<bool> ownerRecovered)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(ownerRecovered);
        state.ItemFlags |= 0x200;
        if (state.Following == 0)
        {
            short shrunk = unchecked((short)(state.ScaleZ - 100));
            state.ScaleZ = shrunk;
            state.ScaleY = shrunk;
            state.ScaleX = shrunk;
            if (shrunk < 100) state.BodyFlags |= 0x40;
            return;
        }
        if (!ownerRecovered())
        {
            state.Following = 0;
            return;
        }
        int target = state.TargetScale;
        if (state.ScaleX == target) return;
        // The step is added in 16 bits and the snap test is one-sided, so a shrinking base reaches its target at once.
        short eased = unchecked((short)(state.ScaleX + ((target - state.ScaleX) >> 2)));
        state.ScaleX = eased;
        if (target - eased < 40) state.ScaleX = unchecked((short)target);
        state.ScaleZ = state.ScaleX;
        state.ScaleY = state.ScaleX;
        if (state.ScaleX == 0) state.BodyFlags |= 0x40;
    }
}
