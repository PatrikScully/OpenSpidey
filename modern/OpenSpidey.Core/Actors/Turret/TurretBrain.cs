namespace OpenSpidey.Core.Actors.Turret;

/// <summary>CTurret constructor tail after SquirtPos/Angles; AI (0x4E49F0) stays in the exe for now.</summary>
public static class TurretBrain
{
    public static void Construct(TurretState state, Action attachToBaddyList)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(attachToBaddyList);
        attachToBaddyList();
        state.CombatFlags |= 0x10200;
        state.Field20C = 1;
    }
}
