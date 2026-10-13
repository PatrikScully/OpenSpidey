using System.Buffers.Binary;
using System.Text;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.UI;

namespace OpenSpidey.Core.Missions;

public readonly record struct MissionControlName(bool IsBound, string Name);

public interface IMissionControlNames
{
    MissionControlName Read(ushort sourceAction);
}

public interface IMissionTextMessageHost
{
    SimpleMissionMessage ShowSimpleMessage(ReadOnlyMemory<byte> text, uint x, uint y, uint duration);
    void ClearSimpleMessages();
}

/// <summary>Original whole-string '?' pattern matching and input-name substitutions, with bounded printf replacement.</summary>
public sealed class TutorialMessageSubstitution
{
    private readonly IReadOnlyList<OriginalMessageSubstitution> substitutions;
    private readonly IMissionControlNames controls;
    private readonly byte[][] buffers = Enumerable.Range(0, 9).Select(_ => new byte[256]).ToArray();

    public TutorialMessageSubstitution(OriginalMissionMessageData data, IMissionControlNames controls)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Substitutions.Count != 9) throw new InvalidDataException("Original tutorial substitution data requires its genuine nine source records.");
        substitutions = data.Substitutions;
        this.controls = controls ?? throw new ArgumentNullException(nameof(controls));
    }

    public ReadOnlyMemory<byte> Substitute(ReadOnlyMemory<byte> original)
    {
        string text = Encoding.Latin1.GetString(original.Span);
        int index = 0;
        while (index < substitutions.Count && !Matches(substitutions[index].Pattern, text)) index++;
        if (index == substitutions.Count) return original;
        OriginalMessageSubstitution match = substitutions[index];
        MissionControlName first = controls.Read(match.FirstAction), second = controls.Read(match.SecondAction);
        string[] names = second.IsBound ? [first.Name, second.Name] : first.IsBound ? [first.Name] : [];
        var result = new StringBuilder();
        int position = 0, name = 0;
        if (names.Length == 0) result.Append(match.Format);
        else while (position < match.Format.Length)
        {
            int placeholder = match.Format.IndexOf("%s", position, StringComparison.Ordinal);
            if (placeholder < 0) { result.Append(match.Format.AsSpan(position)); break; }
            result.Append(match.Format.AsSpan(position, placeholder - position));
            if (name >= names.Length)
                throw new InvalidDataException("Original tutorial format requests an unbound second keyboard name; bounded substitution cannot read a missing printf argument.");
            result.Append(names[name++]); position = placeholder + 2;
        }
        byte[] bytes = Encoding.Latin1.GetBytes(result.ToString());
        if (bytes.Length >= 256) throw new InvalidDataException("Tutorial substitution exceeds its original256-byte buffer.");
        bytes.CopyTo(buffers[index], 0);
        buffers[index][bytes.Length] = 0;
        // Actual messages share this source slot; later substitutions update already-live text references.
        return buffers[index];
    }

    private static bool Matches(string pattern, string text)
    {
        if (pattern.Length != text.Length) return false;
        for (int index = 0; index < pattern.Length; index++)
            if (pattern[index] != '?' && pattern[index] != text[index]) return false;
        return true;
    }
}

/// <summary>Original TextMessage198 and ClearTextMessages218 operations on the shared simple-message owner.</summary>
public sealed class MissionTextCommandHandler : IMissionCommandHandler
{
    private readonly MissionOpcode operation;
    private readonly IMissionTextMessageHost host;
    private readonly TutorialMessageSubstitution substitutions;
    public ushort Opcode => (ushort)operation;

    public MissionTextCommandHandler(MissionOpcode operation, IMissionTextMessageHost host,
        OriginalMissionMessageData data, IMissionControlNames controls)
    {
        if (operation is not (MissionOpcode.TextMessage or MissionOpcode.ClearTextMessages))
            throw new ArgumentOutOfRangeException(nameof(operation));
        this.operation = operation;
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        substitutions = new(data, controls);
    }

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode) throw new ArgumentException("Text command does not match its handler.", nameof(instruction));
        if (operation == MissionOpcode.ClearTextMessages)
        {
            if (instruction.Arguments.Length != 0) throw new InvalidDataException($"Node {context.Node.Id}: ClearTextMessages has no operands.");
            host.ClearSimpleMessages(); return;
        }
        ReadOnlySpan<byte> bytes = instruction.Arguments.Span;
        int terminator = bytes.IndexOf((byte)0);
        if (terminator < 0) throw new InvalidDataException($"Node {context.Node.Id}: TextMessage string has no terminator.");
        int absolute = checked(instruction.SourceOffset + 2 + terminator + 1);
        int operands = checked(terminator + 1 + (absolute & 1));
        if (bytes.Length != operands + 6) throw new InvalidDataException($"Node {context.Node.Id}: TextMessage requires an aligned string and three words.");
        host.ShowSimpleMessage(substitutions.Substitute(instruction.Arguments[..terminator]),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[operands..]),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[(operands + 2)..]),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[(operands + 4)..]));
    }

    public static IEnumerable<IMissionCommandHandler> CreateHandlers(IMissionTextMessageHost host,
        OriginalMissionMessageData data, IMissionControlNames controls) =>
        new[] { MissionOpcode.TextMessage, MissionOpcode.ClearTextMessages }.Select(operation => new MissionTextCommandHandler(operation, host, data, controls));
}
