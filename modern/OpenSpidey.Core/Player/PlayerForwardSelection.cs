namespace OpenSpidey.Core.Player;

public readonly record struct PlayerForwardSelectionInput(bool TargetHeld, bool AnyMovement, bool AllowStart,
    ushort EffectiveHeading, short InputHeading, short CameraHeading, ushort TurnTarget, bool HardSurface);

public interface IPlayerForwardSelectionHost
{
    void SetTargetTorsoAngle(short wanted, bool fast);
    void SelectAndRunAnimation(ushort animation);
}

/// <summary>Actual CheckForwards4BF8A0: held-target/input/clip gate, original roll and torso turn, source movement start clips.</summary>
public static class PlayerForwardSelection
{
    public static bool Check(PlayerCombatState combat, PlayerForwardSelectionInput input, IPlayerForwardSelectionHost host)
    {
        if ((input.TargetHeld && (combat.ActionState & 1) != 0) || combat.LookaroundActive || !input.AnyMovement ||
            combat.AnimationId is 234 or 227 or 281) return false;
        combat.FieldEA6 = 0;
        short wanted = combat.Field8E8 != 0 ? input.InputHeading : unchecked((short)((input.CameraHeading + input.InputHeading) & 4095));
        int delta = (wanted - input.EffectiveHeading) & 4095;
        if (delta is > 1536 and < 2560 && combat.AnimationId is 0 or 11 or 12 or 13)
        {
            host.SelectAndRunAnimation(31);
            combat.HeadingTurnTicksRemaining = 0;
            combat.ActionState = 0x400000;
            return true;
        }
        if (delta is >= 128 and <= 3968)
        {
            int difference = (input.TurnTarget - wanted) & 4095;
            if (combat.HeadingTurnTicksRemaining == 0 || difference is >= 64 and <= 4032)
            { host.SetTargetTorsoAngle(wanted, true); combat.Field551 = 0; }
            return false;
        }
        if (!input.AllowStart) return false;
        ushort animation;
        if (combat.IsCrawling) animation = input.HardSurface ? (ushort)58 : (ushort)57;
        else animation = combat.AnimationId switch { 200 => 197, 194 => 191, _ => 1 };
        combat.ActionState = 16;
        combat.FieldAD7 = 0;
        host.SelectAndRunAnimation(animation);
        return true;
    }
}
