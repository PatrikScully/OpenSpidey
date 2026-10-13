using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;

internal static class ActorScriptTests
{
    public static void Run(Action<bool, string> assert)
    {
        VerifyRecoveredSequences(assert);
        var platformHost = new Host(actorType: 402);
        var platformRuntime = Create(platformHost, 0x4280, 3, 0x2120, 0, 7, 0x4100);
        platformHost.ScriptState.PendingSignals = 3;
        platformHost.ScriptState.Flags = 0x80;
        platformRuntime.AdvancePlatform();
        assert(platformHost.ScriptState.WaitTicks == 3 && platformHost.ScriptState.SignalSnapshot == 3 &&
            platformHost.ScriptState.PendingSignals == 2 && platformHost.ScriptState.PulsesReceived == 1 && platformHost.Flashes == 0,
            "Platforms defer their first parse to AI, latch signals and consume pulses without control-actor compass effects.");
        for (int frame = 0; frame < 3; frame++) platformRuntime.AdvancePlatform();
        assert(platformHost.ScriptState.WaitTicks == 0 && platformHost.Variables.Registers[0] == 0,
            "Platform timed motion waits three whole updates and resumes after the final decrement.");
        platformRuntime.AdvancePlatform();
        platformRuntime.AdvancePlatform();
        assert(!platformHost.ScriptState.Active && !platformHost.ScriptState.Retired && platformHost.Retirements == 0 &&
            platformHost.Variables.Registers[0] == 7,
            "A completed platform script retains its body rather than retiring as a control actor does.");
        var host = new Host();
        var runtime = Create(host, 0x2100, 32760, 0x2120, 0, 0x2100, 0x4110, 0x2120, 0, 50, 0x42B6, 3, 0x4111, 0x2120, 0, 1, 0x4100);
        runtime.Start();
        assert(host.Variables.Registers[0] == -32726 && host.ScriptState.WaitTicks == 3 && host.ScriptState.Active,
            "Actor arithmetic relays getter-consumed register selectors and wraps signed results before yielding.");
        runtime.Advance(2); runtime.Advance(2);
        assert(host.ScriptState.WaitTicks == 0 && host.Variables.Registers[0] == -32726,
            "The actor wait's final decrement does not execute the next command in that update.");
        runtime.Advance(2);
        assert(host.Variables.Registers[0] == -32727 && !host.ScriptState.Active && !host.ScriptState.Retired,
            "The following update resumes the script and stops before retirement.");
        runtime.Advance(2);
        assert(host.ScriptState.Retired && host.Retirements == 1, "An inactive control actor retires on its following update.");

        host = new();
        runtime = Create(host, 0x4281, 0x2120, 0, 7, 0x4100);
        runtime.Start(); runtime.Advance(2);
        assert(runtime.SourcePosition == 100 && host.ScriptState.Active, "Signal waits rewind to their own opcode until a signal arrives.");
        host.ScriptState.PulsesReceived = 255;
        host.ScriptState.Flags = 0x80;
        host.ScriptState.PendingSignals = 3;
        runtime.Advance(2);
        assert(host.ScriptState.PulsesReceived == 0 && host.ScriptState.PendingSignals == 2 && host.ScriptState.SignalSnapshot == 3 &&
            host.Flashes == 1 && host.Variables.Registers[0] == 7, "Signals latch before execution, wrap the pulse byte and preserve unrelated bits.");

        host = new();
        runtime = Create(host, 0x4114, 1, 2, 0x4114, 2, 2, 0x2120, 0, 9, 0x4120, 0x2120, 0, 8, 0x4120,
            0x2120, 0, 3, 0x4100);
        runtime.Start();
        assert(host.Variables.Registers[0] == 3, "A false actor conditional skips nested raw-word blocks through its matching EndIf.");

        host = new();
        runtime = Create(host, 0x4105, 0x4104, 0, 0x4110, 0x2120, 0, 1, 0x4102, 0, 0x4100);
        runtime.Start(); runtime.Advance(2); runtime.Advance(2);
        assert(host.Variables.Registers[0] == 3 && host.ScriptState.Active, "Label discovery and yielding jumps preserve the target cursor across updates.");

        host = new();
        runtime = Create(host, 0x42B7, 0x42B6, 100, 0x4100);
        runtime.Start(); runtime.Advance(2, new(FastForwardDeath: true)); runtime.Advance(2, new(FastForwardDeath: true));
        assert(host.DeathPulses == 1 && host.Retirements == 1 && host.ScriptState.DeathPulseSent,
            "Death fast-forward clears waits and sends the control actor's death pulse once before retirement.");

        host = new();
        runtime = Create(host, 0x4104, 0, 0x4110, 0x2120, 0, 1, 0x4101, 0, 0x4100);
        ActorScriptStep bounded = runtime.Start(instructionBudget: 5);
        assert(bounded.BudgetReached && host.ScriptState.Active && host.Variables.Registers[0] == 2,
            "Cyclic actor programs preserve their cursor and yield at the instruction budget.");

        host = new();
        runtime = Create(host, 0x4999, 0x4100);
        try { runtime.Start(); throw new Exception("Unported actor command was ignored."); }
        catch (InvalidOperationException exception)
        {
            assert(exception.Message.Contains("Actor node 42, command 0x4999, offset 0x64"),
                "Actor command failures report the original node, opcode and file offset.");
        }
        try { runtime.Advance(2); throw new Exception("Faulted actor program was replayed."); }
        catch (InvalidOperationException exception) { assert(exception.Message.Contains("recreate"), "Partly executed actor commands cannot replay after a fault."); }

        var bank = new Host();
        bank.Variables.Write(0x2120, Words(0x100, 0xFFFF));
        bank.Variables.Write(0x2140, Words(0xFFFF));
        assert(bank.Variables.Registers[0] == -1 && bank.Variables.Position.X == -4096,
            "Low-byte register aliases and signed literal position assignments retain GetScriptValue's high-bit guard.");
        bank.World.PlayerPosition = new(3 << 12, 999 << 12, 4 << 12);
        bank.Variables.Position = default;
        assert(bank.Variables.Read(0x2132, Words()) == 5, "Actor player distance uses horizontal source units independent of height.");
        bank.World.PlayerPosition = null;
        assert(bank.Variables.Read(0x2132, Words()) == 8191 && bank.Variables.Read(0x2150, Words()) == 0,
            "Missing players preserve the recovered distance sentinel and zero-coordinate getters.");
        bank.Variables.Write(0x212F, new(new byte[] { 0, 0, 0x78, 0x56, 0x34, 0x12 }, 102));
        assert(bank.World.SelectedModel == 0x12345678, "Model selection uses original file alignment instead of managed buffer alignment.");
        var text = new ActorScriptCursor(new byte[] { 65, 0, 66, 67, 0, 0 }, 100);
        assert(text.ReadString() == "A" && text.ReadString() == "BC" && text.Position == 6,
            "Embedded actor names advance to the next original word boundary.");
        host = new();
        host.Scripts.Add(17, Words(new ushort[] { 0x2120, 0, 12, 0x4100 }, 200));
        runtime = Create(host, 0x4106, 17, 0x4100);
        runtime.Start();
        assert(runtime.SourcePosition == 206 && host.Variables.Registers[0] == 12,
            "Script switches preserve the selected fragment's source offset and current actor variables.");
        host = new() { FailFlash = true };
        host.ScriptState.Flags = 0x80;
        host.ScriptState.PendingSignals = 1;
        runtime = Create(host, 0x4281, 0x4100);
        runtime.Start();
        try { runtime.Advance(2); throw new Exception("Failed actor scheduling callback was ignored."); }
        catch (InvalidOperationException exception) { assert(exception.Message.Contains("Actor node 42 scheduling failed"),
            "Scheduling callback failures retain their actor identity."); }
        try { runtime.Advance(2); throw new Exception("Partly applied actor scheduling was replayed."); }
        catch (InvalidOperationException exception) { assert(exception.Message.Contains("recreate") && host.Flashes == 1,
            "Partly applied signal callbacks cannot replay after a failure."); }
    }

    private static void VerifyRecoveredSequences(Action<bool, string> assert)
    {
        using Stream resource = typeof(ActorScriptTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-scripts.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var host = new Host();
            host.ScriptState.Flags = (uint)test.GetProperty("flags").GetInt32();
            host.ScriptState.WaitFrames = test.GetProperty("waitFrames").GetInt32();
            host.ScriptState.PulsesReceived = (byte)test.GetProperty("pulses").GetInt32();
            ushort[] words = test.GetProperty("words").EnumerateArray().Select(word => (ushort)word.GetInt32()).ToArray();
            var runtime = new ActorScriptRuntime(Words(words, 100), host);
            runtime.Start();
            JsonElement expected = test.GetProperty("expected");
            int update = 0;
            Check(expected[0]);
            foreach (JsonElement input in test.GetProperty("frames").EnumerateArray())
            {
                if (!host.ScriptState.Retired) host.ScriptState.PendingSignals |= (ushort)input[1].GetInt32();
                runtime.Advance(input[0].GetInt32(), new(input[2].GetInt32() != 0, input[3].GetInt32() != 0, input[4].GetInt32() != 0));
                Check(expected[++update]);
            }

            void Check(JsonElement result)
            {
                var state = host.ScriptState;
                int[] actual = [runtime.SourcePosition, state.Active ? 1 : 0, state.Retired ? 1 : 0, state.WaitTicks,
                    state.WaitFrames, state.PulsesReceived, state.SignalSnapshot, state.PendingSignals, state.DeathPulseSent ? 1 : 0,
                    host.DeathPulses, host.CompassTimer, host.Variables.Health, ..host.Variables.Registers.Select(value => (int)value),
                    unchecked((int)state.BehaviorFlags)];
                assert(actual.SequenceEqual(result.EnumerateArray().Select(value => value.GetInt32())),
                    $"Recovered actor sequence mismatch at update {update}, offset 0x{runtime.SourcePosition:X}.");
            }
        }
    }

    private static ActorScriptRuntime Create(Host host, params ushort[] words) => new(Words(words, 100), host);

    public static void VerifyOriginalFirstLevelControl(TriggerNode node, Action<bool, string> assert)
    {
        TriggerActorDefinition actor = TriggerActorDefinition.Read(node);
        var host = new Host(actor.NodeId);
        host.Variables.Position = new FixedVector3(actor.Position.X, actor.Position.Y, actor.Position.Z) << 12;
        var source = new ActorScriptCursor(actor.BehaviorData, checked(node.SourceOffset + actor.ObjectDataOffset + 18));
        var runtime = new ActorScriptRuntime(source, host);
        var environment = new ActorScriptEnvironment(LevelId: 257);
        runtime.Start(environment);
        assert(actor.ActorType == 203 && host.ScriptState.Active && host.ScriptState.WaitTicks == 128,
            "The original first-level control actor starts its recovered 128-tick wait.");
        for (int update = 0; update < 64; update++) runtime.Advance(2, environment);
        assert(host.ScriptState.Active && host.ScriptState.WaitTicks == 0,
            "The original control actor consumes exactly 64 two-tick updates before resumption.");
        runtime.Advance(2, environment);
        runtime.Advance(2, environment);
        assert(host.ScriptState.Retired && runtime.SourcePosition == source.SourceOffset + 4,
            "The original control actor stops at its actual terminator and retires on the following update.");
    }
    internal static ActorScriptCursor Words(ushort[] words, int sourceOffset)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        foreach (ushort word in words) writer.Write(word);
        return new(stream.ToArray(), sourceOffset);
    }
    internal static ActorScriptCursor Words(params ushort[] words) => Words(words, 0);

    internal sealed class Host : IActorScriptHost
    {
        public ActorScriptState ScriptState { get; }
        public VariableWorld World { get; } = new();
        public ActorScriptVariables Variables { get; }
        public int Retirements { get; private set; }
        public int DeathPulses { get; private set; }
        public int Flashes { get; private set; }
        public int CompassTimer { get; private set; }
        public Dictionary<ushort, ActorScriptCursor> Scripts { get; } = [];
        public bool FailFlash { get; init; }
        public Host(ushort nodeId = 42, short actorType = 203)
        {
            ScriptState = new() { NodeId = nodeId, ActorType = actorType };
            Variables = new(ScriptState, World);
        }
        public short ReadVariable(ushort variable, ActorScriptCursor operands) => Variables.Read(variable, operands);
        public void WriteVariable(ushort variable, ActorScriptCursor operands) => Variables.Write(variable, operands);
        public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment) =>
            throw new InvalidOperationException($"Actor command 0x{opcode:X4} has not been ported in this fixture.");
        public ActorScriptCursor ScriptAtNode(ushort nodeId) => Scripts[nodeId].Clone();
        public bool PlayerCeilingCheck(short x, short z) => false;
        public void FlashCompass()
        {
            Flashes++;
            if (FailFlash) throw new InvalidOperationException("Fixture callback failure.");
            CompassTimer = 300;
        }
        public void SendDeathPulse() => DeathPulses++;
        public void Retire() => Retirements++;
    }

    internal sealed class VariableWorld : IActorScriptVariableWorld
    {
        public int Difficulty => 1;
        public int MovieInputPolls => 0;
        public FixedVector3? PlayerPosition { get; set; }
        public uint SelectedModel { get; private set; }
        public bool HasLineOfSight(FixedVector3 from, FixedVector3 to) => true;
        public int Random(int range) => range / 2;
        public ushort FirstLink(ushort nodeId) => 0;
        public void SelectModel(uint checksum) => SelectedModel = checksum;
    }
}
