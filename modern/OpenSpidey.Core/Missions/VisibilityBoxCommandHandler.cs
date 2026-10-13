using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Missions;

public enum SourceVisibilityList { StaticEnvironment, Baddy, EnvironmentBody, PowerUp }

/// <summary>A view of a canonical source item; changing visibility must update its existing flag and native owner.</summary>
public interface IVisibilityBoxItem
{
    FixedVector3 SourcePosition { get; }
    bool RegionUsable { get; }
    ushort ItemFlags { get; }
    void SetVisible(bool visible);
}

public interface IVisibilityBoxHost
{
    IEnumerable<IVisibilityBoxItem> SourceItems(SourceVisibilityList list);
}

public readonly record struct SourceVisibilityBox(FixedVector3 Minimum, FixedVector3 Maximum)
{
    public bool Selects(FixedVector3 position, bool inside)
    {
        bool x = position.X >= Minimum.X && position.X <= Maximum.X;
        bool y = position.Y >= Minimum.Y && position.Y <= Maximum.Y;
        bool z = position.Z >= Minimum.Z && position.Z <= Maximum.Z;
        // The original outside path skips the item if any axis is within its interval.
        return inside ? x && y && z : !x && !y && !z;
    }
}

/// <summary>Original141/192/209 word flags, absolute dword alignment and source visibility-list dispatch.</summary>
public sealed class VisibilityBoxCommandHandler : IMissionCommandHandler
{
    private readonly IVisibilityBoxHost host;
    public ushort Opcode { get; }

    public VisibilityBoxCommandHandler(ushort opcode, IVisibilityBoxHost host)
    {
        if (opcode is not (141 or 192 or 209)) throw new ArgumentOutOfRangeException(nameof(opcode));
        Opcode = opcode;
        this.host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length < 6)
            throw Error(context, "Visibility boxes require two flag words and a sentinel.");
        ReadOnlySpan<byte> bytes = instruction.Arguments.Span;
        bool visible = BinaryPrimitives.ReadUInt16LittleEndian(bytes) != 0;
        bool inside = BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]) != 0;
        int cursor = 4;
        while (true)
        {
            if (cursor > bytes.Length - 2) throw Error(context, "Visibility boxes have no terminating255 word.");
            if (BinaryPrimitives.ReadUInt16LittleEndian(bytes[cursor..]) == 255)
            {
                if (cursor + 2 != bytes.Length) throw Error(context, "Visibility boxes contain data after their sentinel.");
                return;
            }
            int absolute = checked(instruction.SourceOffset + 2 + cursor);
            cursor = checked(cursor + ((4 - (absolute & 3)) & 3));
            if (cursor > bytes.Length - 24) throw Error(context, "An aligned visibility box extends beyond the command.");
            var box = new SourceVisibilityBox(ReadVector(bytes[cursor..]), ReadVector(bytes[(cursor + 12)..]));
            cursor += 24;
            if (Opcode == 141)
            {
                Apply(SourceVisibilityList.StaticEnvironment, box, visible, inside);
                Apply(SourceVisibilityList.Baddy, box, visible, inside);
            }
            else if (Opcode == 192) Apply(SourceVisibilityList.Baddy, box, visible, inside);
            else
            {
                Apply(SourceVisibilityList.EnvironmentBody, box, visible, inside);
                Apply(SourceVisibilityList.PowerUp, box, visible, inside);
            }
        }
    }

    private void Apply(SourceVisibilityList list, SourceVisibilityBox box, bool visible, bool inside)
    {
        foreach (IVisibilityBoxItem item in host.SourceItems(list))
            if (item.RegionUsable && box.Selects(item.SourcePosition, inside)) item.SetVisible(visible);
    }

    private static FixedVector3 ReadVector(ReadOnlySpan<byte> bytes) => new FixedVector3(
        BinaryPrimitives.ReadInt32LittleEndian(bytes), BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]),
        BinaryPrimitives.ReadInt32LittleEndian(bytes[8..])) << 12;

    private static InvalidDataException Error(MissionCommandContext context, string message) =>
        new($"Node {context.Node.Id}: {message}");
}
