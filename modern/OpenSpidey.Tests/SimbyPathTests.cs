using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

/// <summary>Replays tools/reference_simby_path_vectors.py captures of the original CSimby planners.</summary>
internal static class SimbyPathTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(SimbyPathTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.simby-path.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0, returned = 0;
        int[] perFunction = new int[6];
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int fn = test.GetProperty("fn").GetInt32();
            int[] header = Words(test.GetProperty("header"));
            int[] queues = Words(test.GetProperty("queues"));
            long[] expected = Longs(test.GetProperty("state"));
            long[][] events = test.GetProperty("events").EnumerateArray().Select(Longs).ToArray();
            (long[] state, List<long[]> actualEvents) = Replay(fn, header, queues);
            bool eventsMatch = actualEvents.Count == events.Length && actualEvents.Zip(events).All(pair => pair.First.SequenceEqual(pair.Second));
            assert(state.SequenceEqual(expected) && eventsMatch,
                $"Actual PC CSimby planner case {index} fn{fn}: state=[{string.Join(',', state)}] expected=[{string.Join(',', expected)}] " +
                $"events={(eventsMatch ? "match" : string.Join(' ', actualEvents.Select(e => $"[{string.Join(',', e)}]")) + " expected " + string.Join(' ', events.Select(e => $"[{string.Join(',', e)}]")))}.");
            perFunction[fn]++;
            if (expected[0] != 0) returned++;
            index++;
        }
        assert(perFunction.All(count => count >= 100) && returned > 100,
            "Simby planner fixture covers GetToTarget, SetUpJumpPath, RegisterTargetPos, the attack-slot pick, AttemptAttackMethod and MoveInForAttack.");
    }

    private static (long[] State, List<long[]> Events) Replay(int fn, int[] h, int[] q)
    {
        var state = new SimbyState(1, 0x801, 0)
        {
            Stage = h[1], Substate = h[2], BehaviorFlags = unchecked((uint)h[3]), CombatFlags = unchecked((uint)h[4]),
            MinorRadius = unchecked((ushort)h[5]), SurfaceClearance = unchecked((short)h[6]), SetupHigh = h[7], SetupLow = h[8],
            TargetPosition = new(h[13], h[14], h[15]), UnitDirection = new(h[16], h[17], h[18]),
            AttackUnit = new(h[19], h[20], h[21]), ClimbTarget = new(h[22], h[23], h[24]), AimYaw = h[25], AttackAngle = h[26],
            TargetSlot = h[27], AttackMask = h[28], DistanceClock = h[31], CachedPlayerDistance = h[32],
            JumpHeight = h[68], JumpSteps = h[69], JumpStep = h[70]
        };
        var world = new SimbyWorldState { AttackData = h[39], TargetSlotMask = unchecked((uint)h[40]) };
        for (int i = 0; i < 8; i++) world.TargetSlots[i] = new(h[41 + i * 3], h[42 + i * 3], h[43 + i * 3]);
        var host = new Host(q)
        {
            AnimationFinished = h[30] != 0, HasSupportBody = h[29] != 0, PlayerPosition = new(h[33], h[34], h[35]),
            PlayerHealth = unchecked((short)h[36]), PlayerSpecialState = unchecked((byte)h[37]), AttackClock = h[38],
            LastLineHitNormal = new(h[65], h[66], h[67])
        };
        host.Body.Position = new(h[9], h[10], h[11]);
        host.Body.Angles = new(0, unchecked((short)h[12]), 0);
        state.BindCombatFlags(host.Actor);
        var brain = new SimbyBrain(state, world, new SimbyStateFlags(), host);
        long result = 0;
        switch (fn)
        {
            case 0: result = brain.Planner.GetToTarget(h[0]); break;
            case 1: result = brain.Planner.SetUpJumpPath(); break;
            case 2: result = brain.Planner.RegisterTargetPos(state.TargetPosition) ? 1 : 0; break;
            case 3: result = brain.Planner.PickAttackSlot() ? 1 : 0; break;
            case 4: result = brain.Planner.AttemptAttackMethod(h[0]); break;
            default: brain.MoveInForAttack(); break;
        }
        var s = new List<long> { result, state.Stage, state.Substate, state.BehaviorFlags, state.CombatFlags, state.SetupLow, state.SetupHigh };
        foreach (FixedVector3 v in new[] { state.TargetPosition, state.UnitDirection, state.AttackUnit, state.ClimbTarget }) Add(s, v);
        s.AddRange([state.AimYaw, state.AttackAngle, state.JumpHeight, state.TargetSlot, state.JumpSteps, state.JumpStep,
            state.AttackMask, state.CachedPlayerDistance, state.DistanceClock, world.AttackData, world.TargetSlotMask]);
        foreach (FixedVector3 slot in world.TargetSlots) Add(s, slot);
        Add(s, world.OverflowTargetSlot);
        return (s.ToArray(), host.Events);
    }

    private static void Add(List<long> list, FixedVector3 v) => list.AddRange([v.X, v.Y, v.Z]);
    private static int[] Words(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
    private static long[] Longs(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt64()).ToArray();

    private sealed class Host : ISimbyHost
    {
        private readonly int[] paths, grounds, lines, objects, items;
        private int pathIndex, groundIndex, lineIndex, objectIndex, itemIndex;
        public List<long[]> Events { get; } = [];
        public Host(int[] q)
        {
            Actor = new(Body);
            int at = 0;
            int[] Take(int stride) { int n = q[at++] * stride; int[] r = q[at..(at + n)]; at += n; return r; }
            paths = Take(4); grounds = Take(2); lines = Take(4); objects = Take(1); items = Take(2);
        }
        public ActorBodyMotion Body { get; } = new();
        public ActorProcedureState Actor { get; }
        public ActorProcedureList Procedures { get; } = new();
        public ushort ItemFlags { get; set; }
        public int AnimationId { get; set; }
        public bool AnimationFinished { get; set; }
        public int ElapsedTicks => 1;
        public int AttackClock { get; set; }
        public FixedVector3 PlayerPosition { get; set; }
        public short PlayerHealth { get; set; }
        public byte PlayerSpecialState { get; set; }
        public byte PlayerInputLock => 0;
        public bool HasProcedures => false;
        public bool HasSupportBody { get; set; }
        public int IdleSetupLow => 513;
        public int IdleSetupHigh => 0;
        public int CurrentWaypoint => 0;
        public ushort WaypointFlags { get; set; }
        public FixedVector3 LastLineHitNormal { get; set; }

        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => Ground(position, above, below, false, out _);
        public int GroundHeightWithSupport(FixedVector3 position, int above, int below, out bool hasSupport) =>
            Ground(position, above, below, true, out hasSupport);
        private int Ground(FixedVector3 p, int above, int below, bool wantSupport, out bool support)
        {
            int k = groundIndex++ % (grounds.Length / 2) * 2;
            Events.Add([2, p.X, p.Y, p.Z, above, below, wantSupport ? 1 : 0]);
            support = grounds[k + 1] != 0;
            return grounds[k];
        }
        public ActorPathCheckResult PathCheck(FixedVector3 from, FixedVector3 to, bool wantHitPoint, int groundRange, FixedVector3 initialHit)
        {
            int k = pathIndex++ % (paths.Length / 4) * 4;
            Events.Add([1, from.X, from.Y, from.Z, to.X, to.Y, to.Z, wantHitPoint ? 1 : 0, groundRange, Actor.BehaviorFlags,
                initialHit.X, initialHit.Y, initialHit.Z]);
            return new(paths[k], wantHitPoint ? new FixedVector3(paths[k + 1], paths[k + 2], paths[k + 3]) : default);
        }
        public bool LineOfSight(FixedVector3 from, FixedVector3 to, out FixedVector3 hit)
        {
            int k = lineIndex++ % (lines.Length / 4) * 4;
            Events.Add([3, from.X, from.Y, from.Z, to.X, to.Y, to.Z, 0, 1]);
            hit = lines[k] == 0 ? new(lines[k + 1], lines[k + 2], lines[k + 3]) : default;
            return lines[k] != 0;
        }
        public bool CheckObjectCollision(FixedVector3 from, FixedVector3 to)
        {
            int k = objectIndex++ % objects.Length;
            Events.Add([4, from.X, from.Y, from.Z, to.X, to.Y, to.Z, 1, 1]);
            return objects[k] != 0;
        }
        public bool LineToItem(FixedVector3 start, FixedVector3 end, out int hitY)
        {
            int k = itemIndex++ % (items.Length / 2) * 2;
            Events.Add([5, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 1]);
            hitY = items[k + 1];
            return items[k] != 0;
        }
        public void Neutralize() => Events.Add([6]);
        public void RunAnimation(int animation) { AnimationId = animation; Events.Add([7, animation, 0, -1]); }
        public void CycleAnimation(int animation) => throw Unexpected();
        public int SetHeight(bool initial, int above, int below) => throw Unexpected();
        public FixedVector3 WaypointPosition(int node) => throw Unexpected();
        public void BuildTransform() => throw Unexpected();
        public void ProcessProcedures() => throw Unexpected();
        public void DoPhysics(int mode) => throw Unexpected();
        public void RunTimer(ref int timer) => throw Unexpected();
        public bool TryAdvanceWaypoint() => throw Unexpected();
        public void SpawnEmergeEffect() => throw Unexpected();
        public void PlayPositionalSound(int id) => throw Unexpected();
        public int Random(int bound) => throw Unexpected();
        public void DestroySlimeQuad() => throw Unexpected();
        public void UpdateSlimeQuad(FixedVector3 position, bool grounded, byte red, byte green, byte blue) => throw Unexpected();
        public bool BumpedIntoSpidey(int radius) => throw Unexpected();
        public uint PlayerDistance => 0;
        public ushort PlayerMinorRadius => 0;
        public int AnimationFrame => 0;
        public byte HeightQueryInterval { set { } }
        public FixedJointTransform TransformBasis => default;
        public int CeilingSetupLow => 555819296;
        public int CeilingSetupHigh => 543;
        public int SupportVelocityY => 0;
        public ThugMessage? FirstMessage => null;
        public int UpdateGroundSupport(FixedVector3 position, int above, int below) => throw Unexpected();
        public void AttachMoveTo(FixedVector3 target, int speed, int turnRate, int tolerance, uint mask) => throw Unexpected();
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

        public void ReleaseSlimeQuad() => throw Unexpected();
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
        public void CreateSlimeQuad(FixedVector3 position, int flags, bool scaleUp) => throw Unexpected();
        public void CleanUpMessages() => throw Unexpected();

        private static InvalidOperationException Unexpected() => new("Simby planner fixture reached an unrecorded host service.");
    }
}
