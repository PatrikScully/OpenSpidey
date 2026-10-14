using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Torch;
using OpenSpidey.Core.Mathematics;

internal static class TorchTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(TorchTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.torch.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0, counts = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            string mode = test.GetProperty("mode").GetString()!;
            JsonElement expected = test.GetProperty("expected");
            if (mode == "P")
            {
                int[] input = Words(test.GetProperty("input"));
                ActorBodyMotion body = new() { Position = Vector(input, 2), Velocity = Vector(input, 5),
                    Acceleration = Vector(input, 8), Angles = Angles(input, 11), AngularVelocity = Angles(input, 14),
                    AngularAcceleration = Angles(input, 17), AngularDamping = new((byte)input[20], (byte)input[21], (byte)input[22]) };
                TorchMotion.Advance(body, input[0], input[1]);
                int[] actual = Snapshot(body), reference = Words(expected);
                assert(actual.SequenceEqual(reference), $"Actual PC CTorch::DoPhysics case {index}: actual=[{string.Join(',', actual)}], expected=[{string.Join(',', reference)}].");
            }
            else if (mode == "C") Construct(assert, index, Words(test.GetProperty("input")), expected);
            else if (mode == "V") Script(assert, index, test, expected);
            else Ai(assert, index, test, expected);
            index++; counts++;
        }
        assert(counts == 472, "Actual PC CTorch fixture keeps all constructor, physics, interpreter and AI cases.");
    }

    private static void Construct(Action<bool, string> assert, int index, int[] input, JsonElement expected)
    {
        TorchState state = new(input[2], input[0], 0) { BodyFlags = 0 };
        Host host = new(state, []) { AnimationId = 0, ItemFlags = 0, FastForward = input[1] != 0 };
        host.Body.Position = new FixedVector3(input[3], input[4], input[5]) << 12;
        host.Body.Angles = new((short)input[6], (short)input[7], (short)input[8]);
        host.Events.AddRange([14, 15, 1]);
        TorchBrain.Construct(state, host, () => host.Events.AddRange([16, 0x56E990]));
        int[] actual = [host.ItemFlags, (int)state.BodyFlags, state.SurfaceClearance, state.PreviousTicks, state.OlderTicks,
            host.AnimationId, (int)TorchState.LightAddress, TorchState.Type, state.Stage, state.SourceNode, state.MinorRadius, 18,
            state.DeathPulseSent ? 1 : 0];
        int[] reference = expected.EnumerateArray().Take(13).Select(value => value.GetInt32()).ToArray();
        assert(actual.SequenceEqual(reference) && Snapshot(host.Body).SequenceEqual(Words(expected[13])) &&
               host.Events.SequenceEqual(Words(expected[14])),
            $"Actual PC CTorch constructor case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', reference)}]; events=[{string.Join(',', host.Events)}] expected=[{string.Join(',', Words(expected[14]))}].");
    }

    private static void Script(Action<bool, string> assert, int index, JsonElement test, JsonElement expected)
    {
        Host host = new(new(0, 0x201, 0), Words(test.GetProperty("program"))) { AnimationId = 12 };
        CinematicActorScriptRuntime runtime = host.Runtime;
        runtime.Begin();
        int frameIndex = 0;
        foreach (JsonElement frame in test.GetProperty("frames").EnumerateArray())
        {
            int[] input = Words(frame);
            host.Body.Position = Vector(input, 4); host.AnimationFinished = input[2] != 0;
            host.SignalFlags = (ushort)input[3]; host.Events.Clear();
            runtime.Update(input[0], input[1] != 0);
            JsonElement row = expected[frameIndex++];
            int[] scalar = [runtime.Running ? 1 : 0, runtime.Decoding ? 1 : 0, runtime.ElapsedTicks, runtime.Cursor,
                runtime.LookTargetNode, runtime.CheckpointNode, host.ItemFlags, host.AnimationId, host.AnimationFinished ? 1 : 0, host.SignalFlags];
            int[] expectedScalar = row.EnumerateArray().Take(10).Select(value => value.GetInt32()).ToArray();
            assert(scalar.SequenceEqual(expectedScalar) && Snapshot(host.Body).SequenceEqual(Words(row[10])) &&
                   runtime.CommandTypes.SequenceEqual(Words(row[11])) && host.Events.SequenceEqual(Words(row[12])),
                $"Actual PC CTorch interpreter case {index}, frame {frameIndex}: scalar=[{string.Join(',', scalar)}] expected=[{string.Join(',', expectedScalar)}]; " +
                $"motion=[{string.Join(',', Snapshot(host.Body))}] expected=[{string.Join(',', Words(row[10]))}]; " +
                $"commands=[{string.Join(',', runtime.CommandTypes)}] expected=[{string.Join(',', Words(row[11]))}]; " +
                $"events=[{string.Join(',', host.Events)}] expected=[{string.Join(',', Words(row[12]))}].");
        }
    }

    private static void Ai(Action<bool, string> assert, int index, JsonElement test, JsonElement expected)
    {
        int[] h = Words(test.GetProperty("header"));
        TorchState state = new(h[15], h[0], 0)
        {
            BodyFlags = 0, Stage = h[1], Substate = h[2], BehaviorFlags = (uint)h[5], GravityAcceleration = h[7],
            GroundY = (short)h[8], PreviousTicks = h[9], OlderTicks = h[10], CombatFlags = (uint)h[16]
        };
        Host host = new(state, Words(test.GetProperty("program")))
        {
            AnimationId = h[3], AnimationFrame = h[4], ItemFlags = (ushort)h[6], HasSourceJoints = h[11] != 0,
            HeadAngles = new((short)h[12], (short)h[13]), HasMessages = h[14] != 0, PlayerPosition = new(h[17], 0, h[18])
        };
        host.Body.Position = Vector(h, 19); host.Body.Velocity = Vector(h, 22); host.Body.AngularDamping = new(1, 1, 1);
        TorchBrain brain = new(state, host, host.Runtime);
        int frameIndex = 0;
        foreach (JsonElement frame in test.GetProperty("frames").EnumerateArray())
        {
            int[] input = Words(frame);
            host.AnimationFinished = input[2] != 0; host.SignalFlags = (ushort)input[3];
            host.FastForward = input[1] != 0; host.Events.Clear();
            brain.Advance(input[0]);
            JsonElement row = expected[frameIndex++];
            CinematicActorScriptRuntime runtime = host.Runtime;
            int[] scalar = [state.Stage, state.Substate, state.GravityAcceleration, state.GroundY, state.StepScale,
                state.PreviousTicks, state.OlderTicks, (int)state.BehaviorFlags, host.ItemFlags, (int)state.BodyFlags,
                host.AnimationId, host.AnimationFinished ? 1 : 0, runtime.Running ? 1 : 0, runtime.Decoding ? 1 : 0,
                runtime.ElapsedTicks, runtime.Cursor, host.HasSourceJoints ? host.HeadAngles.Pitch : -99999,
                host.HasSourceJoints ? host.HeadAngles.Yaw : -99999, state.DeathPulseSent ? 1 : 0, host.SignalFlags];
            int[] expectedScalar = row.EnumerateArray().Take(20).Select(value => value.GetInt32()).ToArray();
            assert(scalar.SequenceEqual(expectedScalar) && Snapshot(host.Body).SequenceEqual(Words(row[20])) &&
                   runtime.CommandTypes.SequenceEqual(Words(row[21])) && host.Events.SequenceEqual(Words(row[22])),
                $"Actual PC CTorch::AI case {index}, frame {frameIndex}: scalar=[{string.Join(',', scalar)}] expected=[{string.Join(',', expectedScalar)}]; " +
                $"motion=[{string.Join(',', Snapshot(host.Body))}] expected=[{string.Join(',', Words(row[20]))}]; " +
                $"commands=[{string.Join(',', runtime.CommandTypes)}] expected=[{string.Join(',', Words(row[21]))}]; " +
                $"events=[{string.Join(',', host.Events)}] expected=[{string.Join(',', Words(row[22]))}].");
        }
        host.Runtime.ClearCommands();
    }

    private static int[] Words(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
    private static FixedVector3 Vector(int[] words, int offset) => new(words[offset], words[offset + 1], words[offset + 2]);
    private static ActorAngles Angles(int[] words, int offset) => new(unchecked((short)words[offset]), unchecked((short)words[offset + 1]), unchecked((short)words[offset + 2]));
    private static int[] Snapshot(ActorBodyMotion body) => [body.Position.X, body.Position.Y, body.Position.Z,
        body.Velocity.X, body.Velocity.Y, body.Velocity.Z, body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z,
        body.Angles.X, body.Angles.Y, body.Angles.Z, body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z,
        body.AngularAcceleration.X, body.AngularAcceleration.Y, body.AngularAcceleration.Z];

    private sealed class Host : ITorchHost
    {
        private readonly TorchState state;
        public Host(TorchState state, int[] words)
        {
            this.state = state;
            byte[] program = new byte[words.Length * 2];
            for (int word = 0; word < words.Length; word++)
                BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(word * 2), unchecked((short)words[word]));
            Runtime = new(new ActorScriptCursor(program), this, CinematicActorScriptProfile.Torch);
        }
        public CinematicActorScriptRuntime Runtime { get; }
        public ActorBodyMotion Body { get; } = new();
        public int SurfaceClearance => 100;
        public int AnimationId { get; set; }
        public int AnimationFrame { get; set; }
        public bool AnimationFinished { get; set; }
        public ushort ItemFlags { get; set; }
        public ushort SignalFlags { get; set; }
        public bool FastForward { get; set; }
        public bool FastForwardRequested => FastForward;
        public bool HasMessages { get; set; }
        public bool HasSourceJoints { get; set; }
        public AimAngles HeadAngles { get; set; }
        public FixedVector3 PlayerPosition { get; init; }
        public List<int> Events { get; } = [];
        public FixedVector3 TriggerPosition(int node) { Events.AddRange([1, node]); return new(node * 400 * 4096, node * -80 * 4096, node * 130 * 4096); }
        public IReadOnlyList<ushort> TriggerLinks(int node) { Events.AddRange([2, node]); return new ushort[] { 7 }; }
        public int GroundHeight(FixedVector3 position, int above, int below, int flags)
        { Events.AddRange([3, position.X, position.Y, position.Z, above, below, flags]); return 160 * 4096; }
        public void RunAnimation(int animation) { Events.AddRange([4, animation, 0, -1]); AnimationId = animation; AnimationFinished = false; }
        public void CycleAnimation(int animation) { Events.AddRange([5, animation, 1]); AnimationId = animation; AnimationFinished = false; }
        public void PlayVoice(short bank, short track, short priority) => Events.AddRange([6, bank, track, priority]);
        public bool ConsumeAnimationSignal(CinematicActorSignalSource source)
        {
            if (source != CinematicActorSignalSource.InputFlags) throw new InvalidDataException("Actual PC CTorch wait15 consumes input44.");
            if ((SignalFlags & 1) == 0) return false;
            SignalFlags &= 0xFFFE; return true;
        }
        public void SendDeathPulse() => Events.AddRange([2, state.SourceNode, 8, 7]);
        public void MarkDead() { }
        public void BuildTransform() => Events.Add(9);
        public void UpdateShadow() => Events.Add(10);
        public void ApplyPosePacket() { HasSourceJoints = true; Events.AddRange([11, (int)TorchState.PosePacketAddress]); }
        public void DeleteAllMessages() { HasMessages = false; Events.AddRange([12, 1, 0]); }
        public void AdvanceTextureWibble() => Events.AddRange([13, 7]);
        public AimAngles LookTarget(int node) => throw new InvalidDataException("Torch AI fixtures keep the look target unset.");
    }
}
