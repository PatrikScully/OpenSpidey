using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Mathematics;

internal static class SimbyAiTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(SimbyAiTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.simby-ai.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0, counts = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            string mode = test.GetProperty("mode").GetString()!;
            int[] input = Words(test.GetProperty("input"));
            int[] expected = Words(test.GetProperty("expected"));
            if (mode == "I") Init(assert, index, input, expected);
            else if (mode == "M") MoveFreelyLatch(assert, index, input, expected);
            else if (mode == "W") FollowWaypoints(assert, index, input, expected);
            else Flags(assert, index, input, expected);
            index++; counts++;
        }
        assert(counts == 40, "Simby AI fixture keeps Initialize, MoveFreely latch, FollowWaypoints and CheckStateFlags cases.");
        assert(SimbyState.Type == 324 && SimbyState.ModuleName == "simby",
            "Source module name and type324 stay tied to the relocatable Simby factory.");
    }

    private static void FollowWaypoints(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        var state = new SimbyState(1, 0x801, 0)
        {
            Stage = 2, Substate = input[0], Field1F0 = input[1], SetupHigh = 261
        };
        Host host = new()
        {
            Ground = 100 << 12, PlayerHealth = 0, Waypoint = new(409600, 0, 819200),
            WaypointFlags = unchecked((ushort)input[2]), CanAdvanceWaypoint = input[3] != 0
        };
        host.Actor.CompletionFlags = unchecked((uint)input[4]);
        var brain = new SimbyBrain(state, new SimbyWorldState(), new SimbyStateFlags(), host);
        brain.Advance();
        int[] actual =
        [
            state.Stage, state.Substate, state.Field1F0, (int)state.CombatFlags, host.Procedures.Count,
            unchecked((int)host.Actor.CompletionFlags), host.AnimationId
        ];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CSimby FollowWaypoints case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static void Init(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        var state = new SimbyState(1, 0x801, 0);
        if (input[1] != 0) state.BehaviorFlags |= 0x100000;
        Host host = new() { Ground = input[0] };
        var brain = new SimbyBrain(state, new SimbyWorldState(), new SimbyStateFlags(), host);
        brain.Advance();
        int[] actual =
        [
            state.Stage, state.Substate, host.Body.Position.Y, state.ShadowPosition.Y, state.ShadowGroundY, (int)state.Field3A0
        ];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CSimby Initialize case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static void MoveFreelyLatch(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        var state = new SimbyState(1, 0x801, 0)
        {
            Stage = 4, Substate = 0, SetupHigh = input[0], AlertModeTimer = input[1]
        };
        // PlayerHealth 0 keeps DetermineFightState (flag 0x40 on state4) quiet for this latch fixture.
        Host host = new() { Ground = 100 << 12, PlayerHealth = 0 };
        var brain = new SimbyBrain(state, new SimbyWorldState(), new SimbyStateFlags(), host);
        brain.Advance();
        int[] actual = [state.Stage, state.Substate, state.SetupLow, state.SetupHigh, host.AnimationId];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CSimby MoveFreely idle latch case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static void Flags(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        var flags = new SimbyStateFlags();
        ushort value = flags.Check(input[0]);
        int[] actual = [value, flags.CachedIndex];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CBaddy::CheckStateFlags(simby 0x555564) case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static int[] Words(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();

    private sealed class Host : ISimbyHost
    {
        public ActorBodyMotion Body { get; set; } = new();
        public ActorProcedureState Actor { get; }
        public ActorProcedureList Procedures { get; } = new();
        public ushort ItemFlags { get; set; }
        public int AnimationId { get; set; }
        public bool AnimationFinished { get; set; }
        public int ElapsedTicks { get; set; } = 1;
        public int AttackClock { get; set; } = 100;
        public FixedVector3 PlayerPosition { get; set; } = new(0, 1 << 20, 0);
        public short PlayerHealth { get; set; } = 100;
        public byte PlayerSpecialState { get; set; }
        public byte PlayerInputLock { get; set; }
        public bool HasMessages { get; set; }
        public bool HasProcedures => Procedures.Count != 0;
        public bool HasSupportBody { get; set; }
        public int IdleSetupLow => 513;
        public int IdleSetupHigh => 0;
        public int CurrentWaypoint { get; set; } = 1;
        public ushort WaypointFlags { get; set; }
        public FixedVector3 Waypoint { get; set; }
        public bool CanAdvanceWaypoint { get; set; }
        public int Ground { get; set; } = -1;
        public Host() => Actor = new(Body);
        public int PerimeterDistance(int mode) => 2000;
        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => Ground;
        public int GroundHeightWithSupport(FixedVector3 position, int above, int below, out bool hasSupport)
        {
            hasSupport = false;
            return Ground;
        }
        public int SetHeight(bool initial, int above, int below) => 1;
        public FixedVector3 WaypointPosition(int node) => Waypoint;
        public void RunAnimation(int animation) => AnimationId = animation;
        public void CycleAnimation(int animation) => AnimationId = animation;
        public void Neutralize() => Actor.Neutralize(Procedures);
        public void BuildTransform() { }
        public void ProcessMessages() { }
        public void ProcessProcedures() => Procedures.Execute(ElapsedTicks);
        public void DoPhysics(int mode) { }
        public void RunTimer(ref int timer) { timer = unchecked(timer - ElapsedTicks); if (timer < 0) timer = 0; }
        public bool TryAdvanceWaypoint() => CanAdvanceWaypoint;
        public int GetToTarget(int mode) => 0;
        public void SpawnEmergeEffect() { }
        public void PlayPositionalSound(int id) { }
        public int Random(int bound) => bound == 0 ? 0 : 0;
        public void EnsureSlimeQuad() { }
        public void DestroySlimeQuad() { }
        public void UpdateSlimeQuad(bool invertedClearance) { }
        public bool LineOfSightToPlayer() => false;
        public bool RegisterTargetFromPlayer() => false;
        public bool BumpedIntoSpidey(int radius) => false;
    }
}
