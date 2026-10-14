using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Venom;

/// <summary>CVenom::Walkabout (0x4EB530): substates 0/1 and level 0x501 chase-distance probe only.</summary>
public static class VenomWalkabout
{
    public static void Tick(VenomState state, IVenomHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        switch (state.Substate)
        {
            case 0:
                state.Field34D = 1;
                state.Field354 = 1;
                state.Substate = 1;
                break;
            case 1:
                if (host.LevelId == 0x501 && (state.Field218 & 0x400) == 0 && state.DifficultyLevel != 0)
                {
                    int threshold = state.DifficultyLevel == 2 ? 0x1D4C : 0x2328;
                    FixedVector3 player = host.PlayerPosition;
                    int dx = unchecked((host.Body.Position.X >> 12) - (player.X >> 12));
                    int dz = unchecked((host.Body.Position.Z >> 12) - (player.Z >> 12));
                    int distance = FixedAim.Distance(new(dx, 0, dz), default);
                    if (distance > threshold) state.Field354 = 0;
                }
                if (state.Field354 != 0)
                    throw new NotSupportedException("CVenom::SynthesizeAnalogueInput (0x4E9B00) is not ported yet.");
                state.AiState = 1;
                state.Substate = 0;
                break;
            default:
                throw new InvalidOperationException($"CVenom::Walkabout unknown substate {state.Substate}.");
        }
    }
}
