using OpenSpidey.Core.Rendering;

namespace OpenSpidey.Core.Actors.Scripting;

public interface IActorLinkedBodyTarget
{
    ushort SourceNodeId { get; }
    ushort ItemFlags { get; set; }
    SourceSuperItemDepth SourceDepth { get; }
}

public interface IActorLinkedBodyCommandHost
{
    IReadOnlyList<ushort> Links(ushort nodeId);
    IEnumerable<IActorLinkedBodyTarget> ActiveBaddies();
}

/// <summary>Inherited CBaddy450F/4511/4512: raw word, authored link order and every matching active BaddyList body.</summary>
public static class ActorLinkedBodyCommands
{
    public static bool TryExecute(ushort opcode, ActorScriptCursor operands, ushort nodeId, IActorLinkedBodyCommandHost host)
    {
        if (opcode is not (0x450F or 0x4511 or 0x4512)) return false;
        ArgumentNullException.ThrowIfNull(operands);
        ArgumentNullException.ThrowIfNull(host);
        // CBaddy_ReadOperand is deliberately not the variable-expression reader.
        ushort value = operands.ReadWord();
        foreach (ushort link in host.Links(nodeId))
        {
            // Source starts at the current global list head again for each link.
            foreach (IActorLinkedBodyTarget target in host.ActiveBaddies())
            {
                if (target.SourceNodeId != link) continue;
                switch (opcode)
                {
                    case 0x450F:
                        target.ItemFlags = value != 0 ? (ushort)(target.ItemFlags & 0xFFFE) : (ushort)(target.ItemFlags | 1);
                        break;
                    case 0x4511: target.SourceDepth.First = unchecked((short)value); break;
                    case 0x4512: target.SourceDepth.Second = unchecked((short)value); break;
                }
            }
        }
        return true;
    }
}
