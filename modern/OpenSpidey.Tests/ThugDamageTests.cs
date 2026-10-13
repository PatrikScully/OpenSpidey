using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

internal static class ThugDamageTests
{
    private sealed class Procedure(int type) : IActorProcedure
    {
        public int SourceType { get; } = type;
        public int Flags { get; set; }
        public Action? ExecuteNext { get; set; }
        public void Execute(int elapsedTicks) => ExecuteNext?.Invoke();
    }

    private sealed class Host(ThugState state, ActorProcedureState actor, ActorProcedureList procedures) : IThugDamageHost
    {
        private int addCalls;
        public int AnimationId { get; set; }
        public int AnimationFrame { get; set; }
        public byte AnimationFinished { get; set; }
        public byte RunFinished { get; init; }
        public int PlayerAnimationId { get; init; }
        public short PlayerHealth => 100;
        public FixedVector3 PlayerPosition => new(500000, 0, -700000);
        public int PathResult { get; init; }
        public int AddPattern { get; init; }
        public int HitFactor { get; init; }
        public bool DeathSent { get; set; }
        public ThugState? Buddy { get; init; }
        public List<int> Events { get; } = [];
        public ActorBodyMotion? RecoverPlayerBody() => new() { Position = PlayerPosition };
        public ThugState? FindClosest(int actorType)
        {
            Events.AddRange([11, actorType]);
            return Buddy?.Type == actorType ? Buddy : null;
        }
        public void SendMessage(ThugState recipient, int code) => Events.AddRange([12, ReferenceEquals(recipient, state) ? 0 : 1, code]);
        public void SendDeathPulse()
        {
            if (DeathSent || state.SourceNode == ushort.MaxValue) return;
            DeathSent = true;
            Events.Add(13);
        }
        public void ClearAttackOwnership() => Events.Add(10);
        public int PathCheckWithHit(FixedVector3 from, FixedVector3 to, int clearance, out FixedVector3 hit)
        {
            Events.AddRange([4, from.X, from.Y, from.Z, to.X, to.Y, to.Z, clearance]);
            hit = from + ((to - from) >> 2) * HitFactor;
            return PathResult;
        }
        public int PathCheck(FixedVector3 from, FixedVector3 to, int clearance) => PathCheckWithHit(from, to, clearance, out _);
        public bool AddPointToPath(FixedVector3 point, int argument)
        {
            Events.AddRange([5, point.X, point.Y, point.Z, argument]);
            return (AddPattern & (1 << addCalls++)) != 0;
        }
        public FixedVector3 GetPathPoint(int sourceIndex) => throw new NotSupportedException("Damage uses insertion callbacks only.");
        public void StartAttackMonitor(int startFrame, int attackMask, int frameCount, uint completionMask) => throw new NotSupportedException("Damage does not start an attack monitor.");
        public int DistanceToPlayer(int cacheTolerance) => throw new NotSupportedException("Damage does not query player distance.");
        public bool MonitorSpitPlease() => throw new NotSupportedException("Damage does not emit spit.");
        public void Neutralize() { Events.Add(1); actor.Neutralize(procedures); }
        public void PlaySound(ushort sound) => Events.AddRange([9, sound]);
        public void RunAnimation(ushort clip, int from = 0, int to = -1)
        {
            Events.AddRange([2, clip, from, to]);
            AnimationId = clip; AnimationFrame = from; AnimationFinished = RunFinished;
        }
        public void CycleAnimation(ushort clip, sbyte direction = 1)
        {
            Events.AddRange([3, clip, direction]); AnimationId = clip; AnimationFinished = 0;
        }
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugDamageTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-damage.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new ThugState((int)input[27], 0x101);
            state.SetThugType((int)input[1]);
            state.State = (int)input[2]; state.Substate = (int)input[3]; state.Health = (short)input[4];
            state.CombatFlags = (uint)input[5]; state.BodyFlags = (uint)input[7]; state.ActionFlags = (uint)input[8];
            state.ReactionContext = (int)input[9]; state.TurnRate = (int)input[10]; state.ActionCounter = (int)input[11];
            state.MinorRadius = (int)input[12]; state.SlidePitchRate = (int)input[13]; state.SlideYawRate = (int)input[14];
            var body = new ActorBodyMotion
            {
                Position = Vector(34), Velocity = Vector(40), Acceleration = Vector(43), LocalVelocity = Vector(46),
                Angles = Angles(49), AngularVelocity = Angles(52), AngularAcceleration = Angles(55)
            };
            var actor = new ActorProcedureState(body) { BehaviorFlags = (uint)input[6], CompletionFlags = uint.MaxValue };
            var procedures = new ActorProcedureList();
            Procedure[] existing = [new(257), new(1794), new(0x20000)];
            for (int i = 0; i < existing.Length; i++) { procedures.Attach(existing[i]); existing[i].Flags = i * 4; }
            ThugState? buddy = null;
            if (input[20] != 0)
            {
                buddy = new(17, 0x101) { AlertTicks = (int)input[21] };
                buddy.SetThugType((int)input[20]);
            }
            var host = new Host(state, actor, procedures)
            {
                AnimationId = (int)input[16], AnimationFrame = (int)input[17], AnimationFinished = (byte)input[18],
                RunFinished = (byte)input[19], Buddy = buddy, PlayerAnimationId = (int)input[22], PathResult = (int)input[23],
                AddPattern = (int)input[24], DeathSent = input[26] != 0, HitFactor = (int)input[33]
            };
            ThugAnimationBehavior.SetMode(state, (int)input[15], false, host);
            host.Events.Clear();
            var flags = new ThugStateFlags();
            var hit = new ActorHit((byte)input[28], 0, (int)input[29], (ushort)input[30], Vector(37), (short)input[31], (ushort)input[32]);
            int result = 0;
            int Random(int range) { host.Events.AddRange([8, range]); return (int)input[25] % range; }
            switch (input[0])
            {
                case 0: result = ThugDamageBehavior.Hit(state, actor, procedures, flags, host, hit, Random) ? 1 : 0; break;
                case 1: ThugDamageBehavior.TakeHit(state, actor, procedures, host); break;
                case 2: ThugDamageBehavior.SlideFromHit(state, actor, procedures, host, (int)input[31], (int)input[32], hit.Direction, Random); break;
                case 3: ThugDamageBehavior.RunHitAnimation(state, host); break;
                case 4: ThugDamageBehavior.SetHitDirection(state, actor, hit.Direction); break;
            }
            JsonElement before = test.GetProperty("expected")[0];
            CheckState(before, "before procedure execution");
            for (int i = 0; i < existing.Length; i++)
                assert(existing[i].Flags == before[42][before[42].GetArrayLength() - i - 1][1].GetInt32(), "Damage marks ordinary and matching-family procedures with source protected-procedure rules.");
            procedures.Execute(2);
            CheckState(test.GetProperty("expected")[1], "after procedure execution");

            void CheckState(JsonElement expected, string stage)
            {
                long[] actual = [result, state.State, state.Substate, state.Health, state.CombatFlags, actor.BehaviorFlags,
                    state.BodyFlags, state.ActionFlags, state.ReactionContext, state.TurnRate, state.ActionCounter, state.MinorRadius,
                    state.SlidePitchRate, state.SlideYawRate, state.AnimationWord294, state.AnimationWord298,
                    host.AnimationId, host.AnimationFrame, host.AnimationFinished, host.DeathSent ? 1 : 0, flags.CachedIndex,
                    body.Velocity.X, body.Velocity.Y, body.Velocity.Z, body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z,
                    body.LocalVelocity.X, body.LocalVelocity.Y, body.LocalVelocity.Z, body.Angles.X, body.Angles.Y, body.Angles.Z,
                    body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z,
                    body.AngularAcceleration.X, body.AngularAcceleration.Y, body.AngularAcceleration.Z,
                    state.AlertTicks, body.MovementFlags, actor.CompletionFlags];
                assert(actual.SequenceEqual(expected.EnumerateArray().Take(42).Select(value => value.GetInt64())),
                    $"Damage operation {input[0]} matches recovered health, reaction, slide and all body fields {stage}.");
                assert(host.Events.SequenceEqual(expected[43].EnumerateArray().Select(value => value.GetInt32())),
                    "Damage random, sound, message, path, animation, ownership and pulse callbacks preserve exact source order.");
                assert(procedures.Count == expected[42].GetArrayLength() && body.Position == Vector(34),
                    "Damage procedure lifetime and stationary source position match before and after live LookAt execution.");
            }
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
            ActorAngles Angles(int offset) => new((short)input[offset], (short)input[offset + 1], (short)input[offset + 2]);
        }
        CheckZeroFrames(assert);
    }

    private static void CheckZeroFrames(Action<bool, string> assert)
    {
        var state = new ThugState(16, 0x101) { Health = 50, ActionCounter = 9, State = 12 };
        state.SetThugType(312);
        var actor = new ActorProcedureState(new ActorBodyMotion { Velocity = new(17, 19, 23) }) { BehaviorFlags = 0x10000010 };
        var procedures = new ActorProcedureList();
        var host = new Host(state, actor, procedures);
        ThugDamageBehavior.SlideFromHit(state, actor, procedures, host, 40, 0, new(4096, 0, 0), _ => 0);
        assert(state.State == 12 && state.ActionCounter == 9 && actor.BehaviorFlags == 0x10000010 &&
            actor.Body.Velocity == new FixedVector3(17, 19, 23) && host.Events.Count == 0,
            "A zero-frame slide safely leaves the hit reaction intact instead of dividing by zero as the original did.");
        bool reacted = ThugDamageBehavior.Hit(state, actor, procedures, new(), host,
            new ActorHit(8, 0, 0, 0, new(4096, 0, 0), 40, 0), _ => 0);
        assert(reacted && state.State == 12 && state.Health == 50 && host.AnimationId == 14,
            "A zero-frame hit still performs its source damage reaction and animation while omitting invalid slide integration.");
    }
}
