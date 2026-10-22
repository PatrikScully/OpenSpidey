using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Combat;
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
        public void ProcessProcedures() => Procedures.Execute(ElapsedTicks);
        public void DoPhysics(int mode) { }
        public void RunTimer(ref int timer) { timer = unchecked(timer - ElapsedTicks); if (timer < 0) timer = 0; }
        public bool TryAdvanceWaypoint() => CanAdvanceWaypoint;
        public void SpawnEmergeEffect() { }
        public void PlayPositionalSound(int id) { }
        public int Random(int bound) => bound == 0 ? 0 : 0;
        public void DestroySlimeQuad() { }
        public void UpdateSlimeQuad(FixedVector3 position, bool grounded, byte red, byte green, byte blue) { }
        public bool LineOfSight(FixedVector3 from, FixedVector3 to, out FixedVector3 hit) { hit = default; return true; }
        public FixedVector3 LastLineHitNormal => default;
        public bool LineToItem(FixedVector3 start, FixedVector3 end, out int hitY) { hitY = 0; return false; }
        public bool CheckObjectCollision(FixedVector3 from, FixedVector3 to) => false;
        public ActorPathCheckResult PathCheck(FixedVector3 from, FixedVector3 to, bool wantHitPoint, int groundRange, FixedVector3 initialHit) => default;
        public bool BumpedIntoSpidey(int radius) => false;
        public uint PlayerDistance => 0;
        public ushort PlayerMinorRadius => 0;
        public int AnimationFrame => 0;
        public byte HeightQueryInterval { set { } }
        public FixedJointTransform TransformBasis => default;
        public int CeilingSetupLow => 555819296;
        public int CeilingSetupHigh => 543;
        public int SupportVelocityY => 0;
        public ThugMessage? FirstMessage => null;
        public int UpdateGroundSupport(FixedVector3 position, int above, int below) => GroundHeightWithSupport(position, above, below, out _);
        public void AttachMoveTo(FixedVector3 target, int speed, int turnRate, int tolerance, uint mask) =>
            Procedures.AttachMove(new ActorMoveToProcedure(Actor, target, speed, turnRate, tolerance, mask));
        public void AttachLookAtPlayer(uint mask, int rate, int tolerance) => throw Unexpected();
        public void AttachLookAtPoint(FixedVector3 point, uint mask, int rate, int tolerance) => throw Unexpected();
        public void AttachLookAtYaw(int yaw, uint mask, int rate, int tolerance) => throw Unexpected();
        public FixedVector3 DynamicHookPosition(ActorHook hook) => throw Unexpected();
        public void ApplyPose() => throw Unexpected();
        public void AttachAttackMonitor(int startFrame, uint hookMask, int frames, uint completionMask) => throw Unexpected();
        public void AttachStateSwitchMessage(int message) => throw Unexpected();
        public bool TrapWebBurning() => throw Unexpected();
        public void BurstTrapWeb() => throw Unexpected();
        public bool PlayerCanTalk() => throw Unexpected();
        public bool PlayAndAttachPlayerXA(int bank, int slot, int volume) => throw Unexpected();
        public void SpawnShot(FixedVector3 position) => throw Unexpected();
        public int PlayerAnimationId => 0;
        public int PlayerActionState { get; set; }
        public short PlayerYaw => 0;
        public FixedVector3 PlayerVelocity { set => throw Unexpected(); }
        public bool GrabUpdate(ref FixedVector3 position, ref short yaw) => throw Unexpected();
        public void SetPlayerAi(SimbyPlayerAi ai) => throw Unexpected();
        public void RestorePlayerAi() => throw Unexpected();
        public void HitPlayer(ActorHit hit) => throw Unexpected();
        public void PlayPlayerSingleAnimation(int animation) => throw Unexpected();
        public void CreatePlayerImpactEffect(FixedVector3 position, int flags) => throw Unexpected();
        public void SendSelfMessage(int code) => throw Unexpected();

        public void ReleaseSlimeQuad() { }
        public void NotifyPlayerKill() => throw Unexpected();
        public void Die() => throw Unexpected();
        public void SplattyDeath() => throw Unexpected();
        public void SpawnFireDeath() => throw Unexpected();
        public int SpawnSymBase(FixedVector3 position, ActorAngles angles, bool following) => throw Unexpected();
        public void StartSymBaseFollowing(int handle, int scale) => throw Unexpected();
        public void SetSymBaseTarget(int handle, int scale) => throw Unexpected();
        public void ReleaseSymBase(int handle) => throw Unexpected();
        public void StopSymBaseFollowing(int handle) => throw Unexpected();
        public void DeleteSymBase(int handle) => throw Unexpected();
        public void SetSymBaseItemScale(int handle, short scale) => throw Unexpected();
        public void SetSymBaseAngleX(int handle, short angle) => throw Unexpected();
        public int CreateRibbon(int faces, byte red, byte green, byte blue, int width) => throw Unexpected();
        public void SetRibbonPoint(int handle, int index, FixedVector3 position) => throw Unexpected();
        public void SetRibbonPointY(int handle, int index, int y) => throw Unexpected();
        public void SetRibbonWidth(int handle, int index, int width) => throw Unexpected();
        public void DeleteRibbon(int handle) => throw Unexpected();
        public int ClipFrameCount(int clip) => throw Unexpected();
        public int WebGroundY(FixedVector3 position) => throw Unexpected();
        public void SpawnDrop(FixedVector3 position, FixedVector3 velocity, int groundY, int flags) => throw Unexpected();
        public void CreateSlimeQuad(FixedVector3 position, int flags, bool scaleUp) { }
        public void CleanUpMessages() { }
        private static InvalidOperationException Unexpected() => new("Simby fixture reached an unrecorded host service.");

    }
}
