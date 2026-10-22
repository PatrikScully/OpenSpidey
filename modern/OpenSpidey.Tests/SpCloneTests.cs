using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.SpClone;
using OpenSpidey.Core.Mathematics;

internal static class SpCloneTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(SpCloneTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.spclone-ai.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0, calls = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            short[] program = test.GetProperty("program").EnumerateArray().Select(value => (short)value.GetInt32()).ToArray();
            int[] start = Words(test.GetProperty("start"));
            byte[] bytes = new byte[program.Length * 2];
            Buffer.BlockCopy(program, 0, bytes, 0, bytes.Length);
            var state = new SpCloneState(100, 0x704, 0)
            {
                AiState = start[0], AiSubstate = start[1], GroundY = (short)start[4], GravityAcceleration = start[5],
                CombatFlags = (uint)start[7], PreviousTicks = start[8], OlderTicks = start[9]
            };
            var host = new Host(start);
            var script = new CinematicActorScriptRuntime(new ActorScriptCursor(bytes), host, CinematicActorScriptProfile.SpiderClone);
            if (start[10] != 0) script.Begin();
            var brain = new SpCloneBrain(state, host, script);
            JsonElement[] expected = test.GetProperty("expected").EnumerateArray().ToArray();
            int frame = 0;
            foreach (JsonElement frameInput in test.GetProperty("frames").EnumerateArray())
            {
                int[] f = Words(frameInput);
                host.FastForwardRequested = f[1] != 0;
                if (f[2] >= 0) host.AnimationFinished = f[2] != 0;
                host.InputFlags |= (ushort)f[3];
                host.GroundResult = f[4];
                host.Events.Clear();
                brain.Advance(f[0]);
                int[] scalar = [state.AiState, state.AiSubstate, state.GravityAcceleration, state.GroundY, state.PreviousTicks, state.OlderTicks,
                    state.StepScale, host.ItemFlags, (ushort)state.BodyFlags, state.DeathPulseSent ? 1 : 0, host.AnimationId,
                    host.AnimationFinished ? 1 : 0, script.Running ? 1 : 0, script.Decoding ? 1 : 0, script.ElapsedTicks, script.Cursor,
                    script.LookTargetNode, host.InputFlags, host.HasSourceJoints ? 1 : 0, host.HeadAngles.Pitch, host.HeadAngles.Yaw];
                JsonElement wanted = expected[frame];
                int[] expectedScalar = wanted.EnumerateArray().Take(scalar.Length).Select(value => value.GetInt32()).ToArray();
                int[] motion = Snapshot(host.Body), expectedMotion = Words(wanted[scalar.Length]);
                int[] commands = script.CommandTypes.ToArray(), expectedCommands = Words(wanted[scalar.Length + 1]);
                int[] expectedEvents = Words(wanted[scalar.Length + 2]);
                assert(scalar.SequenceEqual(expectedScalar) && motion.SequenceEqual(expectedMotion) && commands.SequenceEqual(expectedCommands)
                    && host.Events.SequenceEqual(expectedEvents),
                    $"Original CSpClone::AI case{index} call{frame}: scalar=[{string.Join(',', scalar)}] expected=[{string.Join(',', expectedScalar)}]; " +
                    $"motion=[{string.Join(',', motion)}] expected=[{string.Join(',', expectedMotion)}]; commands=[{string.Join(',', commands)}] " +
                    $"expected=[{string.Join(',', expectedCommands)}]; events=[{string.Join(',', host.Events)}] expected=[{string.Join(',', expectedEvents)}].");
                frame++; calls++;
            }
            index++;
        }
        assert(index == 280 && calls > 2000, "The original CSpClone::AI fixture covers landing, bytecode and death sequences.");
    }

    private static int[] Words(JsonElement value) => value.EnumerateArray().Select(element => element.GetInt32()).ToArray();
    private static int[] Snapshot(ActorBodyMotion body) => [body.Position.X, body.Position.Y, body.Position.Z,
        body.Velocity.X, body.Velocity.Y, body.Velocity.Z, body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z,
        body.Angles.X, body.Angles.Y, body.Angles.Z, body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z,
        body.AngularAcceleration.X, body.AngularAcceleration.Y, body.AngularAcceleration.Z];

    private sealed class Host(int[] start) : ISpCloneHost
    {
        public ActorBodyMotion Body { get; } = new()
        {
            Position = new(start[14], start[15], start[16]), Velocity = new(start[17], start[18], start[19]),
            Angles = new((short)start[20], (short)start[21], (short)start[22]), AngularDamping = new(1, 1, 1)
        };
        public int SurfaceClearance => 100;
        public int AnimationId { get; private set; } = start[2];
        public bool AnimationFinished { get; set; } = start[3] != 0;
        public ushort ItemFlags { get; set; } = (ushort)start[6];
        public ushort InputFlags { get; set; }
        public int GroundResult { get; set; }
        public FixedVector3 PlayerPosition => new(start[23], start[24], start[25]);
        public bool FastForwardRequested { get; set; }
        public bool HasMessages => false;
        public bool HasSourceJoints { get; private set; } = start[11] != 0;
        public AimAngles HeadAngles { get; set; } = start[11] != 0 ? new((short)start[12], (short)start[13]) : default;
        public List<int> Events { get; } = [];
        public FixedVector3 TriggerPosition(int node) { Events.AddRange([1, node]); return new(node * 400 * 4096, node * -80 * 4096, node * 130 * 4096); }
        public IReadOnlyList<ushort> TriggerLinks(int node) { Events.AddRange([2, node]); return new ushort[] { 7 }; }
        public int GroundHeight(FixedVector3 position, int above, int below, int flags)
        { Events.AddRange([3, position.X, position.Y, position.Z, above, below, flags]); return GroundResult; }
        public void RunAnimation(int animation) { Events.AddRange([4, animation, 0, -1]); AnimationId = animation; AnimationFinished = false; }
        public void CycleAnimation(int animation) { Events.AddRange([5, animation, 1]); AnimationId = animation; AnimationFinished = false; }
        public void PlayVoice(short bank, short track, short priority) => Events.AddRange([6, bank, track, priority]);
        public bool ConsumeAnimationSignal(CinematicActorSignalSource source)
        {
            if (source != CinematicActorSignalSource.InputFlags || (InputFlags & 1) == 0) return false;
            InputFlags &= 0xFFFE; return true;
        }
        public void DeleteAllMessages() { }
        public AimAngles LookTarget(int node)
        {
            Events.Add(13); TriggerPosition(node);
            return FixedAim.Calculate(default, default);
        }
        public void ApplyPosePacket()
        {
            if (!HasSourceJoints) { HasSourceJoints = true; HeadAngles = default; }
            Events.Add(11);
        }
        public void BuildTransform() => Events.Add(9);
        public void UpdateShadow() => Events.Add(10);
        public void SendDeathPulse() => Events.AddRange([2, 100, 7, 7]);
        public void MarkDead() { }
    }
}
