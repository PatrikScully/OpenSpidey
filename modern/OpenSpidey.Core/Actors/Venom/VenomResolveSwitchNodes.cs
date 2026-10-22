using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;
namespace OpenSpidey.Core.Actors.Venom;

public readonly record struct VenomControlSwitch(object Identity, FixedVector3 Position);

/// <summary>CVenom::ResolveSwitchNodes (0x4EB690): map ID_SWITCH_TO subtype4 nodes to nearest type407 controls.</summary>
public static class VenomResolveSwitchNodes
{
    public static void Resolve(VenomState state, IVenomHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        state.SwitchNodeCount = 0;
        Array.Clear(state.SwitchNodeIndices);
        Array.Clear(state.SwitchTargets);

        int nodeCount = host.TriggerNodeCount;
        for (int index = 1; index < nodeCount; index++)
        {
            if (host.TriggerNodeTypeCode(index) != 1002 || host.TriggerNodeSubtype(index) != 4)
                continue;

            if (state.SwitchNodeCount >= state.SwitchNodeIndices.Length)
                throw new InvalidOperationException("More than 4 ID_SWITCH_TO nodes found.");

            int slot = state.SwitchNodeCount;
            state.SwitchNodeIndices[slot] = index;
            int terminal = FollowSwitchToChain(host, index);
            FixedVector3 targetPosition = host.TriggerPosition(terminal);

            uint bestDistance = uint.MaxValue;
            object? nearest = null;
            foreach (VenomControlSwitch control in host.EnumerateControlSwitches())
            {
                uint distance = unchecked((uint)FixedVectorMath.OriginalDistance(targetPosition, control.Position));
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                nearest = control.Identity;
            }

            if (bestDistance == uint.MaxValue)
                throw new InvalidOperationException("No switch found.");

            state.SwitchTargets[slot] = nearest;
            state.SwitchNodeCount++;
        }

        state.Field3B8 = (1 << state.SwitchNodeCount) - 1;
        state.ResolveSwitchNodesPending = false;
    }

    private static int FollowSwitchToChain(IVenomHost host, int startNode)
    {
        int linkNode = startNode;
        while (true)
        {
            (ushort kind, ushort target) = ReadLinkHead(host, linkNode);
            if (kind == 0)
                return linkNode;
            if (kind != 1)
                throw new InvalidOperationException("More than 1 linked node for GointToSwitches path.");
            linkNode = target;
        }
    }

    /// <summary>First link packet word pair at Trig_GetLinksPointer (0x4E3880).</summary>
    internal static (ushort Kind, ushort Target) ReadLinkHead(IVenomHost host, int node)
    {
        TriggerNode trigger = host.TriggerNode(node);
        ReadOnlySpan<byte> bytes = trigger.Data.Span;
        int offset = trigger.TypeCode switch
        {
            1 => 6,
            5 or 20 or 1002 => 4,
            2 or 3 or 6 or 8 or 9 or 10 or 12 or 13 or 1000 or 1001 => 2,
            _ => throw new InvalidDataException($"Node {trigger.Id}: type {trigger.TypeCode} has no supported link layout.")
        };
        if (offset + 4 > bytes.Length)
            throw new InvalidDataException($"Node {trigger.Id}: truncated link packet.");
        return (BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[(offset + 2)..]));
    }
}
