namespace OpenSpidey.Core.Combat;

public enum EnvironmentImpactAction { None, DestroyCrate, ShatterFace }

/// <summary>Actual PC Exp_HitEnvItem eligibility. Its 16-bit SHR AX,24 makes the stored crate-health value zero.</summary>
public static class EnvironmentImpactPolicy
{
    public static EnvironmentImpactAction Evaluate(bool hasItem, ushort itemFlags, bool belongsToEnvironment,
        ushort modelFlags, bool hasFace, int damage)
    {
        if (!hasItem || (itemFlags & 1) != 0 || !belongsToEnvironment) return EnvironmentImpactAction.None;
        if ((modelFlags & 2) != 0)
            return damage == 0xFFFF ? EnvironmentImpactAction.DestroyCrate : EnvironmentImpactAction.None;
        return hasFace ? EnvironmentImpactAction.ShatterFace : EnvironmentImpactAction.None;
    }
}
