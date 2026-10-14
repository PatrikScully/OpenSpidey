using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Jonah's desired/smoothed contact coordinates; displacement remains owned by his AI state.</summary>
public sealed class JonahScorpionContactState(JonahBrainState brain)
{
    public JonahBrainState Brain { get; } = brain ?? throw new ArgumentNullException(nameof(brain));
    public int DesiredX { get; set; } // Source +0x33C.
    public int SmoothedX { get; set; } // Source +0x340.
    public int DesiredZ { get; set; } // Source +0x344.
    public int SmoothedZ { get; set; } // Source +0x348.
}

/// <summary>The complete PC GoAroundScorp contact easing, with its source rounding and deadzone.</summary>
public static class JonahScorpionContact
{
    public static void Resolve(JonahScorpionContactState state, FixedVector3 scorpionPosition)
    {
        JonahBrainState brain = state.Brain;
        FixedVector3 position = brain.Position;
        int deltaX = unchecked(position.X - scorpionPosition.X), deltaZ = unchecked(position.Z - scorpionPosition.Z);
        if (GameWord.Absolute(deltaX) < 409600 && GameWord.Absolute(deltaZ) < 409600)
        {
            FixedVector3 offset = FixedVectorMath.Normalize(new(deltaX >> 12, 0, deltaZ >> 12)) * 100;
            int previousX = unchecked(position.X + brain.PreviousDisplacementX);
            state.DesiredX = unchecked(position.X + offset.X);
            state.SmoothedX = unchecked(previousX + Adjustment(unchecked(state.DesiredX - previousX)));
            brain.PreviousDisplacementX = unchecked(state.SmoothedX - position.X);
            int previousZ = unchecked(position.Z + brain.PreviousDisplacementZ);
            state.DesiredZ = unchecked(position.Z + offset.Z);
            state.SmoothedZ = unchecked(previousZ + Adjustment(unchecked(state.DesiredZ - previousZ)));
            brain.PreviousDisplacementZ = unchecked(state.SmoothedZ - position.Z);
            brain.Position = position with { X = state.SmoothedX, Z = state.SmoothedZ };
            return;
        }
        if (brain.PreviousDisplacementX != 0)
        {
            state.DesiredX = position.X;
            state.SmoothedX = unchecked(position.X + brain.PreviousDisplacementX + Adjustment(unchecked(-brain.PreviousDisplacementX)));
            brain.PreviousDisplacementX = unchecked(state.SmoothedX - position.X);
            if (GameWord.Absolute(brain.PreviousDisplacementX) >= 8192) position = position with { X = state.SmoothedX };
            else brain.PreviousDisplacementX = 0;
        }
        if (brain.PreviousDisplacementZ != 0)
        {
            state.DesiredZ = position.Z;
            state.SmoothedZ = unchecked(position.Z + brain.PreviousDisplacementZ + Adjustment(unchecked(-brain.PreviousDisplacementZ)));
            brain.PreviousDisplacementZ = unchecked(state.SmoothedZ - position.Z);
            if (GameWord.Absolute(brain.PreviousDisplacementZ) >= 8192) position = position with { Z = state.SmoothedZ };
            else brain.PreviousDisplacementZ = 0;
        }
        brain.Position = position;
    }

    private static int Adjustment(int difference) => unchecked((800 * (difference >> 6)) >> 6) & ~63;
}
