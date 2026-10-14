namespace OpenSpidey.Core.Actors.LizMan;

/// <summary>Shared attack-slot globals at gGlobalLizMan 0x682C44 and gLizManAttackFlag 0x682B6E.</summary>
public sealed class LizManAttackOwnership
{
    public object? GlobalLizMan { get; set; }
    public byte AttackFlag { get; set; }
}

/// <summary>CLizMan::ClearAttackFlags, inlined into FlyAcrossRoom and the SetAttackFlags prologue.</summary>
public static class LizManAttackFlags
{
    public static void Clear(LizManState state, object identity, LizManAttackOwnership ownership)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(ownership);
        if (ReferenceEquals(ownership.GlobalLizMan, identity))
            ownership.GlobalLizMan = null;
        else if ((state.Field39C & 2) != 0)
            ownership.AttackFlag = unchecked((byte)(ownership.AttackFlag & ~state.Field39D));
        state.Field39C = 0;
        state.Field39D = 0;
    }
}
