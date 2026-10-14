namespace OpenSpidey.Core.Actors.Rhino;

/// <summary>CRhino::DetermineFightState at 0x481300 (LineOfSightCheck, path probe, distance bands).</summary>
public static class RhinoDetermineFightState
{
    public static bool TryAdvance(RhinoState state, IRhinoHost host, int argument) =>
        host.TryDetermineFightState(state, argument);
}
