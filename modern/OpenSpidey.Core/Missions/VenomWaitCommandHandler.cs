using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>The CVenom virtual slots called by story commands220/221.</summary>
public interface IMissionVenom
{
    /// <summary>Vtable +0x44.</summary>
    void EnterWaitState();
    /// <summary>Vtable +0x48, both words zero-extended.</summary>
    void ExitWaitState(uint first, uint second);
}

/// <summary>Original220/221 (0x4E290B) dispatch to the first type0x139 baddy.</summary>
public sealed class VenomWaitCommandHandler : IMissionCommandHandler
{
    public const ushort VenomType = 0x139;
    private readonly IMissionBaddyLookup baddies;
    public ushort Opcode { get; }

    public VenomWaitCommandHandler(MissionOpcode operation, IMissionBaddyLookup baddies)
    {
        if (operation is not (MissionOpcode.VenomEnterWaitState or MissionOpcode.VenomExitWaitState))
            throw new ArgumentOutOfRangeException(nameof(operation));
        Opcode = (ushort)operation;
        this.baddies = baddies ?? throw new ArgumentNullException(nameof(baddies));
    }

    public static IEnumerable<IMissionCommandHandler> CreateHandlers(IMissionBaddyLookup baddies) =>
        new[] { MissionOpcode.VenomEnterWaitState, MissionOpcode.VenomExitWaitState }
            .Select(operation => new VenomWaitCommandHandler(operation, baddies));

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        bool exit = Opcode == (ushort)MissionOpcode.VenomExitWaitState;
        int length = exit ? 4 : 0;
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != length)
            throw new InvalidDataException($"Node {context.Node.Id}: Venom command{Opcode} requires its original{length}-byte operands.");
        object? body = baddies.FindBaddyOfType(VenomType);
        if (body is null)
        {
            // The PC skips221's operand advance on this path, so its two words execute as the next commands.
            if (exit) throw new InvalidOperationException(
                $"Node {context.Node.Id}: source221 without a live Venom reinterprets its operands as mission commands.");
            return;
        }
        IMissionVenom venom = body as IMissionVenom ?? throw new NotSupportedException(
            $"Source Venom command{Opcode} found a type0x139 baddy without the CVenom wait-state interface.");
        if (!exit) { venom.EnterWaitState(); return; }
        ReadOnlySpan<byte> words = instruction.Arguments.Span;
        venom.ExitWaitState(BinaryPrimitives.ReadUInt16LittleEndian(words), BinaryPrimitives.ReadUInt16LittleEndian(words[2..]));
    }
}
