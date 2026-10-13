using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugAttackTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugAttackTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-attack.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new ThugState(0, 0)
            {
                Substate = (int)input[2], State = (int)input[3], ActionCounter = (int)input[4], PauseUpdates = (int)input[5],
                BodyFlags = (uint)input[7], TurnRate = (int)input[10], PathPointIndex = (int)input[11]
            };
            state.SetThugType((int)input[1]);
            var actor = new ActorProcedureState(new ActorBodyMotion
            {
                Position = Vector(18), Velocity = new(11, 17, 13), Acceleration = new(-7, -23, 5),
                LocalVelocity = new(11, 17, 13), AngularVelocity = new(11, (short)input[27], 13),
                AngularAcceleration = new(11, (short)input[28], 13)
            }) { BehaviorFlags = (uint)input[8], CompletionFlags = (uint)input[9] };
            var procedures = new ActorProcedureList();
            var existing = new[] { new Procedure(257), new Procedure(1794), new Procedure(0x20501) };
            foreach (Procedure procedure in existing) { procedure.Flags = (int)input[36]; procedures.Attach(procedure); }
            var host = new Host(state, actor, procedures)
            {
                PlayerHealth = (short)input[17], PlayerPosition = Vector(21), AnimationId = (int)input[13],
                AnimationFrame = (int)input[14], AnimationFinished = (byte)input[15], RunFinished = (byte)input[16],
                Distances = input.Skip(29).Take(4).Select(value => (int)value).ToArray(),
                PathResult = (int)input[34], AddResult = input[35] != 0
            };
            host.Points[state.PathPointIndex] = Vector(24);
            ThugAnimationBehavior.SetMode(state, (int)input[12], false, host);
            host.Events.Clear();
            bool result = false;
            if (input[0] == 0)
                ThugAttackBehavior.Step(state, actor, procedures, host, bound =>
                {
                    host.Events.AddRange([8, bound]);
                    return (int)input[33] % bound;
                }, (int)input[6]);
            else result = ThugAttackBehavior.TryRetreat(state, actor, host);
            ActorBodyMotion body = actor.Body;
            long[] actual = [result ? 1 : 0, state.Substate, state.State, state.ActionCounter, state.PauseUpdates,
                state.BodyFlags, actor.BehaviorFlags, actor.CompletionFlags, state.TurnRate, state.PathPointIndex,
                state.AnimationWord294, state.AnimationWord298, host.AnimationId, host.AnimationFrame, host.AnimationFinished,
                body.Position.X, body.Position.Y, body.Position.Z, body.AngularVelocity.X, body.AngularVelocity.Y,
                body.AngularVelocity.Z, body.AngularAcceleration.X, body.AngularAcceleration.Y, body.AngularAcceleration.Z,
                body.Velocity.X, body.Velocity.Y, body.Velocity.Z, body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z,
                body.LocalVelocity.X, body.LocalVelocity.Y, body.LocalVelocity.Z];
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(33).Select(value => value.GetInt64())),
                "Melee substates, signed timer clamp, animation selection, angular cancellation and retreat smoothing match compiled C++.");
            assert(host.Events.SequenceEqual(expected[34].EnumerateArray().Select(value => value.GetInt32())),
                "Attack monitor frame/mask parameters, random/distance/path/add-point queries and sound preserve source call order.");
            JsonElement[] expectedProcedures = expected[33].EnumerateArray().ToArray();
            assert(procedures.Count == expectedProcedures.Length, "Attack attaches LookAt/monitor procedures with deferred cancellation.");
            for (int i = 0; i < existing.Length; i++)
                assert(existing[i].Flags == expectedProcedures[^(i + 1)][1].GetInt32(),
                    "Attack cancellation marks ordinary and matching-category procedures, including persistent monitors.");
            if (host.AttackMonitor is not null)
            {
                assert(expectedProcedures[0][0].GetInt32() == host.AttackMonitor.SourceType &&
                    expectedProcedures[0][1].GetInt32() == host.AttackMonitor.Flags,
                    "Attack monitor replaces its source procedure category.");
            }
            if (input[0] == 0 && input[2] is 0 or 1 && input[17] > 0)
            {
                body.Angles = new(0, FixedAim.Calculate(body.Position, host.PlayerPosition).Yaw, 0);
                procedures.Execute(2);
                assert((actor.CompletionFlags & 2) != 0,
                    "Attack attaches a real recoverable-player LookAt that signals melee alignment.");
            }
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }
    }

    private sealed class Procedure(int sourceType) : IActorProcedure
    {
        public int SourceType { get; } = sourceType;
        public int Flags { get; set; }
        public Action? ExecuteNext { get; set; }
        public void Execute(int elapsedTicks) => ExecuteNext?.Invoke();
    }

    private sealed class Host(ThugState state, ActorProcedureState actor, ActorProcedureList procedures) : IThugAttackHost
    {
        private int distanceIndex;
        public short PlayerHealth { get; init; }
        public FixedVector3 PlayerPosition { get; init; }
        public int AnimationId { get; set; }
        public int AnimationFrame { get; set; }
        public byte AnimationFinished { get; set; }
        public byte RunFinished { get; init; }
        public required int[] Distances { get; init; }
        public int PathResult { get; init; }
        public bool AddResult { get; init; }
        public Dictionary<int, FixedVector3> Points { get; } = [];
        public List<int> Events { get; } = [];
        public Procedure? AttackMonitor { get; private set; }
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
        public bool AddPointToPath(FixedVector3 point, int argument)
        {
            Events.AddRange([5, point.X, point.Y, point.Z, argument]);
            if (AddResult) Points[++state.PathPointIndex] = point;
            return AddResult;
        }
        public FixedVector3 GetPathPoint(int sourceIndex) => Points[sourceIndex];
        public void StartAttackMonitor(int startFrame, int attackMask, int frameCount, uint completionMask)
        {
            Events.AddRange([6, startFrame, attackMask, frameCount, (int)completionMask]);
            AttackMonitor = new(1281);
            procedures.Attach(AttackMonitor);
            actor.CompletionFlags &= ~completionMask;
        }
        public void PlaySound(ushort sound) => Events.AddRange([9, sound]);
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
        public bool MonitorSpitPlease() => throw new NotSupportedException("Melee does not request spit effects.");
    }
}
