using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Jonah's actual DoMGSShadow footprint, using source part hooks3,6,10,15 at the signed ground register.</summary>
public static class JonahFootprintGeometry
{
    public static FixedVector3[] FromPose(FixedVector3 position, FixedVector3 transformTranslation,
        FixedJointTransform rotation, short groundY, ReadOnlySpan<FixedJointTransform> pose)
    {
        if (pose.Length != 16) throw new ArgumentException("Jonah footprint requires the genuine16-part Jameson/Joel pose.", nameof(pose));
        ReadOnlySpan<ushort> parts = [3, 6, 10, 15];
        Span<FixedVector3> hooks = stackalloc FixedVector3[4];
        for (int index = 0; index < parts.Length; index++)
            hooks[index] = ActorHookPosition.Evaluate(position, transformTranslation, rotation, pose, new ActorHook(0, 0, 0, parts[index]));
        return FromWorldHooks(position, rotation, groundY, hooks);
    }

    public static FixedVector3[] FromWorldHooks(FixedVector3 position, FixedJointTransform rotation,
        short groundY, ReadOnlySpan<FixedVector3> hooks) => CinematicActorPose.ShadowCorners(position, rotation, groundY, hooks);
}
