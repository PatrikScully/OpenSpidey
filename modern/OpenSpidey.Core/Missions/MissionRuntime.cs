using System.Buffers.Binary;
using System.Collections.Frozen;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>Ordered mission event execution, with bounded work per frame and inspectable state.</summary>
public sealed class MissionRuntime : IMissionContactFrameHost
{
    private readonly TriggerFile file;
    private readonly IMissionActorFactory actorFactory;
    private readonly FrozenDictionary<ushort, IMissionCommandHandler> handlers;
    private readonly Dictionary<ushort, CommandPointState> points = [];
    private readonly Dictionary<uint, List<CommandPointState>> contacts = [];
    private readonly Dictionary<ushort, IReadOnlyList<TriggerInstruction>> programs = [];
    private readonly Queue<ushort> pending = [];
    private ProgramContinuation? continuation;
    private IMissionBlockingCommandHandler? barrier;
    private bool faulted;

    public IReadOnlyDictionary<ushort, CommandPointState> CommandPoints => points;
    public int PendingCount => pending.Count + (continuation is null ? 0 : 1);
    public bool IsBlocked => barrier?.IsBlocking == true;
    public event Action<MissionTrace>? Trace;
    public event Action<MissionCommandContext>? CommandListCompleted;

    public MissionRuntime(TriggerFile file, IMissionActorFactory actorFactory,
        IEnumerable<IMissionCommandHandler> handlers)
    {
        this.file = file;
        this.actorFactory = actorFactory;
        this.handlers = handlers.ToFrozenDictionary(handler => handler.Opcode);
        foreach (TriggerNode node in file.Nodes)
        {
            programs[node.Id] = TriggerCommandReader.Read(node);
            if (node.TypeCode is not (2 or 6 or 9)) continue;
            uint checksum = ReadChecksum(node);
            var point = new CommandPointState(node.Id, checksum);
            points.Add(node.Id, point);
            if (!contacts.TryGetValue(checksum, out List<CommandPointState>? matches))
                contacts.Add(checksum, matches = []);
            // Original lists prepend; duplicate checksums fire in reverse creation order.
            matches.Insert(0, point);
        }
    }

    public void QueueAutoexec(bool alternative = false)
    {
        if (alternative)
        {
            TriggerNode? node = file.Nodes.FirstOrDefault(node => node.TypeCode == (ushort)TriggerNodeKind.AlternativeAutoexec);
            if (node is not null) { Queue(node.Id); return; }
        }
        foreach (TriggerNode node in file.Nodes)
            if (node.TypeCode == (ushort)TriggerNodeKind.Autoexec) Queue(node.Id);
    }

    public void ReceivePulse(ushort nodeId)
    {
        EnsureHealthy();
        TriggerNode node = GetNode(nodeId);
        switch (node.TypeCode)
        {
            case 1: case 5: case 7: case 20:
                actorFactory.Spawn(node);
                break;
            case 6:
                CommandPointState point = points[nodeId];
                point.PulsesReceived = unchecked((byte)(point.PulsesReceived + 1));
                Queue(nodeId);
                break;
        }
    }

    public void BeginContactFrame()
    {
        foreach (CommandPointState point in points.Values) point.ContactThisFrame = false;
    }

    public void NotifyContact(uint checksum)
    {
        EnsureHealthy();
        if (!contacts.TryGetValue(checksum, out List<CommandPointState>? matches)) return;
        foreach (CommandPointState point in matches)
        {
            point.ContactThisFrame = true;
            if (point.ExecutedForContact) continue;
            point.ExecutedForContact = true;
            Queue(point.NodeId);
        }
    }

    /// <summary>Original Trig_TriggerCommandPoint: mark visited matches and queue the first unexecuted one.</summary>
    public CommandPointState? TriggerCommandPoint(uint checksum)
    {
        EnsureHealthy();
        if (!contacts.TryGetValue(checksum, out List<CommandPointState>? matches)) return null;
        foreach (CommandPointState point in matches)
        {
            point.ContactThisFrame = true;
            if (point.ExecutedForContact) continue;
            Queue(point.NodeId);
            point.ExecutedForContact = true;
            return point;
        }
        return null;
    }

    public void EndContactFrame()
    {
        foreach (CommandPointState point in points.Values)
            if (!point.ContactThisFrame) point.ExecutedForContact = false;
    }

    public void Queue(ushort nodeId)
    {
        EnsureHealthy();
        TriggerNode node = GetNode(nodeId);
        pending.Enqueue(nodeId);
        Trace?.Invoke(new(MissionTraceKind.Queued, nodeId, null, node.SourceOffset));
    }

    /// <summary>Trig_ZeroPendingList discards queued points without changing the executing command list or contact state.</summary>
    public void ClearPendingEvents()
    {
        EnsureHealthy();
        pending.Clear();
    }

    /// <summary>Includes events appended during execution. Cycles yield to the next frame.</summary>
    public int ExecutePending(int eventBudget = 1024)
    {
        EnsureHealthy();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventBudget);
        int executed = 0;
        while (PendingCount > 0 && executed < eventBudget)
        {
            if (IsBlocked) return executed;
            ProgramContinuation? resume = continuation;
            ushort nodeId = resume?.NodeId ?? pending.Dequeue();
            TriggerNode node = GetNode(nodeId);
            barrier = null;
            try { continuation = ExecuteProgram(node, resume?.Index ?? 0, resume?.InsideIf ?? false); }
            catch
            {
                faulted = true; // Never replay a partially executed list after a command failure.
                Trace?.Invoke(new(MissionTraceKind.Faulted, nodeId, null, node.SourceOffset));
                throw;
            }
            if (continuation is null) executed++;
        }
        if (pending.TryPeek(out ushort next))
            Trace?.Invoke(new(MissionTraceKind.BudgetReached, next, null, GetNode(next).SourceOffset));
        return executed;
    }

    private ProgramContinuation? ExecuteProgram(TriggerNode node, int start, bool insideIf)
    {
        IReadOnlyList<TriggerInstruction> program = programs[node.Id];
        // Original ExecuteCommandList binds pCommandPoint only for kind6. Static kind2/9 contacts own
        // registry state, but their SendPulse bypasses NumPulses rather than inheriting the initial zero.
        CommandPointState? point = node.TypeCode == 6 ? points[node.Id] : null;
        for (int index = start; index < program.Count; index++)
        {
            TriggerInstruction instruction = program[index];
            Trace?.Invoke(new MissionTrace(MissionTraceKind.CommandStarted, node.Id, instruction.Opcode, instruction.SourceOffset)
            { SourceKind = instruction.SourceKind });
            switch ((MissionOpcode)instruction.Opcode)
            {
                case MissionOpcode.IfPulseCount:
                    if (point is null || insideIf)
                        throw CommandError(node, instruction, "IfPulseCount requires a command point and cannot be nested.");
                    if (point.PulsesReceived == ReadWord(instruction)) insideIf = true;
                    else
                    {
                        while (++index < program.Count && program[index].Opcode != (ushort)MissionOpcode.EndIf)
                            if (program[index].Opcode == (ushort)MissionOpcode.IfPulseCount)
                                throw CommandError(node, program[index], "Nested IfPulseCount.");
                        if (index == program.Count) throw CommandError(node, instruction, "Missing EndIf.");
                    }
                    break;
                case MissionOpcode.EndIf:
                    if (!insideIf) throw CommandError(node, instruction, "EndIf without IfPulseCount.");
                    insideIf = false;
                    break;
                case MissionOpcode.SetInitialPulses:
                    if (point is null) throw CommandError(node, instruction, "SetInitialPulses requires a command point.");
                    if (!point.InitialPulsesSet)
                    {
                        point.InitialPulsesSet = true;
                        point.RemainingPulses = ReadWord(instruction);
                    }
                    break;
                case MissionOpcode.SendPulse:
                    if (point is not null && point.RemainingPulses == 0) break;
                    foreach (ushort target in ReadLinks(node)) ReceivePulse(target);
                    if (point is not null && point.RemainingPulses != ushort.MaxValue) point.RemainingPulses--;
                    break;
                default:
                    if (!handlers.TryGetValue(instruction.Opcode, out IMissionCommandHandler? handler))
                        throw CommandError(node, instruction, "Gameplay handler has not been ported.");
                    handler.Execute(new(node, point?.PulsesReceived ?? 0), instruction);
                    if (handler is IMissionBlockingCommandHandler blocking && blocking.IsBlocking) barrier = blocking;
                    break;
            }
            Trace?.Invoke(new MissionTrace(MissionTraceKind.CommandCompleted, node.Id, instruction.Opcode, instruction.SourceOffset)
            { SourceKind = instruction.SourceKind });
            if (barrier is not null) return new(node.Id, index + 1, insideIf);
        }
        if (insideIf) throw new InvalidDataException($"Node {node.Id}: missing EndIf.");
        CommandListCompleted?.Invoke(new(node, point?.PulsesReceived ?? 0));
        return null;
    }

    private TriggerNode GetNode(ushort id)
    {
        if (id >= file.Nodes.Count || file.Nodes[id].Id != id)
            throw new ArgumentOutOfRangeException(nameof(id), id, "Mission node is missing.");
        return file.Nodes[id];
    }

    private static ushort ReadWord(TriggerInstruction instruction) =>
        BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span);

    public static IReadOnlyList<ushort> ReadLinks(TriggerNode node)
    {
        int offset = node.TypeCode switch
        {
            1 => 6,
            2 or 3 or 6 or 8 or 9 or 10 or 12 or 13 or 1000 or 1001 => 2,
            5 or 20 or 1002 => 4,
            _ => throw new InvalidDataException($"Node {node.Id}: type {node.TypeCode} has no supported link layout.")
        };
        ReadOnlySpan<byte> bytes = node.Data.Span;
        if (offset + 2 > bytes.Length) throw new InvalidDataException("Truncated mission links.");
        int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
        if (count * 2 > bytes.Length - offset - 2) throw new InvalidDataException("Truncated mission links.");
        var links = new ushort[count];
        for (int index = 0; index < count; index++)
            links[index] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset + 2 + index * 2, 2));
        return Array.AsReadOnly(links);
    }

    private static uint ReadChecksum(TriggerNode node)
    {
        if (node.TypeCode is 2 or 9) return TriggerEnvironmentReference.ReadChecksum(node);
        ReadOnlySpan<byte> bytes = node.Data.Span;
        if (bytes.Length < 4) throw new InvalidDataException("Truncated command point.");
        int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]);
        int offset = 4 + count * 2;
        offset += (4 - (node.SourceOffset + offset) % 4) % 4;
        if (offset > bytes.Length - 4) throw new InvalidDataException("Truncated command point checksum.");
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    }

    private static InvalidOperationException CommandError(TriggerNode node, TriggerInstruction instruction, string message) =>
        new($"Node {node.Id}, command {instruction.Opcode}, {instruction.SourceKind} coordinate 0x{instruction.SourceOffset:X}: {message}");

    private void EnsureHealthy()
    {
        if (faulted) throw new InvalidOperationException("Mission execution faulted; reload the session before continuing.");
    }

    private sealed record ProgramContinuation(ushort NodeId, int Index, bool InsideIf);
}
