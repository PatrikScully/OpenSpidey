using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Venom;

/// <summary>Venom_DisplayProgressBar (0x4E7E10): distance clamp and field_32C accumulator; HUD draw stays unported.</summary>
public static class VenomChaseBar
{
    public static void Update(VenomState venom, FixedVector3 venomPosition, FixedVector3 playerPosition, int difficultyLevel,
        bool postWaterEffect, int elapsedTicks)
    {
        int maxDistance = difficultyLevel switch { 2 => 7500, 0 or 1 => 9000, _ => 6000 };
        int dx = unchecked((playerPosition.X >> 12) - (venomPosition.X >> 12));
        int dz = unchecked((playerPosition.Z >> 12) - (venomPosition.Z >> 12));
        int distance = FixedAim.Distance(new(dx, 0, dz), default);
        if (distance > maxDistance) distance = maxDistance;
        _ = 307 * distance / maxDistance;
        if (!postWaterEffect) venom.ChaseBarAccumulator = unchecked(venom.ChaseBarAccumulator + elapsedTicks);
    }
}
