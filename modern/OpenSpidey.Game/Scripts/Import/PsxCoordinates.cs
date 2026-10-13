using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Game.Import;

internal static class PsxCoordinates
{
    private const float WorldUnitsPerMeter = 100;
    private const float CharacterUnitsPerMeter = WorldUnitsPerMeter * 16;

    public static Vector3 FixedWorldPosition(FixedVector3 position) =>
        new Vector3(position.X, -(float)position.Y, -(float)position.Z) / (4096 * WorldUnitsPerMeter);

    public static FixedVector3 OriginalWorldPosition(Vector3 position) =>
        new(GameWord.TruncateToInt32(position.X * (4096 * WorldUnitsPerMeter)),
            GameWord.TruncateToInt32(-position.Y * (4096 * WorldUnitsPerMeter)),
            GameWord.TruncateToInt32(-position.Z * (4096 * WorldUnitsPerMeter)));

    public static FixedJointTransform OriginalRotation(Basis basis) => new(
        (short)(basis.X.X * 4096), (short)(-basis.Y.X * 4096), (short)(-basis.Z.X * 4096),
        (short)(-basis.X.Y * 4096), (short)(basis.Y.Y * 4096), (short)(basis.Z.Y * 4096),
        (short)(-basis.X.Z * 4096), (short)(basis.Y.Z * 4096), (short)(basis.Z.Z * 4096), 0, 0, 0);

    public static FixedVector3 RotateOriginalNormal(FixedVector3 normal, FixedJointTransform rotation) =>
        new(unchecked((short)((rotation.M00 * normal.X + rotation.M01 * normal.Y + rotation.M02 * normal.Z) >> 12)),
            unchecked((short)((rotation.M10 * normal.X + rotation.M11 * normal.Y + rotation.M12 * normal.Z) >> 12)),
            unchecked((short)((rotation.M20 * normal.X + rotation.M21 * normal.Y + rotation.M22 * normal.Z) >> 12)));

    public static Vector3 WorldPosition(System.Numerics.Vector3 position) =>
        new Vector3(position.X, -position.Y, -position.Z) / WorldUnitsPerMeter;

    public static Vector3 CharacterPosition(System.Numerics.Vector3 position) =>
        new Vector3(position.X, -position.Y, -position.Z) / CharacterUnitsPerMeter;

    public static Transform3D InstanceTransform(PsxInstance instance)
    {
        Transform3D transform = CharacterTransform(InstanceRotation(instance));
        transform.Origin = WorldPosition(instance.Position / 4096);
        return transform;
    }

    public static FixedJointTransform InstanceRotation(PsxInstance instance) =>
        FixedJointTransform.FromJoint(new((short)instance.Angles.X,
            (short)instance.Angles.Y, (short)instance.Angles.Z, 0, 0, 0));

    public static Transform3D CharacterTransform(FixedJointTransform pose) => new(
        new Basis(new Vector3(pose.M00, -pose.M10, -pose.M20) / 4096,
            new Vector3(-pose.M01, pose.M11, pose.M21) / 4096,
            new Vector3(-pose.M02, pose.M12, pose.M22) / 4096),
        new Vector3(pose.X, -pose.Y, -pose.Z) / CharacterUnitsPerMeter);
}
