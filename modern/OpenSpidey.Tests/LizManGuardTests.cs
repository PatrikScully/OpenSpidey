using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.LizMan;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

/// <summary>CLizMan::Guard, CalculateJumpPositionArray and Jump against the PC executable.</summary>
internal static class LizManGuardTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(LizManGuardTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.lizman-guard.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        string[] fields = root.GetProperty("fields").EnumerateArray().Select(field => field.GetString()!).ToArray();
        int[] random = Words(root.GetProperty("random"));
        int index = 0;
        foreach (JsonElement test in root.GetProperty("cases").EnumerateArray())
        {
            var input = fields.Zip(Words(test.GetProperty("input"))).ToDictionary(pair => pair.First, pair => pair.Second);
            int[] expected = Words(test.GetProperty("s"));
            int[] expectedArray = Words(test.GetProperty("arr"));
            string[] expectedLog = test.GetProperty("log").EnumerateArray().Select(entry => entry.GetString()!).ToArray();
            (int[] actual, int[] array, List<string> log, List<string> lookAts) = Execute(input, random);
            string[] expectedLookAts = expectedLog.Where(entry => entry[0] == 'K').Select(entry => string.Join(':', entry.Split(':').Take(4))).ToArray();
            // Mem_Delete ("F") of the 0x3B4 array is the null JumpPositions checked through the last state word.
            string[] expectedOther = expectedLog.Where(entry => entry[0] is not ('K' or 'F')).ToArray();
            assert(actual.SequenceEqual(expected) && array.SequenceEqual(expectedArray) && log.SequenceEqual(expectedOther) &&
                lookAts.SequenceEqual(expectedLookAts),
                $"Actual PC CLizMan guard/jump case {index} mode {input["mode"]}: actual=[{string.Join(',', actual)}] " +
                $"log=[{string.Join(',', log)}] lookAt=[{string.Join(',', lookAts)}] expected=[{string.Join(',', expected)}] " +
                $"log=[{string.Join(',', expectedOther)}] lookAt=[{string.Join(',', expectedLookAts)}].");
            index++;
        }
        assert(index == 92, "LizMan guard fixture keeps 40 Guard, 40 Jump and 12 jump-array cases.");
    }

    private static (int[], int[], List<string>, List<string>) Execute(Dictionary<string, int> input, int[] random)
    {
        var state = new LizManState(3, 0x503, 0, 0)
        {
            State = input["mode"] == 0 ? 1 : 0, Substate = input["substate"], Field1F8 = input["f1F8"], Field374 = input["f374"],
            Field1F0 = input["f1F0"], Field1F4 = input["f1F4"], MinorRadius = (ushort)input["minor"],
            BodyFlags = (ushort)input["bodyFlags"], Field394 = input["f394"]
        };
        var host = new Host(input, random, state);
        int[] array = [];
        LizManAnimationFollowOn.PendingAnimation = unchecked((ushort)input["pending"]);
        if (input["mode"] == 0) LizManGuard.Guard(state, host);
        else
        {
            LizManGuard.CalculateJumpPositionArray(state, host, new(input["tx"], input["ty"], input["tz"]));
            if (input["mode"] == 1)
            {
                host.Log.Clear();
                state.State = 23;
                state.JumpPositionIndex = input["f944"];
                LizManGuard.Jump(state, host);
            }
            else array = state.JumpPositions!.SelectMany(position => new[] { position.X, position.Y, position.Z }).ToArray();
        }
        LizManAnimationFollowOn.PendingAnimation = 0xFFFF;
        List<string> lookAts = host.Procedures.SourceOrder.OfType<ActorLookAtProcedure>()
            .Select(procedure => $"K{procedure.SourceType}:{procedure.CompletionMask}:{procedure.Rate}:{procedure.Tolerance}").ToList();
        int[] actual =
        [
            state.State, state.Substate, state.Field1F8, state.Field1F0, state.Field1F4, host.Body.Position.X, host.Body.Position.Y,
            host.Body.Position.Z, unchecked((short)state.MinorRadius), state.Field394, unchecked((ushort)state.BodyFlags),
            state.JumpPositionIndex, host.AnimationFrame, state.JumpPositions is null ? 0 : 1
        ];
        return (actual, array, host.Log, lookAts);
    }

    private static int[] Words(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();

    private sealed class Host : ILizManHost
    {
        private readonly Dictionary<string, int> input;
        private readonly int[] random;
        private int used;
        public List<string> Log { get; } = [];

        private readonly LizManState state;

        public Host(Dictionary<string, int> input, int[] random, LizManState state)
        {
            this.state = state;
            this.input = input;
            this.random = random;
            Actor = new(new ActorBodyMotion { Position = new(input["px"], input["py"], input["pz"]) });
            AnimationId = input["anim"];
            AnimationFrame = input["frame"];
        }

        public ActorBodyMotion Body => Actor.Body;
        public ActorProcedureState Actor { get; }
        public ActorProcedureList Procedures { get; } = new();
        public int AnimationId { get; private set; }
        public int AnimationFrame { get; private set; }
        public bool AnimationFinished => false;
        public ushort ItemFlags { get; set; }
        public int ElapsedTicks => input["ticks"];
        public int AttackClock => 0;
        public int TotalTime => input["timer"];
        public FixedVector3 PlayerPosition => default;
        public bool HasMessages => false;
        public bool HasLinkedClimbNode => false;
        public bool TryAdvanceWaypoint() => throw new InvalidOperationException();
        public int GroundHeight(FixedVector3 position, int above, int below, int flags)
        {
            Log.Add($"g{above}:{below}");
            return input["ground"];
        }
        public void SetHeight(bool initial, int above, int below) => Log.Add($"H{(initial ? 1 : 0)}:{above}:{below}");
        public void RunAnimation(int animation)
        {
            Log.Add($"A{animation}");
            AnimationId = animation;
        }
        public void Neutralize() => Log.Add("N");
        public void BuildTransform() { }
        public void ProcessMessages() { }
        public void UpdateFootstepBits() { }
        public void PlaySounds() { }
        public ActorBodyMotion? RecoverPlayerBody() => new();
        public void SetAnimationFrame(int frame) => AnimationFrame = frame;
        public void KillShadow() => state.BodyFlags &= ~8u;
        public int ScanNearbyNodesForJumpTarget()
        {
            Log.Add("Q");
            return input["scan"];
        }
        public FixedVector3 TriggerPosition(int node)
        {
            Log.Add($"T{node}");
            return new(input["tx"], input["ty"], input["tz"]);
        }
        public void PlayPositionalSound(int id) => Log.Add($"S{id}");
        public int Random(int bound)
        {
            int value = random[(input["seed"] + used++) % random.Length];
            return bound > 0 ? value % bound : 0;
        }
    }
}
