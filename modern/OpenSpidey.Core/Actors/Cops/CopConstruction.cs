namespace OpenSpidey.Core.Actors.Cops;

/// <summary>CCop::CCop (0x428750) before ParseScript and Trig_SetCreatedObjectSubType.</summary>
public static class CopConstruction
{
    public const uint InitialBehaviorFlags = 1;

    public static void Apply(CopState state, Action attachToBaddyList)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(attachToBaddyList);
        state.ShadowScale = 50;
        attachToBaddyList();
        state.Field212 = 60;
    }
}
