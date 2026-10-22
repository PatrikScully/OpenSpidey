using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Venom;

/// <summary>CVenom scripted walkabout (0x4EB530, AI state 0x4000): synthesized-input script, l5a1 chase escape and exit hooks.</summary>
public static class VenomWalkabout
{
    public static void Tick(VenomState state, IVenomHost host, Action synthesizeAnalogueInput)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(synthesizeAnalogueInput);
        switch (state.Substate)
        {
            case 0:
                state.Field34D = 1;
                state.Field354 = 1;
                state.Substate = 1;
                break;
            case 1:
                break;
            default:
                // Original asserts "Unknown substate" and returns.
                return;
        }

        int level = host.LevelId;
        if (level == 0x501 && (state.Field218 & 0x400) == 0 && host.DifficultyLevel != 0)
        {
            int threshold = host.DifficultyLevel == 2 ? 7500 : host.DifficultyLevel == 1 ? 9000 : 6000;
            if (XZDistance(host.Body.Position, host.PlayerPosition) > threshold)
            {
                state.Field218 |= 0x400;
                host.PulseL6A4Node(true);
            }
        }

        if (state.Field34D != 0)
        {
            synthesizeAnalogueInput();
            return;
        }

        state.AiState = 1;
        state.Substate = 0;
        if (level == 0x502)
            host.SetPlayerFocusLockTarget();
        else if (level == 0x604)
        {
            if (state.Field34C == 1 && host.SwitchIsOn(state.SwitchTargets[2])) host.FlickSwitch(state.SwitchTargets[2]);
            state.Field338 = 0xE000;
            host.SetPlayerFocusLockTarget();
        }
        else
            host.Die();
    }

    /// <summary>Utils_XZDist (0x4E61A0): shifts the difference before squaring.</summary>
    public static int XZDistance(FixedVector3 first, FixedVector3 second)
    {
        int x = unchecked(first.X - second.X) >> 12;
        int z = unchecked(first.Z - second.Z) >> 12;
        return FixedVectorMath.OriginalSquareRoot(unchecked(x * x + z * z));
    }
}
