namespace OpenSpidey.Core.Player;

public interface IPlayerTorsoAimFinalizerHost
{
    void SetTargetTorsoAngle(short wanted, bool fast);
    void LockTargetTorsoAngle();
}

/// <summary>Runs the source torso slice after the real camera-facing owner and before turn/velocity integration.</summary>
public interface IPlayerTorsoAimFinalizerFrameHost
{
    void UpdateTorsoAim(short inputHeading, short cameraHeading);
}

/// <summary>Complete original4B70D5..717F torso aim before heading integration; AE6 and E8C retain different roles.</summary>
public static class PlayerTorsoAimFinalizer
{
    public static short Update(PlayerMeleeState melee, short inputHeading, short? cameraHeading, IPlayerTorsoAimFinalizerHost host)
    {
        PlayerCombatState combat = melee.Combat;
        bool movement = melee.Input.Sideways != 0 || melee.Input.Forward != 0;
        short camera = cameraHeading ?? 0;
        if ((combat.ActionState & 16) != 0)
            host.SetTargetTorsoAngle(combat.Field8E8 != 0 ? inputHeading : unchecked((short)(inputHeading + camera)),
                combat.Field8E8 == 0 && combat.SynthesizedInputActive);
        else if (combat.ActionState == 1)
        {
            if (!movement) host.LockTargetTorsoAngle();
        }
        else if ((combat.ActionState & 0x4E) != 0)
        {
            // The original calculated speed is positive for every non-centred
            // signed16 heading; only its zero/nonzero test survives this slice.
            if (combat.FieldAE6 != 0 || !movement) host.LockTargetTorsoAngle();
            else host.SetTargetTorsoAngle(unchecked((short)(inputHeading + camera)), false);
        }
        return camera;
    }
}
