using System.Buffers.Binary;

namespace OpenSpidey.Core.Actors.Scripting;

/// <summary>Recovered ParseScript/control-actor scheduling with bounded work and inspectable source cursors.</summary>
public sealed class ActorScriptRuntime(ActorScriptCursor initial, IActorScriptHost host)
{
    private const ushort Stop = (ushort)ActorScriptOpcode.Stop;
    private ActorScriptCursor cursor = initial.Clone();
    private readonly ActorScriptCursor?[] labels = new ActorScriptCursor?[8];
    private bool started;
    private bool faulted;
    private ActorScriptState State => host.ScriptState;
    public int SourcePosition => cursor.AbsolutePosition;

    public ActorScriptStep Start(ActorScriptEnvironment environment = default, int instructionBudget = 1024)
    {
        EnsureHealthy();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(instructionBudget);
        if (started) throw new InvalidOperationException("Actor script has already started.");
        started = true;
        State.Active = true;
        return Execute(environment, instructionBudget);
    }

    /// <summary>External waypoint parsing replaces the cursor while preserving the actor's labels and scheduling state.</summary>
    public ActorScriptStep ParseAt(ActorScriptCursor source, ActorScriptEnvironment environment = default, int instructionBudget = 1024)
    {
        EnsureHealthy();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(instructionBudget);
        if (State.Retired) throw new InvalidOperationException("A retired actor cannot parse a new waypoint script.");
        started = true;
        cursor = source.Clone();
        return Execute(environment, instructionBudget);
    }

    public ActorScriptStep Advance(int ticks, ActorScriptEnvironment environment = default, int instructionBudget = 1024)
    {
        EnsureHealthy();
        if (!started) throw new InvalidOperationException("Start the actor script before advancing it.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticks);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(instructionBudget);
        try { return AdvanceControlFrame(ticks, environment, instructionBudget); }
        catch (Exception exception) when (!faulted)
        {
            faulted = true;
            throw new InvalidOperationException($"Actor node {State.NodeId} scheduling failed: {exception.Message}", exception);
        }
    }

    /// <summary>Platforms first parse during AI, wait in updates, and retain their body after script completion.</summary>
    public ActorScriptStep AdvancePlatform(ActorScriptEnvironment environment = default, int instructionBudget = 1024)
    {
        EnsureHealthy();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(instructionBudget);
        if (State.ActorType != 402) throw new InvalidOperationException("Platform scheduling requires actor type 402.");
        if (!started)
        {
            started = true;
            State.Active = true;
        }
        State.SignalSnapshot = State.PendingSignals;
        if ((State.PendingSignals & 1) != 0)
        {
            State.PendingSignals &= 0xFFFE;
            State.PulsesReceived = unchecked((byte)(State.PulsesReceived + 1));
        }
        if (!State.Active || State.Retired) return default;
        if (State.WaitTicks != 0)
        {
            State.WaitTicks--;
            return default;
        }
        return Execute(environment, instructionBudget);
    }

    private ActorScriptStep AdvanceControlFrame(int ticks, ActorScriptEnvironment environment, int instructionBudget)
    {
        if (State.Retired) return default;
        State.SignalSnapshot = State.PendingSignals;
        if ((State.PendingSignals & 1) != 0)
        {
            State.PendingSignals &= 0xFFFE;
            State.PulsesReceived = unchecked((byte)(State.PulsesReceived + 1));
            if ((State.Flags & 0x80) != 0) host.FlashCompass();
        }
        if (!State.Active)
        {
            host.Retire();
            State.Retired = true;
            return default;
        }
        if (State.SkipDuringDeath && environment.FastForwardDeath) { State.WaitTicks = 0; State.WaitFrames = 0; }
        if (State.WaitTicks != 0)
        {
            State.WaitTicks = State.WaitTicks < ticks ? 0 : State.WaitTicks - ticks;
            return default;
        }
        if (State.WaitFrames != 0) { State.WaitFrames = unchecked(State.WaitFrames - 1); return default; }
        return Execute(environment, instructionBudget);
    }

    private ActorScriptStep Execute(ActorScriptEnvironment environment, int budget)
    {
        int instructions = 0;
        ushort opcode = 0;
        int source = cursor.AbsolutePosition;
        try
        {
            while (true)
            {
                source = cursor.AbsolutePosition;
                opcode = 0;
                if (cursor.PeekWord() == Stop) break;
                if (instructions == budget) return new(instructions, true);
                opcode = cursor.ReadWord();
                instructions++;
                if ((opcode & 0x6000) == 0) throw new InvalidDataException("Invalid actor script instruction word.");
                if ((opcode & 0x4000) != 0)
                {
                    if (!ExecuteCommand(opcode, environment)) return new(instructions, false);
                }
                else host.WriteVariable(opcode, cursor);
            }
            if (State.SkipDuringDeath && environment.FastForwardDeath)
            {
                if (State.TryMarkDeathPulseSent()) host.SendDeathPulse();
                State.BehaviorFlags |= 0x4000;
            }
            State.Active = false;
            return new(instructions, false);
        }
        catch (Exception exception)
        {
            faulted = true;
            throw new InvalidOperationException($"Actor node {State.NodeId}, command 0x{opcode:X4}, offset 0x{source:X}: {exception.Message}", exception);
        }
    }

    private bool ExecuteCommand(ushort opcode, ActorScriptEnvironment environment)
    {
        switch ((ActorScriptOpcode)opcode)
        {
            case ActorScriptOpcode.Jump: JumpToLabel(); return true;
            case ActorScriptOpcode.JumpAndYield:
                JumpToLabel();
                return State.ActorType == 203 && State.SkipDuringDeath && environment.FastForwardDeath;
            case ActorScriptOpcode.SetLabel: labels[ReadLabelIndex()] = cursor.Clone(); return true;
            case ActorScriptOpcode.DiscoverLabels: DefineLabels(); return true;
            case ActorScriptOpcode.SwitchScript: cursor = host.ScriptAtNode(ResolveOperand()).Clone(); return true;
            case ActorScriptOpcode.Yield: return false;
            case ActorScriptOpcode.Add: case ActorScriptOpcode.Subtract: UpdateVariable(opcode == (ushort)ActorScriptOpcode.Add); return true;
            case ActorScriptOpcode.IfGreater: case ActorScriptOpcode.IfLess: case ActorScriptOpcode.IfEqual:
                short first = unchecked((short)ResolveOperand());
                short second = unchecked((short)ResolveOperand());
                bool condition = (ActorScriptOpcode)opcode switch { ActorScriptOpcode.IfGreater => first > second, ActorScriptOpcode.IfLess => first < second, _ => first == second };
                if (!condition) SkipConditional();
                return true;
            case ActorScriptOpcode.IfAllFlags: case ActorScriptOpcode.IfNoFlags:
                ushort mask = cursor.ReadWord();
                bool matches = opcode == (ushort)ActorScriptOpcode.IfAllFlags ? (State.Flags & mask) == mask : (State.Flags & mask) == 0;
                if (!matches) SkipConditional();
                return true;
            case ActorScriptOpcode.IfWhatIf: if (!environment.WhatIf) SkipConditional(); return true;
            case ActorScriptOpcode.IfPlayerCeiling:
                short x = unchecked((short)cursor.ReadWord());
                short z = unchecked((short)cursor.ReadWord());
                if (!host.PlayerCeilingCheck(x, z)) SkipConditional();
                return true;
            case ActorScriptOpcode.IfSymbiote: if (!environment.Symbiote) SkipConditional(); return true;
            case ActorScriptOpcode.EndIf: return true;
            case ActorScriptOpcode.StopExecution: State.Active = false; return false;
            case ActorScriptOpcode.TimedMotion:
                // Two levels additionally simulate and record body physics; that work belongs to their actor adapter.
                if (environment.LevelId is 258 or 2051) return host.ExecuteCommand(opcode, cursor, environment);
                ushort durationWord = cursor.ReadWord();
                int duration = (durationWord & 0x2000) != 0 ? host.ReadVariable(durationWord, cursor) : durationWord;
                if (State.ActorType == 203 && State.SkipDuringDeath && environment.FastForwardDeath) return true;
                State.WaitTicks = duration;
                return false;
            case ActorScriptOpcode.WaitForSignal:
                if ((State.ActorType == 203 && State.SkipDuringDeath && environment.FastForwardDeath) || (State.SignalSnapshot & 1) != 0)
                    return true;
                cursor.Seek(cursor.Position - 2);
                return false;
            case ActorScriptOpcode.WaitTicks:
                ushort wait = ResolveOperand();
                if (State.ActorType == 203 && State.SkipDuringDeath && environment.FastForwardDeath) return true;
                State.WaitTicks = wait;
                return false;
            case ActorScriptOpcode.EnableDeathSkip: State.SkipDuringDeath = true; return true;
            default: return host.ExecuteCommand(opcode, cursor, environment);
        }
    }

    private ushort ResolveOperand()
    {
        ushort word = cursor.ReadWord();
        return (word & 0x2000) != 0 ? unchecked((ushort)host.ReadVariable(word, cursor)) : word;
    }

    private int ReadLabelIndex()
    {
        int index = cursor.ReadWord();
        if (index >= labels.Length) throw new InvalidDataException("Actor label exceeds the eight-entry table.");
        return index;
    }

    private void JumpToLabel()
    {
        int index = ReadLabelIndex();
        cursor = labels[index]?.Clone() ?? throw new InvalidDataException($"Actor label {index} has not been defined.");
    }

    private void DefineLabels()
    {
        int saved = cursor.Position;
        while (cursor.PeekWord() != Stop)
            if (cursor.ReadWord() == (ushort)ActorScriptOpcode.SetLabel) labels[ReadLabelIndex()] = cursor.Clone();
        cursor.Seek(saved);
    }

    private void UpdateVariable(bool add)
    {
        ushort variable = cursor.ReadWord();
        int selectorStart = cursor.Position;
        short previous = host.ReadVariable(variable, cursor);
        int selectorLength = cursor.Position - selectorStart;
        ReadOnlyMemory<byte> selectors = cursor.Source.Slice(selectorStart, selectorLength);
        ushort operand = ResolveOperand();
        short result = unchecked((short)(add ? previous + operand : previous - operand));
        byte[] relay = new byte[selectorLength + 2];
        selectors.Span.CopyTo(relay);
        BinaryPrimitives.WriteInt16LittleEndian(relay.AsSpan(selectorLength), result);
        // Native ADD/SUB relays getter-consumed selectors through SetVariable's operand parser.
        host.WriteVariable(variable, new ActorScriptCursor(relay));
    }

    private void SkipConditional()
    {
        int depth = 0;
        while (true)
        {
            ushort word = cursor.ReadWord();
            if (word == Stop) throw new InvalidDataException("Actor conditional has no matching EndIf.");
            if (word is >= (ushort)ActorScriptOpcode.IfGreater and <= (ushort)ActorScriptOpcode.IfSymbiote) depth++;
            if (word != (ushort)ActorScriptOpcode.EndIf) continue;
            if (depth == 0) return;
            depth--;
        }
    }

    private void EnsureHealthy()
    {
        if (faulted) throw new InvalidOperationException("Actor script faulted; recreate the actor before continuing.");
    }
}
