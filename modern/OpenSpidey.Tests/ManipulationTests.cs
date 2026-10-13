using System.Text.Json;
using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Actors.Switches;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

internal static class ManipulationTests
{
    private sealed class Host : IManipulationHost, IManipulationDebrisHost
    {
        public List<int> Events { get; } = [];
        public int Seed { get; set; }
        public ManipulationWorldHit? Contact { get; set; }
        public int Random(int range)
        {
            Events.AddRange([1, range]);
            uint value = unchecked((uint)Seed * 1664525u + 1013904223u); Seed = unchecked((int)value);
            return range > 0 ? (int)((value >> 1) % (uint)range) : 0;
        }
        public ManipulationWorldHit? LineToItem(FixedVector3 start, FixedVector3 end, int mode)
        { Events.AddRange([4, start.X, start.Y, start.Z, end.X, end.Y, end.Z, mode]); return Contact; }
        public void HideEnvironmentModel(SwitchEnvironmentItem shadowItem) => Events.Add(2);
        public void SendAttachedPulse(ushort node) => Events.AddRange([3, node]);
        public IReadOnlyList<ManipulationTargetBody> SourceBaddyOrder { get; set; } = [];
        public bool IgnoreSphereRadius { get; set; }
        public void InitializeObjectLibrary() => Events.Add(1);
        public void AttachToEnvironmentalList(ManipulationState state) => Events.Add(2);
        public void SelectObjectModel(uint checksum) => Events.AddRange([3, unchecked((int)checksum), 7]);
        public SwitchEnvironmentItem? FindEnvironmentItem(uint checksum)
        { Events.AddRange([4, unchecked((int)checksum)]); return checksum == 0x12345678 ? new(new object(), checksum, default) : null; }
        public void HitBaddy(object identity, ActorHit hit) => Events.AddRange([5, hit.Flags, hit.AttackKind, hit.Damage,
            hit.Direction.X, hit.Direction.Y, hit.Direction.Z, hit.SlideDistance, hit.SlideFrames]);
        public void ShakeCamera(FixedVector3 position, int magnitude) => Events.AddRange([8, position.X, position.Y, position.Z, magnitude]);
        public void Chunk(ManipulationState state, FixedVector3 normal) => Events.AddRange([9, normal.X, normal.Y, normal.Z,
            state.Body.Position.X, state.Body.Position.Y, state.Body.Position.Z]);
    }
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ManipulationTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.manipulation.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var body = new ActorBodyMotion { Position = Vector(input, 1), Velocity = Vector(input, 4), Acceleration = Vector(input, 7),
                Angles = Angles(input, 10), AngularVelocity = Angles(input, 13), LinearDamping = new(12, 12, 12) };
            var state = new ManipulationState(9, body) { ItemFlags = (ushort)input[16], Flags = input[17], PulseSent = unchecked((byte)input[18]),
                ShadowItem = input[19] == 0 ? null : new(new object(), 0, default) };
            var host = new Host { Seed = input[20] };
            FixedVector3 point = Vector(input, 21), velocity = Vector(input, 24);
            JsonElement expected = test.GetProperty("expected");
            if (input[0] < 6)
            {
                switch (input[0])
                {
                    case 0: assert(ManipulationBehavior.TugImpulse(state, point, velocity, host.Random), "Original tug accepts the impulse."); break;
                    case 1: ManipulationBehavior.Drop(state, velocity, host.Random); break;
                    case 2: ManipulationBehavior.Throw(state, velocity, host.Random); break;
                    case 3: ManipulationBehavior.Pickup(state, host); ManipulationBehavior.Pickup(state, host); break;
                    case 4: ManipulationBehavior.ThrowAt(state, point, input[27], host.Random); break;
                    case 5: ManipulationBehavior.CalculateThrowPositions(state, point, input[27]); break;
                }
                int[] actual = [body.Position.X, body.Position.Y, body.Position.Z, body.Velocity.X, body.Velocity.Y, body.Velocity.Z,
                    body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z, body.Angles.X, body.Angles.Y, body.Angles.Z,
                    body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z, state.ItemFlags, state.Flags, state.PulseSent,
                    ManipulationBehavior.GetAttachPoint(state).X, ManipulationBehavior.GetAttachPoint(state).Y, ManipulationBehavior.GetAttachPoint(state).Z,
                    state.ThrowPositionCount, state.ThrowPositionIndex];
                assert(actual.SequenceEqual(expected.EnumerateArray().Take(23).Select(value => value.GetInt32())), $"Manipulation helper case{caseIndex} preserves source fields, flags, attachment and RNG-dependent angular velocity.");
                FixedVector3[] positions = expected[23].EnumerateArray().Select(value => new FixedVector3(value[0].GetInt32(), value[1].GetInt32(), value[2].GetInt32())).ToArray();
                assert((state.ThrowPositions ?? []).SequenceEqual(positions), $"Manipulation trajectory case{caseIndex} preserves truncation, first point, arc sine and final endpoint.");
                assert(host.Events.SequenceEqual(expected[24].EnumerateArray().Select(value => value.GetInt32())), $"Manipulation helper case{caseIndex} preserves RNG order, repeated shadow hiding and one-shot pulse.");
            }
            else
            {
                ManipulationDebrisState debris = ManipulationDebrisState.Create(0x12345678, default, default, _ => 0);
                debris.Body.Position = body.Position; debris.Body.Velocity = body.Velocity; debris.Body.Acceleration = body.Acceleration;
                debris.Body.Angles = body.Angles; debris.Body.AngularVelocity = body.AngularVelocity;
                debris.HitCount = input[18]; debris.RemainingTicks = input[27];
                int update = 0;
                foreach (JsonElement frame in expected.EnumerateArray())
                {
                    host.Events.Clear(); host.Contact = input[29 + update * 2] == 0 ? null : new(new object(), point,
                        new(unchecked((short)velocity.X), unchecked((short)velocity.Y), unchecked((short)velocity.Z)));
                    debris.Advance(input[28 + update * 2], host, host.Random);
                    int[] actual = [debris.Body.Position.X, debris.Body.Position.Y, debris.Body.Position.Z, debris.Body.Velocity.X, debris.Body.Velocity.Y, debris.Body.Velocity.Z,
                        debris.Body.Angles.X, debris.Body.Angles.Y, debris.Body.Angles.Z, debris.Body.AngularVelocity.X, debris.Body.AngularVelocity.Y, debris.Body.AngularVelocity.Z,
                        debris.RemainingTicks, debris.HitCount, debris.BodyFlags];
                    assert(actual.SequenceEqual(frame.EnumerateArray().Take(15).Select(value => value.GetInt32())), $"Manipulation debris case{caseIndex} update{update} preserves signed lifetime, per-tick damping, unmasked angles, hit counter and bounce.");
                    assert(host.Events.SequenceEqual(frame[15].EnumerateArray().Select(value => value.GetInt32())), $"Manipulation debris case{caseIndex} update{update} preserves actual world probe and bounce RNG order.");
                    update++;
                }
            }
            caseIndex++;
        }
        bool invalidTrajectory = false;
        try { ManipulationBehavior.CalculateThrowPositions(new(0, new()), default, 1); }
        catch (ArgumentOutOfRangeException) { invalidTrajectory = true; }
        assert(invalidTrajectory, "Managed trajectory rejects the original count-one divide-by-zero domain explicitly.");
        RunOriginalAi(assert);
        RunConstruction(assert);
    }
    private static void RunOriginalAi(Action<bool, string> assert)
    {
        using Stream stream = typeof(ManipulationTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.manipulation-ai.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            JsonElement input = test.GetProperty("input");
            var body = new ActorBodyMotion { Position = Vector(input.GetProperty("position")), Velocity = Vector(input.GetProperty("velocity")),
                Acceleration = Vector(input.GetProperty("acceleration")), Angles = Angles(input.GetProperty("angles")),
                AngularVelocity = Angles(input.GetProperty("angular")), LinearDamping = new((byte)input.GetProperty("damping")[0].GetInt32(),
                    (byte)input.GetProperty("damping")[1].GetInt32(), (byte)input.GetProperty("damping")[2].GetInt32()) };
            var state = new ManipulationState(9, body) { Flags = input.GetProperty("flags").GetInt32(), ImpactCount = (short)input.GetProperty("impact").GetInt32(),
                ThrowPositionCount = input.GetProperty("count").GetInt32(), ThrowPositionIndex = input.GetProperty("index").GetInt32(),
                ThrowPositions = input.GetProperty("pathPresent").GetBoolean() ? input.GetProperty("points").EnumerateArray().Select(Vector).ToArray() : null };
            object target = new();
            var host = new Host { Seed = input.GetProperty("seed").GetInt32(), IgnoreSphereRadius = input.GetProperty("ignore").GetInt32() != 0,
                SourceBaddyOrder = input.GetProperty("targetPresent").GetInt32() == 0 ? [] : [new(new(target, Vector(input.GetProperty("targetPosition")),
                    (ushort)input.GetProperty("targetRadius").GetInt32(), (uint)input.GetProperty("targetFlags").GetInt32()), (ushort)input.GetProperty("targetType").GetInt32())] };
            int update = 0;
            foreach (JsonElement frame in test.GetProperty("expected").EnumerateArray())
            {
                JsonElement step = input.GetProperty("steps")[update]; host.Events.Clear();
                host.Contact = step[1].GetInt32() == 0 ? null : new(new object(), Vector(input.GetProperty("contact")), Vector(input.GetProperty("normal")));
                ManipulationBehavior.Advance(state, step[0].GetInt32(), host, host.Random);
                int[] actual = [body.Position.X, body.Position.Y, body.Position.Z, body.Velocity.X, body.Velocity.Y, body.Velocity.Z,
                    body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z, body.Angles.X, body.Angles.Y, body.Angles.Z,
                    body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z, state.Flags, state.ImpactCount, state.BodyFlags,
                    state.ThrowPositionCount, state.ThrowPositionIndex, state.ThrowPositions is null ? 0 : 1];
                assert(actual.SequenceEqual(frame.EnumerateArray().Take(21).Select(value => value.GetInt32())),
                    $"Original PC manipulation AI case{caseIndex} update{update} preserves physics substeps, trajectory exhaustion, signed impact counter, angular wrapping and death.");
                assert(host.Events.SequenceEqual(frame[21].EnumerateArray().Select(value => value.GetInt32())),
                    $"Original PC manipulation AI case{caseIndex} update{update} preserves actual sphere scale6144, eligible enemy types, world endpoints, hits, camera, Chunk normal and RNG order.");
                update++;
            }
            caseIndex++;
        }
    }
    private static FixedVector3 Vector(JsonElement value) => new(value[0].GetInt32(), value[1].GetInt32(), value[2].GetInt32());
    private static ActorAngles Angles(JsonElement value) => new((short)value[0].GetInt32(), (short)value[1].GetInt32(), (short)value[2].GetInt32());
    private static void RunConstruction(Action<bool, string> assert)
    {
        using Stream stream = typeof(ManipulationTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.manipulation-construction.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            JsonElement input = test.GetProperty("input"), expected = test.GetProperty("expected");
            int nodeOffset = input.GetProperty("nodeOffset").GetInt32(), objectOffset = input.GetProperty("objectOffset").GetInt32();
            int absolute = nodeOffset + objectOffset + 18, padding = ((absolute + 3) & ~3) - absolute;
            uint[] chunks = input.GetProperty("chunks").EnumerateArray().Select(value => value.GetUInt32()).ToArray();
            byte[] packet = new byte[padding + 8 + (input.GetProperty("shadow").GetUInt32() == 0 ? 0 : (chunks.Length + 1) * 4) + 6];
            int cursor = padding;
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(cursor), input.GetProperty("model").GetUInt32()); cursor += 4;
            uint shadow = input.GetProperty("shadow").GetUInt32(); BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(cursor), shadow); cursor += 4;
            if (shadow != 0)
            {
                foreach (uint chunk in chunks) { BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(cursor), chunk); cursor += 4; }
                cursor += 4;
            }
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(cursor), (ushort)input.GetProperty("flags").GetInt32());
            BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(cursor + 2), (short)input.GetProperty("param").GetInt32());
            BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(cursor + 4), (short)input.GetProperty("sound").GetInt32());
            FixedVector3 position = Vector(input.GetProperty("position")); ActorAngles angles = Angles(input.GetProperty("angles"));
            var definition = new TriggerActorDefinition(9, 1, 401, [], [], new(position.X, position.Y, position.Z),
                new(angles.X, angles.Y, angles.Z), objectOffset, packet);
            var state = new ManipulationState(9, new()); var host = new Host();
            ManipulationBehavior.Construct(state, definition, nodeOffset, host);
            long[] actual = [state.Body.Position.X, state.Body.Position.Y, state.Body.Position.Z, state.Body.Angles.X, state.Body.Angles.Y, state.Body.Angles.Z,
                state.ItemFlags, ManipulationState.Type, state.Node, state.Body.LinearDamping.X, state.Body.LinearDamping.Y, state.Body.LinearDamping.Z,
                state.Flags, state.Parameter108, state.ImpactSound, state.ModelChecksum, state.ShadowItem is null ? 0 : 1];
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(17).Select(value => value.GetInt64())), $"Manipulation constructor{caseIndex} preserves source OBJ checksum, ENV shadow, flags, signed parameters, damping and wrapped placement.");
            assert(state.ChunkModels.SequenceEqual(chunks) && host.Events.SequenceEqual(expected[17].EnumerateArray().Select(value => unchecked((int)value.GetInt64()))),
                $"Manipulation constructor{caseIndex} preserves absolute cursor alignment, zero shadow as chunk terminator, source host call order and chunk model list.");
            caseIndex++;
        }
    }
    private static FixedVector3 Vector(int[] input, int offset) => new(input[offset], input[offset + 1], input[offset + 2]);
    private static ActorAngles Angles(int[] input, int offset) => new((short)input[offset], (short)input[offset + 1], (short)input[offset + 2]);
}
