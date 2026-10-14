using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>Original FindBaddyOfType (0x403F90): the first BaddyList body whose unsigned type word +0x38 matches.</summary>
public interface IMissionBaddyLookup
{
    object? FindBaddyOfType(ushort type);
}

/// <summary>The CChopper fields and virtual slots written by story commands203-212.</summary>
public interface IMissionChopper
{
    int Flags218 { get; set; }
    int Waypoint1F4 { set; }
    int Velocity348 { set; }
    int MinimumHeight350 { set; }
    int TargetAngle358 { set; }
    /// <summary>Vtable +0x44.</summary>
    void FireMissileAtWaypoint(uint waypoint);
    /// <summary>Vtable +0x48.</summary>
    void FireMachineGunAtWaypoint(uint waypoint, uint count);
    /// <summary>Vtable +0x4C; the PC pushes both words without clearing the upper halves.</summary>
    void SetFlag(ushort flag, short value);
}

/// <summary>Original203-212 chopper commands (0x4E296B-0x4E2B2E). Every operand is consumed even without a chopper.</summary>
public sealed class ChopperCommandHandler : IMissionCommandHandler
{
    public const ushort ChopperType = 0x13E;
    private static readonly MissionOpcode[] Operations = [MissionOpcode.SetChopperAngle, MissionOpcode.ChopperFollowWayPoints,
        MissionOpcode.ChopperContinue, MissionOpcode.ChopperVelocity, MissionOpcode.ChopperMinHeight, MissionOpcode.ChopperSetFlag,
        MissionOpcode.ChopperFireMissileAtWayPoint, MissionOpcode.ChopperFireMachineGunAtWayPoint];
    private readonly MissionOpcode operation;
    private readonly TriggerFile triggers;
    private readonly IMissionBaddyLookup baddies;
    public ushort Opcode => (ushort)operation;

    public ChopperCommandHandler(MissionOpcode operation, TriggerFile triggers, IMissionBaddyLookup baddies)
    {
        if (!Operations.Contains(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        this.operation = operation;
        this.triggers = triggers ?? throw new ArgumentNullException(nameof(triggers));
        this.baddies = baddies ?? throw new ArgumentNullException(nameof(baddies));
    }

    public static IEnumerable<IMissionCommandHandler> CreateHandlers(TriggerFile triggers, IMissionBaddyLookup baddies) =>
        Operations.Select(operation => new ChopperCommandHandler(operation, triggers, baddies));

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        int length = operation switch
        {
            MissionOpcode.ChopperContinue => 0,
            MissionOpcode.ChopperSetFlag or MissionOpcode.ChopperFireMachineGunAtWayPoint => 4,
            _ => 2
        };
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != length)
            throw new InvalidDataException($"Node {context.Node.Id}: chopper command{Opcode} requires its original{length}-byte operands.");
        object? body = baddies.FindBaddyOfType(ChopperType);
        if (body is null) return;
        IMissionChopper chopper = body as IMissionChopper ?? throw new NotSupportedException(
            $"Source chopper command{Opcode} found a type0x13E baddy without the CChopper story-command interface.");
        ReadOnlyMemory<byte> words = instruction.Arguments;
        ushort Word(int index) => BinaryPrimitives.ReadUInt16LittleEndian(words.Span[(index * 2)..]);
        switch (operation)
        {
            case MissionOpcode.SetChopperAngle: chopper.TargetAngle358 = Word(0) * 182 >> 4; break;
            case MissionOpcode.ChopperFollowWayPoints:
                chopper.Flags218 |= 1;
                chopper.Waypoint1F4 = Word(0);
                break;
            case MissionOpcode.ChopperContinue: chopper.Flags218 |= 4; break;
            case MissionOpcode.ChopperVelocity: chopper.Velocity348 = Word(0); break;
            case MissionOpcode.ChopperMinHeight: chopper.MinimumHeight350 = TriggerPositionY(context, Word(0)); break;
            case MissionOpcode.ChopperSetFlag: chopper.SetFlag(Word(0), unchecked((short)Word(1))); break;
            case MissionOpcode.ChopperFireMissileAtWayPoint: chopper.FireMissileAtWaypoint(Word(0)); break;
            case MissionOpcode.ChopperFireMachineGunAtWayPoint: chopper.FireMachineGunAtWaypoint(Word(0), Word(1)); break;
        }
    }

    /// <summary>Trig_GetPosition (0x4E3940) Y in Q12; unrecognized kinds leave the zeroed caller vector unchanged.</summary>
    private int TriggerPositionY(MissionCommandContext context, ushort node)
    {
        if (node >= triggers.Nodes.Count || triggers.Nodes[node].Id != node)
            throw new InvalidDataException($"Node {context.Node.Id}: ChopperMinHeight references missing node{node}.");
        TriggerNode target = triggers.Nodes[node];
        if (!TriggerPlacement.IsSupported(target.TypeCode)) return 0;
        return unchecked(TriggerPlacement.Read(target).Position.Y << 12);
    }
}
