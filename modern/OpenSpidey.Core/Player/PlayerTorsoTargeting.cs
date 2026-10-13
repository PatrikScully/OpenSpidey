using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Source torso target requests update the shared heading turn; they do not rotate a model joint directly.</summary>
public static class PlayerTorsoTargeting
{
    public static PlayerHeadingTurn Lock(PlayerHeadingTurn turn) => turn with { RemainingTicks = 0 };

    /// <param name="turn">Heading must contain current PlayerOrientation.GetEffectiveHeading, including the wall case.</param>
    public static PlayerHeadingTurn TargetPoint(PlayerHeadingTurn turn, FixedVector3 position, FixedVector3 target,
        FixedJointTransform sourceCameraRotation, bool onCeiling, uint action, int airborneTurnFactor, bool crawling)
    {
        FixedVector3 delta = new(unchecked(target.X - position.X) >> 12,
            unchecked(target.Y - position.Y) >> 12, unchecked(target.Z - position.Z) >> 12);
        FixedVector3 local = FixedRotation.Transform(sourceCameraRotation, delta);
        short localHeading = (short)((1024 - GameAngle.Atan2(unchecked(-local.Z), unchecked(-local.X))) & 4095);
        short wanted = unchecked((short)((onCeiling ? turn.Heading - localHeading : turn.Heading + localHeading) & 4095));
        return PlayerHeadingTurning.Target(turn, wanted, action, airborneTurnFactor, crawling, false);
    }
}
