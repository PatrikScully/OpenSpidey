using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Recovered attachment-point transform and positional smoothing used by player body probes.</summary>
public static class PlayerBodyHook
{
    /// <param name="previousPosition">Root position at the last pose build, whose whole-unit translation is cached.</param>
    /// <param name="partPose">Cached part pose from the last pose build, before the current animation clock update.</param>
    /// <remarks>The crawl body hook uses part zero and a zero offset; local coordinates are signed 16-bit values.</remarks>
    public static FixedVector3 Evaluate(FixedVector3 position, FixedVector3 previousPosition,
        FixedJointTransform bodyBasis, FixedJointTransform partPose,
        short offsetX = 0, short offsetY = 0, short offsetZ = 0, bool mirrored = false)
    {
        FixedVector3 rotated = FixedRotation.Transform(partPose, new(offsetX, offsetY, offsetZ));
        var local = new FixedVector3(unchecked((short)(rotated.X + partPose.X)),
            unchecked((short)(rotated.Y + partPose.Y)), unchecked((short)(rotated.Z + partPose.Z)));
        if (mirrored)
            bodyBasis = bodyBasis with { M00 = unchecked((short)-bodyBasis.M00),
                M10 = unchecked((short)-bodyBasis.M10), M20 = unchecked((short)-bodyBasis.M20) };
        FixedVector3 world = FixedRotation.Transform(bodyBasis, local) + (previousPosition >> 12);
        return position + (((world << 12) - position) >> 4);
    }
}
