using System.Buffers.Binary;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

public interface IEnvironmentVisibilityHost
{
    /// <summary>Changes the first matching static ENV item; an absent checksum has no effect.</summary>
    void SetVisibility(uint checksum, bool visible);
}

/// <summary>Original named ENV lookup and flag0 policy, separate from dynamic actor visibility.</summary>
public static class EnvironmentVisibilityPolicy
{
    public static ushort SetVisible(ushort flags, bool visible) => visible
        ? (ushort)(flags & ~1) : (ushort)(flags | 1);

    public static void SetNamed(IEnvironmentVisibilityHost host, ReadOnlySpan<byte> prefix,
        ushort first, ushort last, bool visible)
    {
        ArgumentNullException.ThrowIfNull(host);
        // The PC writes into a sixteen-byte local buffer. Reject names that would overrun it.
        if (prefix.Length >= 13 || prefix.IndexOf((byte)0) >= 0)
            throw new InvalidDataException("A named ENV prefix must contain at most twelve nonzero original bytes.");
        Span<byte> name = stackalloc byte[prefix.Length + 2];
        prefix.CopyTo(name);
        for (int index = first; index <= last; index++)
        {
            // Source indices outside00..99 still use exactly two truncated character bytes.
            name[^2] = unchecked((byte)('0' + (index < 10 ? 0 : index / 10)));
            name[^1] = unchecked((byte)('0' + index % 10));
            host.SetVisibility(SourceAssetChecksum.Compute(name), visible);
        }
    }
}

/// <summary>SetVisibilityByName191: aligned Latin1 prefix, inclusive unsigned range, and nonzero visibility word.</summary>
public sealed class EnvironmentVisibilityCommandHandler(IEnvironmentVisibilityHost host) : IMissionCommandHandler
{
    public ushort Opcode => (ushort)MissionOpcode.SetEnvironmentVisibilityByName;

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (instruction.Opcode != Opcode) throw new ArgumentException("ENV visibility command does not match its handler.", nameof(instruction));
        ReadOnlySpan<byte> bytes = instruction.Arguments.Span;
        int terminator = bytes.IndexOf((byte)0);
        if (terminator < 0) throw new InvalidDataException($"Node {context.Node.Id}: named ENV prefix has no terminator.");
        int absolute = checked(instruction.SourceOffset + 2 + terminator + 1);
        int operands = checked(terminator + 1 + (absolute & 1));
        if (bytes.Length != operands + 6)
            throw new InvalidDataException($"Node {context.Node.Id}: named ENV visibility requires an aligned string and three words.");
        EnvironmentVisibilityPolicy.SetNamed(host, bytes[..terminator],
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[operands..]),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[(operands + 2)..]),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[(operands + 4)..]) != 0);
    }
}
