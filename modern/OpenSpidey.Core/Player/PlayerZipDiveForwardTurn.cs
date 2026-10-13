namespace OpenSpidey.Core.Player;

public readonly record struct PlayerZipDiveForwardInput(byte SurfaceTransition, sbyte Forward, sbyte Sideways,
    ushort Animation, ushort EffectiveHeading, short InputHeading, short CameraHeading, bool OnSurface,
    ushort TurnTarget, int TurnRemaining, byte RetargetFlag);
public readonly record struct PlayerZipDiveForwardResult(bool Retarget, short Wanted, byte RetargetFlag);

/// <summary>The CheckForwards(false) branch reached exclusively by ZipDive's holding clip123.</summary>
public static class PlayerZipDiveForwardTurn
{
    public static PlayerZipDiveForwardResult Evaluate(PlayerZipDiveForwardInput input)
    {
        if (input.Animation != 123) throw new ArgumentException("This source slice is only valid for ZipDive holding animation123.", nameof(input));
        short wanted = input.OnSurface ? input.InputHeading : unchecked((short)((input.CameraHeading + input.InputHeading) & 4095));
        if (input.SurfaceTransition != 0 || (input.Forward == 0 && input.Sideways == 0)) return new(false, wanted, input.RetargetFlag);
        int delta = (wanted - input.EffectiveHeading) & 4095;
        if (delta < 128 || delta > 3968) return new(false, wanted, input.RetargetFlag);
        int difference = (input.TurnTarget - wanted) & 4095;
        bool retarget = input.TurnRemaining == 0 || (difference >= 64 && difference <= 4032);
        return new(retarget, wanted, retarget ? (byte)0 : input.RetargetFlag);
    }
}
