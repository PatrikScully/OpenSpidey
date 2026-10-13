using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Core.Actors.Scripting;

/// <summary>Recovered first-link waypoint traversal and the waypoint command state consumed by enemy behavior.</summary>
public sealed class ActorWaypointRoute(TriggerFile file, int initialNode, Action<ActorScriptCursor> parseScript)
{
    public int CurrentNode { get; private set; } = initialNode;
    public uint Flags { get; set; }
    public ushort Parameter { get; set; }
    public bool RunToNextPoint => (Flags & 1) != 0;
    public bool PauseAtNextPoint => (Flags & 2) != 0;

    public bool Advance()
    {
        if (CurrentNode < 0) return false;
        TriggerNode node = Node(CurrentNode);
        IReadOnlyList<ushort> links = node.TypeCode is 1 or 7 ? TriggerActorDefinition.Read(node).Links : MissionRuntime.ReadLinks(node);
        if (links.Count == 0) return false;
        CurrentNode = links[0];
        Flags = 0;
        TriggerNode target = Node(CurrentNode);
        if (target.TypeCode is 1000 or 1002)
        {
            TriggerScriptFragment fragment = TriggerPlacement.Read(target).ReadScriptAfterAngles(target);
            Parameter = 0;
            parseScript(new(fragment.Data, fragment.SourceOffset));
        }
        return true;
    }

    public FixedVector3 Position()
    {
        TriggerPosition position = TriggerPlacement.Read(Node(CurrentNode)).Position;
        return new FixedVector3(position.X, position.Y, position.Z) << 12;
    }

    public bool TryExecuteCommand(ushort opcode, ActorScriptCursor operands)
    {
        switch (opcode)
        {
            case 0x4702: Flags |= 1; return true;
            case 0x4703: Flags |= 2; return true;
            case 0x4704: case 0x4706: Flags |= 8; return true;
            case 0x4705: Flags |= 0x10; return true;
            case 0x4707: Parameter = operands.ReadWord(); return true;
            case 0x4708: Flags |= 0x20; return true;
            case 0x4709: Flags |= 0x40; return true;
            default: return false;
        }
    }

    private TriggerNode Node(int id) => (uint)id < file.Nodes.Count && file.Nodes[id].Id == id ? file.Nodes[id]
        : throw new InvalidDataException($"Actor waypoint references missing node {id}.");
}
