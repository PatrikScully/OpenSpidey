using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Concrete waypoint, hiding and recovery fields layered over Jonah's canonical AI owners.</summary>
public sealed class JonahLocomotionState(JonahBrainState brain)
{
    public JonahBrainState Brain { get; } = brain ?? throw new ArgumentNullException(nameof(brain));
    public JonahState Jonah => Brain.Jonah;
    public int WaypointRole { get; set; } // Source +0x2F8, independent of the waypoint node ID.
    public int MovementRestart { get; set; } // Source +0x310; following a waypoint resets it.
    public int HitReactionContext { get; set; } // Source +0x318.
    public int ObstructionUpdates { get; set; } // Source +0x330.
    public PlayerKickHandle HidingPlace { get; set; } // Source +0x364; pointer clearing preserves validation.
    public int NextRoomWaypoint { get; set; } // Source +0x36C.
    public int HasNextRoomWaypoint { get; set; } // Source +0x370, source checks nonzero rather than exactly1.
    public void SelectWaypoint(int node, int role) { Jonah.CurrentWaypoint = node; WaypointRole = role; }
}
