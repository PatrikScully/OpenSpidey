namespace OpenSpidey.Core.Actors.Venom;

/// <summary>CVenom::PulseL6A4Node (0x4F0F00): pulse the first type-1 node at the origin, or at (1000, 0, 0).</summary>
public static class VenomPulse
{
    public static void PulseL6A4Node(IVenomHost host, bool atOrigin)
    {
        ArgumentNullException.ThrowIfNull(host);
        for (int node = 1; node < host.TriggerNodeCount; node++)
        {
            if (host.TriggerNodeTypeCode(node) != 1) continue;
            var position = host.TriggerPosition(node);
            bool match = atOrigin
                ? (position.Z | position.Y | position.X) == 0
                : position.X == 0x3E8000 && (position.Z | position.Y) == 0;
            if (!match) continue;
            host.SendPulseToNode(node);
            return;
        }
        // Original only asserts "Node not found".
    }
}
