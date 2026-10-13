using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;

internal static class BlackCatTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(BlackCatTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.blackcat-ai.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            JsonElement reference = test.GetProperty("expected");
            BlackCatState state = new(100, input[0], 259) { Stage = input[1], Substate = input[2], GroundY = (short)input[8],
                GravityAcceleration = input[9], BehaviorFlags = (uint)input[11] };
            Host host = new(state, input);
            CinematicActorScriptRuntime script = new(new ActorScriptCursor(new byte[] { 0, 0, 255, 0 }), host, CinematicActorScriptProfile.BlackCat);
            host.Script = script;
            if (input[12] != 0) script.Begin();
            new BlackCatBrain(state, host, script).Advance(input[3]);
            int[] scalar = [state.Stage, state.Substate, state.GravityAcceleration, state.GroundY, unchecked((int)state.BehaviorFlags),
                host.ItemFlags, (ushort)state.BodyFlags, state.DeathPulseSent ? 1 : 0, host.AnimationId,
                host.AnimationFinished ? 1 : 0, script.Running ? 1 : 0, script.Decoding ? 1 : 0,
                script.Cursor, host.Head.Pitch, host.Head.Yaw];
            int[] expectedScalar = reference.EnumerateArray().Take(15).Select(value => value.GetInt32()).ToArray();
            int[] motion = Snapshot(host.Body), expectedMotion = Words(reference[15]), expectedEvents = Words(reference[16]);
            assert(scalar.SequenceEqual(expectedScalar) && motion.SequenceEqual(expectedMotion) && host.Events.SequenceEqual(expectedEvents),
                $"Original PC Black Cat AI case {index}: scalar=[{string.Join(',', scalar)}] expected=[{string.Join(',', expectedScalar)}]; " +
                $"motion=[{string.Join(',', motion)}] expected=[{string.Join(',', expectedMotion)}]; " +
                $"events=[{string.Join(',', host.Events)}] expected=[{string.Join(',', expectedEvents)}].");
            assert(state.SourceConstructionOrdinal == 259 && state.ActorPhase == 3 && state.ModelName == (input[0] == 0x803 ? "bc2" : "blackcat"),
                "Black Cat retains the shared full construction ordinal, lowbyte phase and original level-specific model selection.");
            index++;
        }
    }

    private static int[] Words(JsonElement value) => value.EnumerateArray().Select(element => element.GetInt32()).ToArray();
    private static int[] Snapshot(ActorBodyMotion body) => [body.Position.X, body.Position.Y, body.Position.Z,
        body.Velocity.X, body.Velocity.Y, body.Velocity.Z, body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z,
        body.Angles.X, body.Angles.Y, body.Angles.Z, body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z,
        body.AngularAcceleration.X, body.AngularAcceleration.Y, body.AngularAcceleration.Z];

    private sealed class Host(BlackCatState state, int[] input) : ICinematicBlackCatHost
    {
        public ActorBodyMotion Body { get; } = new() { Position = new(input[17], input[18], input[19]),
            Velocity = new(input[20], input[21], input[22]), AngularDamping = new(1, 1, 1) };
        public CinematicActorScriptRuntime Script { get; set; } = null!;
        public int SurfaceClearance => state.SurfaceClearance;
        public int AnimationId { get; private set; } = input[5];
        public int AnimationFrame => input[6];
        public bool AnimationFinished { get; private set; } = input[7] != 0;
        public ushort ItemFlags { get; set; } = (ushort)input[10];
        public FixedVector3 PlayerPosition => new(input[23], input[24], input[25]);
        public bool FastForwardRequested => input[4] != 0;
        public AimAngles Head { get; private set; } = input[15] != 0 ? new((short)input[13], (short)input[14]) : default;
        public List<int> Events { get; } = [];
        public FixedVector3 TriggerPosition(int node) { Events.AddRange([1, node]); return new(node * 400 * 4096, node * -80 * 4096, node * 130 * 4096); }
        public IReadOnlyList<ushort> TriggerLinks(int node) { Events.AddRange([2, node]); return new ushort[] { 7 }; }
        public int GroundHeight(FixedVector3 position, int above, int below, int flags)
        { Events.AddRange([3, position.X, position.Y, position.Z, above, below, flags]); return input[16]; }
        public void RunAnimation(int animation) { Events.AddRange([4, animation, 0, -1]); AnimationId = animation; AnimationFinished = false; }
        public void CycleAnimation(int animation) { Events.AddRange([5, animation, 1]); AnimationId = animation; AnimationFinished = false; }
        public void PlayVoice(short bank, short track, short priority) => Events.AddRange([6, bank, track, priority]);
        public bool ConsumeAnimationSignal(CinematicActorSignalSource source) => false;
        public void PlayPositionSound(uint sound, FixedVector3 position, int pitch) => Events.AddRange([12, (int)sound, position.X, position.Y, position.Z, pitch]);
        public void DeleteAllMessages() { }
        public void SendDeathPulse() => Events.AddRange([2, state.SourceNode, 7, 7]);
        public void MarkDead() { }
        public void UpdatePoseAndShadow(int lookTargetNode, bool scriptRunning)
        {
            if (lookTargetNode != 0) throw new InvalidDataException("The AI leaf fixtures exercise source neutral look; full hook world math belongs to the native imported-model proof.");
            if (input[15] != 0)
            {
                Head = CinematicActorPose.AdvanceHead(Head, default);
                if ((ItemFlags & 4) != 0 && scriptRunning && (Head.Pitch | Head.Yaw) == 0) ItemFlags &= 0xFFFB;
            }
            Events.Add((ItemFlags & 4) != 0 ? 11 : 9); Events.Add(10);
        }
    }
}
