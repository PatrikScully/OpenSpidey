using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors;

/// <summary>Recovered gsub_46F820 and M3dUtils_GetDynamicHookPosition affine hook math.</summary>
public static class ActorHookPosition
{
    /// <param name="position">Current actor position in original Q12 world units.</param>
    /// <param name="transformTranslation">Cached MATRIX.t translation in whole world units, before character scaling.</param>
    /// <param name="actorRotation">Original actor Q12 rotation; its short translation fields are ignored.</param>
    /// <param name="pose">Current original model-space part matrices, without the player's render-only part swaps.</param>
    public static FixedVector3 Evaluate(FixedVector3 position, FixedVector3 transformTranslation,
        FixedJointTransform actorRotation, ReadOnlySpan<FixedJointTransform> pose, ActorHook hook)
    {
        if (hook.PartIndex >= pose.Length) throw new ArgumentOutOfRangeException(nameof(hook), "Hook references a missing pose part.");
        FixedJointTransform part = pose[hook.PartIndex];
        FixedVector3 rotated = Rotate(part, new(hook.X, hook.Y, hook.Z));
        var local = new FixedVector3(unchecked((short)(rotated.X + part.X)),
            unchecked((short)(rotated.Y + part.Y)), unchecked((short)(rotated.Z + part.Z)));
        FixedVector3 world = Rotate(actorRotation, local) + transformTranslation;
        // Characters use sixteen model units per world unit. Preserve the source subtraction and
        // arithmetic shift, including wrapping Q12 conversion and fractional actor-position rounding.
        return position + (((world << 12) - position) >> 4);
    }

    // The PC routine sign-extends each operand, calls __allmul and __allshr, then narrows the result.
    // Keep this separate from FixedRotation's general GTE-style 32-bit accumulation.
    private static FixedVector3 Rotate(FixedJointTransform basis, FixedVector3 vector) => new(
        Dot(basis.M00, basis.M01, basis.M02, vector),
        Dot(basis.M10, basis.M11, basis.M12, vector),
        Dot(basis.M20, basis.M21, basis.M22, vector));

    private static int Dot(short x, short y, short z, FixedVector3 vector) =>
        unchecked((int)(((long)x * vector.X + (long)y * vector.Y + (long)z * vector.Z) >> 12));
}
