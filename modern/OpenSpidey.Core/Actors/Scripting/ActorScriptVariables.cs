using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scripting;

public interface IActorScriptVariableWorld
{
    int Difficulty { get; }
    int MovieInputPolls { get; }
    FixedVector3? PlayerPosition { get; }
    bool HasLineOfSight(FixedVector3 from, FixedVector3 to);
    int Random(int range);
    ushort FirstLink(ushort nodeId);
    void SelectModel(uint checksum);
}

/// <summary>Recovered common actor variable bank. Numeric IDs remain only at the bytecode boundary.</summary>
public sealed class ActorScriptVariables(ActorScriptState state, IActorScriptVariableWorld world)
{
    private readonly short[] registers = new short[6];
    private readonly short[] attributes = new short[6];
    // These parameter roles are not recovered yet; retain their source IDs and storage widths.
    private readonly Dictionary<ushort, short> shortParameters = new() { [0x212E] = 32 };
    private readonly Dictionary<ushort, uint> dwordParameters = [];
    public FixedVector3 Position { get; set; }
    public short Health { get; set; }
    public ushort CollisionRadius { get; set; } = 128;
    public ushort Damage { get; set; }
    public ushort ItemFlags { get; set; }
    public short GroundOffset { get; set; }
    public short EyeHeightOffset { get; set; }
    public int AnimationSpeed { get; set; }
    public byte ByteParameter { get; set; }
    public IReadOnlyList<short> Registers => Array.AsReadOnly(registers);
    public IReadOnlyList<short> Attributes => Array.AsReadOnly(attributes);
    public IReadOnlyDictionary<ushort, short> ShortParameters => shortParameters;
    public IReadOnlyDictionary<ushort, uint> DwordParameters => dwordParameters;

    public short Read(ushort variable, ActorScriptCursor operands)
    {
        switch (variable)
        {
            case 0x2100: return Health;
            case 0x2120: return registers[RegisterIndex(operands.ReadWord())];
            case 0x2129: return unchecked((short)world.Random(operands.ReadWord()));
            case 0x212A: return unchecked((short)state.NodeId);
            case 0x212B:
                ushort node = operands.ReadWord();
                if ((node & 0x2000) != 0) node = unchecked((ushort)Read(node, operands));
                return unchecked((short)world.FirstLink(node));
            case 0x212C: return state.PulsesReceived;
            case 0x212D: return ByteParameter;
            case 0x212E: return shortParameters[variable];
            case 0x2132:
                if (world.PlayerPosition is not { } player) return 8191;
                FixedVector3 difference = (Position - player) >> 12;
                int squared = unchecked(difference.X * difference.X + difference.Z * difference.Z);
                return (short)Math.Min(FixedVectorMath.OriginalSquareRoot(squared), 8191);
            case 0x2133:
                if (world.PlayerPosition is not { } target) return 0;
                FixedVector3 from = Position with { Y = unchecked(Position.Y - (EyeHeightOffset << 12)) };
                return world.HasLineOfSight(from, target) ? (short)1 : (short)0;
            case 0x2136: return unchecked((short)world.MovieInputPolls);
            case 0x2139: return unchecked((short)world.Difficulty);
            case 0x2140: return unchecked((short)(Position.X >> 12));
            case 0x2141: return unchecked((short)(Position.Y >> 12));
            case 0x2142: return unchecked((short)(Position.Z >> 12));
            case 0x2150: return unchecked((short)((world.PlayerPosition?.X ?? 0) >> 12));
            case 0x2151: return unchecked((short)((world.PlayerPosition?.Y ?? 0) >> 12));
            case 0x2152: return unchecked((short)((world.PlayerPosition?.Z ?? 0) >> 12));
            default: throw new InvalidDataException($"Actor variable 0x{variable:X4} has no recovered getter.");
        }
    }

    public void Write(ushort variable, ActorScriptCursor operands)
    {
        switch (variable)
        {
            case 0x2100: Health = unchecked((short)operands.ReadWord()); break;
            case 0x2101: CollisionRadius = operands.ReadWord(); break;
            case 0x2103: Damage = operands.ReadWord(); break;
            case 0x2106: case 0x212E: shortParameters[variable] = unchecked((short)operands.ReadWord()); break;
            case 0x2120:
                int register = RegisterIndex(operands.ReadWord());
                registers[register] = operands.ReadScriptValue(Read);
                break;
            case 0x2121: AnimationSpeed = unchecked((short)operands.ReadWord()); break;
            case 0x2122: GroundOffset = unchecked((short)operands.ReadWord()); break;
            case 0x2125:
                int index = operands.ReadWord();
                if (index >= attributes.Length) throw new InvalidDataException("Actor attribute index exceeds six slots.");
                attributes[index] = unchecked((short)operands.ReadWord());
                break;
            case 0x212C: state.PulsesReceived = unchecked((byte)operands.ReadWord()); break;
            case 0x212D: ByteParameter = unchecked((byte)operands.ReadWord()); break;
            case 0x212F: world.SelectModel(operands.ReadAlignedDword()); break;
            case 0x2131:
                if (operands.ReadWord() != 0) ItemFlags |= 8;
                else ItemFlags &= unchecked((ushort)~8);
                break;
            case 0x2135: dwordParameters[variable] = operands.ReadAlignedDword(); break;
            case 0x2138: operands.ReadAlignedDword(); break; // Unused aligned source parameter.
            case 0x2140: Position = Position with { X = operands.ReadScriptValue(Read) << 12 }; break;
            case 0x2141: Position = Position with { Y = operands.ReadScriptValue(Read) << 12 }; break;
            case 0x2142: Position = Position with { Z = operands.ReadScriptValue(Read) << 12 }; break;
            default: throw new InvalidDataException($"Actor variable 0x{variable:X4} has no recovered setter.");
        }
    }

    private static int RegisterIndex(ushort operand)
    {
        int index = operand & 255;
        if (index >= 6) throw new InvalidDataException("Actor register index exceeds six slots.");
        return index;
    }
}
