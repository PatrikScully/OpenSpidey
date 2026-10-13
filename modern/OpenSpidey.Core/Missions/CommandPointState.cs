namespace OpenSpidey.Core.Missions;

/// <summary>State belongs to a mission session, never to an imported asset or fixed address.</summary>
public sealed class CommandPointState(ushort nodeId, uint checksum)
{
    public ushort NodeId { get; } = nodeId;
    public uint Checksum { get; } = checksum;
    public bool ContactThisFrame { get; internal set; }
    public bool ExecutedForContact { get; internal set; }
    public byte PulsesReceived { get; internal set; }
    public ushort RemainingPulses { get; internal set; }
    public bool InitialPulsesSet { get; internal set; }
}
