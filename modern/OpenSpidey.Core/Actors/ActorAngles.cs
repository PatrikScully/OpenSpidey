namespace OpenSpidey.Core.Actors;

public readonly record struct ActorAngles(short X, short Y, short Z)
{
    public static ActorAngles operator +(ActorAngles left, ActorAngles right) =>
        new(unchecked((short)(left.X + right.X)), unchecked((short)(left.Y + right.Y)), unchecked((short)(left.Z + right.Z)));
    public ActorAngles Mask() => new((short)(X & 4095), (short)(Y & 4095), (short)(Z & 4095));
    public ActorAngles KillSmall() => new(Small(X), Small(Y), Small(Z));
    private static short Small(short value) => value is >= -1 and <= 1 ? (short)0 : value;
}
