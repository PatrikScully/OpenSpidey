using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.LizMan;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

internal static class LizManTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(LizManTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.lizman.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0, counts = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            string mode = test.GetProperty("mode").GetString()!;
            int[] input = Words(test.GetProperty("input"));
            int[] expected = Words(test.GetProperty("expected"));
            if (mode == "C") Construct(assert, index, input, expected);
            else if (mode == "P") Physics(assert, index, input, expected);
            else Clear(assert, index, input, expected);
            index++; counts++;
        }
        assert(counts == 256, "LizMan fixture keeps all recovered constructor, original physics and ClearAttackFlags cases.");
        assert(LizManState.ModelNameFor(0x503) == "lizman2" && LizManState.ModelNameFor(0x201) == "lizman",
            "Levels 0x503/504/601/602 use lizman2; every other level uses lizman.");
        assert(LizManState.ModuleName == "lizman" && LizManState.Type == 317,
            "Source module name and type317 stay tied to the relocatable LizMan factory.");
    }

    private static void Construct(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        LizManState state = new(input[2], input[0], input[3], 0);
        Host host = new(state) { AttackClock = input[3] };
        LizManBrain.Construct(state, host, input[4], () => { });
        int[] actual =
        [
            LizManState.Type, state.SurfaceClearance, state.LevelId, state.AttackRelatedSnapshot, state.Field338, state.Field3AC,
            state.ShadowScale, state.MinorRadius, state.Field216, state.PushValue, state.SourceNode, state.Field1F4,
            state.State, state.Substate, state.Field398, state.Field212, (int)state.BehaviorFlags, (int)state.CombatFlags,
            unchecked((int)LizManState.SetupWord0), unchecked((int)LizManState.SetupWord1), state.Field374,
            state.ModelName == "lizman2" ? 1 : 0
        ];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CLizMan constructor case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static void Physics(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        LizManPhysics.TrajectoryVector = new(111, 222, 0);
        ActorBodyMotion body = new()
        {
            Position = Vector(input, 3), Velocity = Vector(input, 6), Acceleration = Vector(input, 9),
            Angles = Angles(input, 12), AngularVelocity = Angles(input, 15), AngularAcceleration = Angles(input, 18),
            LinearDamping = new((byte)input[21], (byte)input[22], (byte)input[23]),
            AngularDamping = new((byte)input[24], (byte)input[25], (byte)input[26])
        };
        LizManPhysics.Advance(body, input[0], input[1], input[2], out FixedVector3 trajectory);
        int[] actual =
        [
            ..Snapshot(body), trajectory.X, trajectory.Y, trajectory.Z, body.CollisionFlags, 0
        ];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CLizMan::DoLizmanPhysics case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static void Clear(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        LizManState state = new(1, 0x503, 0, 0) { Field39C = unchecked((byte)input[1]), Field39D = unchecked((byte)input[2]) };
        object identity = new();
        LizManAttackOwnership ownership = new()
        {
            GlobalLizMan = input[0] != 0 ? identity : null,
            AttackFlag = unchecked((byte)input[3])
        };
        LizManAttackFlags.Clear(state, identity, ownership);
        int[] actual =
        [
            ownership.GlobalLizMan is null ? 0 : 1, ownership.AttackFlag, state.Field39C, state.Field39D
        ];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CLizMan::ClearAttackFlags case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static int[] Words(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
    private static FixedVector3 Vector(int[] words, int offset) => new(words[offset], words[offset + 1], words[offset + 2]);
    private static ActorAngles Angles(int[] words, int offset) =>
        new(unchecked((short)words[offset]), unchecked((short)words[offset + 1]), unchecked((short)words[offset + 2]));
    private static int[] Snapshot(ActorBodyMotion body) =>
    [
        body.Position.X, body.Position.Y, body.Position.Z, body.Velocity.X, body.Velocity.Y, body.Velocity.Z,
        body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z, body.Angles.X, body.Angles.Y, body.Angles.Z,
        body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z,
        body.AngularAcceleration.X, body.AngularAcceleration.Y, body.AngularAcceleration.Z
    ];

    private sealed class Host(LizManState state) : ILizManHost
    {
        public ActorBodyMotion Body { get; } = new();
        public ActorProcedureState Actor { get; } = new(new ActorBodyMotion());
        public ActorProcedureList Procedures { get; } = new();
        public int AnimationId { get; set; }
        public int AnimationFrame { get; set; }
        public bool AnimationFinished { get; set; }
        public ushort ItemFlags { get; set; }
        public int ElapsedTicks { get; set; } = 1;
        public int AttackClock { get; set; }
        public int TotalTime { get; set; } = 1000;
        public FixedVector3 PlayerPosition { get; set; }
        public bool HasMessages { get; set; }
        public bool HasLinkedClimbNode => (state.Field2F0 & 0x20) != 0;
        public bool TryAdvanceWaypoint() => false;
        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => -1;
        public void SetHeight(bool initial, int above, int below) { }
        public void RunAnimation(int animation) => AnimationId = animation;
        public void Neutralize() { }
        public void BuildTransform() { }
        public void ProcessMessages() { }
        public void UpdateFootstepBits() { }
        public void PlaySounds() { }
        public void LookAtPlayer(int holdTicks, int rate) { }
        public int ScanNearbyNodesForJumpTarget() => 0;
        public FixedVector3 TriggerPosition(int node) => default;
        public void PlayPositionalSound(int id) { }
        public int Random(int bound) => 0;
    }
}
