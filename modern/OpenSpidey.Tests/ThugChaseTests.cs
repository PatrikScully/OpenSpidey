using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugChaseTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugChaseTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-chase.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new ThugState(0, 0)
            {
                Substate = (int)input[2], State = (int)input[3], ActionCounter = (int)input[4], BodyFlags = (uint)input[6],
                TurnRate = (int)input[9], PathPointIndex = (int)input[10], WorkCounter = (int)input[16],
                GrenadeElapsedTicks = (int)input[17], GroundReferenceY = (int)input[18], ActionFlags = (uint)input[19],
                AttackOwnershipFlags = (byte)input[20], AttackSlotMask = (byte)input[21], AttackSlotCursor = (byte)input[22],
                ActorPhase = (byte)input[23], AlertTicks = (int)input[24]
            };
            state.SetThugType((int)input[1]);
            var actor = new ActorProcedureState(new ActorBodyMotion
            {
                Position = Vector(28), Velocity = new(11, 17, 13), Acceleration = new(-7, -23, 5),
                LocalVelocity = new(11, 17, 13), AngularVelocity = new(11, 17, 13), AngularAcceleration = new(11, -23, 13),
                MovementFlags = (uint)input[57]
            }) { BehaviorFlags = (uint)input[7], CompletionFlags = (uint)input[8] };
            var procedures = new ActorProcedureList();
            var existing = new[] { new Procedure(257), new Procedure(1794), new Procedure(0x20501) };
            foreach (Procedure procedure in existing) { procedure.Flags = (int)input[58]; procedures.Attach(procedure); }
            var host = new Host(state, actor, procedures)
            {
                PlayerPosition = Vector(31), PlayerUnavailableFlag = (int)input[25], PlayerHoldsObject = input[26] != 0,
                HasFightOwner = input[27] != 0, AnimationId = (int)input[12], AnimationFrame = (int)input[13],
                AnimationFinished = (byte)input[14], RunFinished = (byte)input[15], PathHit = Vector(37), SearchPoint = Vector(40),
                Distances = input.Skip(43).Take(6).Select(value => (int)value).ToArray(), PathResult = (int)input[50],
                AddResults = input.Skip(51).Take(3).Select(value => value != 0).ToArray(),
                AssignedFlags = (byte)input[54], AssignedMask = (byte)input[55], AssignedCursor = (byte)input[56]
            };
            host.Points[state.PathPointIndex] = Vector(34);
            ThugAnimationBehavior.SetMode(state, (int)input[11], false, host);
            host.Events.Clear();
            FixedVector3 target = default;
            if (input[0] == 0) ThugChaseBehavior.Step(state, actor, procedures, host, Random, (int)input[5], (int)input[59]);
            else if (input[0] == 1) target = ThugChaseBehavior.GetAttackPosition(state, actor, host);
            else ThugChaseBehavior.LookForPlayer(state, actor, host, Random);
            ActorBodyMotion body = actor.Body;
            long[] actual = [state.Substate, state.State, state.ActionCounter, state.BodyFlags, actor.BehaviorFlags,
                actor.CompletionFlags, state.TurnRate, state.PathPointIndex, state.AnimationWord294, state.AnimationWord298,
                host.AnimationId, host.AnimationFrame, host.AnimationFinished, state.WorkCounter, state.GrenadeElapsedTicks,
                state.GroundReferenceY, state.ActionFlags, state.AttackOwnershipFlags, state.AttackSlotMask, state.AttackSlotCursor,
                state.ActorPhase, state.AlertTicks, host.HasFightOwner ? 1 : 0, body.Position.X, body.Position.Y, body.Position.Z,
                body.Velocity.X, body.Velocity.Y, body.Velocity.Z, body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z,
                body.LocalVelocity.X, body.LocalVelocity.Y, body.LocalVelocity.Z, body.AngularVelocity.X, body.AngularVelocity.Y,
                body.AngularVelocity.Z, body.AngularAcceleration.X, body.AngularAcceleration.Y, body.AngularAcceleration.Z,
                target.X, target.Y, target.Z];
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(44).Select(value => value.GetInt64())),
                $"Chase case {caseIndex}, input [{string.Join(",", input)}], actual [{string.Join(",", actual)}], expected {expected}.");
            assert(host.Events.SequenceEqual(expected[45].EnumerateArray().Select(value => value.GetInt32())),
                "Chase ownership/path/retry/random/animation queries preserve source order, recursion depth and short circuits.");
            JsonElement[] expectedProcedures = expected[44].EnumerateArray().ToArray();
            assert(procedures.Count == expectedProcedures.Length, "Chase schedules real MoveTo/facing and look procedures with deferred cancellation.");
            for (int i = 0; i < existing.Length; i++)
                assert(existing[i].Flags == expectedProcedures[^(i + 1)][1].GetInt32(),
                    "Chase ordinary/category cancellation preserves existing procedure flags.");
            if (expectedProcedures.Length == 5 && expectedProcedures[0][1].GetInt32() == 0 && expectedProcedures[1][1].GetInt32() == 0)
            {
                FixedVector3 destination = host.GetPathPoint(state.PathPointIndex);
                for (int frame = 0; frame < 5000 && (actor.CompletionFlags & 1) == 0; frame++) procedures.Execute(2);
                assert(body.Position.X == destination.X && body.Position.Z == destination.Z && (actor.CompletionFlags & 1) != 0,
                    "Active chase MoveTo reaches the source cached path point and signals arrival.");
            }
            caseIndex++;
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
            int Random(int bound)
            {
                host.Events.AddRange([8, bound]);
                return (int)input[49] % bound;
            }
        }
    }

    private sealed class Procedure(int sourceType) : IActorProcedure
    {
        public int SourceType { get; } = sourceType;
        public int Flags { get; set; }
        public Action? ExecuteNext { get; set; }
        public void Execute(int elapsedTicks) => ExecuteNext?.Invoke();
    }

    private sealed class Host(ThugState state, ActorProcedureState actor, ActorProcedureList procedures) : IThugChaseHost
    {
        private int distanceIndex;
        private int addIndex;
        public short PlayerHealth => 100;
        public FixedVector3 PlayerPosition { get; init; }
        public int PlayerUnavailableFlag { get; init; }
        public bool PlayerHoldsObject { get; init; }
        public bool HasFightOwner { get; set; }
        public int AnimationId { get; set; }
        public int AnimationFrame { get; set; }
        public byte AnimationFinished { get; set; }
        public byte RunFinished { get; init; }
        public required int[] Distances { get; init; }
        public required bool[] AddResults { get; init; }
        public int PathResult { get; init; }
        public FixedVector3 PathHit { get; init; }
        public FixedVector3 SearchPoint { get; init; }
        public byte AssignedFlags { get; init; }
        public byte AssignedMask { get; init; }
        public byte AssignedCursor { get; init; }
        public Dictionary<int, FixedVector3> Points { get; } = [];
        public List<int> Events { get; } = [];
        public ActorBodyMotion? RecoverPlayerBody() => new() { Position = PlayerPosition };
        public int DistanceToPlayer(int cacheTolerance)
        {
            int value = Distances[Math.Min(distanceIndex++, Distances.Length - 1)];
            Events.AddRange([7, cacheTolerance, value]);
            return value;
        }
        public int PathCheck(FixedVector3 from, FixedVector3 to, int clearance)
        {
            Events.AddRange([4, from.X, from.Y, from.Z, to.X, to.Y, to.Z, clearance]);
            return PathResult;
        }
        public int PathCheckWithHit(FixedVector3 from, FixedVector3 to, int clearance, out FixedVector3 hit)
        {
            hit = PathHit;
            return PathCheck(from, to, clearance);
        }
        public bool AddPointToPath(FixedVector3 point, int argument)
        {
            Events.AddRange([5, point.X, point.Y, point.Z, argument]);
            bool result = AddResults[Math.Min(addIndex++, AddResults.Length - 1)];
            if (result) Points[++state.PathPointIndex] = point;
            return result;
        }
        public FixedVector3 GetPathPoint(int sourceIndex) => Points.GetValueOrDefault(sourceIndex);
        public FixedVector3 GetWaypointNearTarget(FixedVector3 target, int radius, byte phase)
        {
            Events.AddRange([13, target.X, target.Y, target.Z, radius, phase]);
            return SearchPoint;
        }
        public int SetAttackFlags()
        {
            Events.Add(10);
            state.AttackOwnershipFlags = AssignedFlags;
            state.AttackSlotMask = AssignedMask;
            state.AttackSlotCursor = AssignedCursor;
            return 0;
        }
        public void ClearAttackFlags()
        {
            Events.Add(12);
            state.AttackOwnershipFlags = 0;
            state.AttackSlotMask = 0;
        }
        public void SetFightOwner() => HasFightOwner = true;
        public void ReportChaseDepth(int depth) => Events.AddRange([11, depth]);
        public void Neutralize() { Events.Add(1); actor.Neutralize(procedures); }
        public void RunAnimation(ushort clip, int from = 0, int to = -1)
        {
            Events.AddRange([2, clip, from, to]);
            AnimationId = clip;
            AnimationFrame = from;
            AnimationFinished = RunFinished;
        }
        public void CycleAnimation(ushort clip, sbyte direction = 1)
        {
            Events.AddRange([3, clip, direction]);
            AnimationId = clip;
            AnimationFinished = 0;
        }
        public void StartAttackMonitor(int startFrame, int attackMask, int frameCount, uint completionMask) =>
            throw new NotSupportedException("Chase does not attach attack monitors.");
        public void PlaySound(ushort sound) => throw new NotSupportedException("Chase does not play a sound directly.");
        public bool MonitorSpitPlease() => throw new NotSupportedException("Chase does not request spit effects.");
    }
}
