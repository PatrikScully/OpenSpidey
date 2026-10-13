using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

/// <summary>Retained original process globals548858/54885C; constructing a camera never resets them.</summary>
public sealed class CameraNormalProjectionCache
{
    public int LookAngle { get; set; } = 300;
    public int Distance { get; set; } = 200;
    public void UpdateFromDistances(int horizontal, int vertical)
    {
        int y = unchecked(-vertical);
        Distance = FixedVectorMath.OriginalSquareRoot(unchecked(horizontal * horizontal + y * y));
        LookAngle = GameAngle.Atan2(y, horizontal);
    }
}

/// <summary>CCamera source Euler234/236/238 and normal-mode desired/current position and quaternion.</summary>
public sealed class CameraNormalPlacementState
{
    public short Pitch { get; set; }
    public short Heading { get; set; }
    public short Roll { get; set; }
    public FixedVector3 Focus { get; set; }
    public FixedVector3 DesiredPosition { get; set; }
    public FixedQuaternion DesiredRotation { get; set; } = FixedQuaternion.Identity;
    public FixedVector3 Position { get; set; }
    public FixedQuaternion Rotation { get; set; } = FixedQuaternion.Identity;

    public void CalculateNormal(CameraNormalProjectionCache cache)
    {
        (short sine, short cosine) = GameAngle.Heading(cache.LookAngle);
        FixedJointTransform rotation = FixedJointTransform.FromJoint(new(Pitch, Heading, Roll, 0, 0, 0));
        FixedVector3 forward = FixedRotation.Transform(rotation, new(0, -sine, cosine));
        FixedVector3 up = FixedRotation.Transform(rotation, new(0, cosine, sine));
        FixedVector3 back = forward * -1;
        FixedVector3 right = new(unchecked(up.Y * back.Z - up.Z * back.Y) >> 12,
            unchecked(up.Z * back.X - up.X * back.Z) >> 12,
            unchecked(up.X * back.Y - up.Y * back.X) >> 12);
        var matrix = new FixedJointTransform(unchecked((short)right.X), unchecked((short)up.X), unchecked((short)back.X),
            unchecked((short)right.Y), unchecked((short)up.Y), unchecked((short)back.Y),
            unchecked((short)right.Z), unchecked((short)up.Z), unchecked((short)back.Z), 0, 0, 0);
        DesiredPosition = Focus + forward * cache.Distance;
        DesiredRotation = FixedQuaternion.FromMatrix(matrix);
        if (Pitch != 0 || Roll != 0)
            Heading = unchecked((short)((-1024 - GameAngle.Atan2(matrix.M22, matrix.M02)) & 4095));
    }

    public void SetStartPosition(int mode, FixedVector3 tripod, CameraNormalProjectionCache cache)
    {
        if (mode != 3) return;
        Focus = tripod;
        CalculateNormal(cache);
        Position = DesiredPosition;
        Rotation = DesiredRotation;
    }
}
