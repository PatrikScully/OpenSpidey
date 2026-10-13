using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugAwarenessTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugAwarenessTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-awareness.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new ThugState(0, 0)
            {
                State = (int)input[2], Substate = (int)input[3], Health = (short)input[4], RangedCooldownTicks = (int)input[11],
                AlertTicks = (int)input[12], Timer334Ticks = (int)input[13], GunSoundCooldownTicks = (int)input[14],
                ConversationTicks = (int)input[15], CollisionDelayTicks = (int)input[16], ClosePlayerTicks = (int)input[17],
                GunSoundDelayTicks = (int)input[18], FightSuppressionTicks = (int)input[19], DistanceSampleClock = (int)input[21],
                CachedPlayerDistance = (int)input[22], FightPollInterval = (int)input[23], ActionCounter = (int)input[24],
                TurnRate = (int)input[25], GroundReferenceY = (int)input[26], BodyFlags = (uint)input[28],
                CombatFlags = (uint)input[29], ActionFlags = (uint)input[30], AttackOwnershipFlags = (byte)input[31],
                AttackSlotMask = (byte)input[32], LastSeenPlayerPosition = new(40960, 0, -81920)
            };
            state.SetThugType((int)input[1]);
            var actor = new ActorProcedureState(new ActorBodyMotion
            {
                Position = Vector(35), Angles = new(0, (short)input[38], 0), MovementFlags = (uint)input[34],
                Velocity = new(11, 17, 13), Acceleration = new(-7, -23, 5), LocalVelocity = new(11, 17, 13),
                AngularVelocity = new(11, 17, 13), AngularAcceleration = new(11, -23, 13)
            }) { BehaviorFlags = (uint)input[27], CompletionFlags = (uint)input[33] };
            var procedures = new ActorProcedureList();
            var existing = new[] { new Procedure(257), new Procedure(1794), new Procedure(0x20501) };
            foreach (Procedure procedure in existing) procedures.Attach(procedure);
            var flags = new ThugStateFlags();
            var host = new Host(state, actor, procedures)
            {
                Player = new(Vector(39), (short)input[5], (uint)input[6], (int)input[7], input[8] != 0),
                HasFightOwner = input[9] != 0, AttackClock = (int)input[10], PlayerSurfaceClearance = (short)input[42],
                LineClear = input[43] != 0, TargetPathResult = (int)input[44], LastPathResult = (int)input[45],
                FirstAddResult = input[46] != 0, SecondAddResult = input[47] != 0, HitMutation = input[48] != 0,
                ConversationMutation = input[49] != 0, RandomValues = input.Skip(50).Take(3).Select(value => (int)value).ToArray()
            };
            ThugAnimationBehavior.SetMode(state, (int)input[53], false, host);
            host.Events.Clear();
            int result = 0;
            switch (input[0])
            {
                case 0: ThugAwarenessBehavior.DoAISwitchLogic(state, actor, flags, host, host.Random, (int)input[20]); break;
                case 1: ThugAwarenessBehavior.Caution(state, actor, host, host.Random); break;
                case 2: ThugAwarenessBehavior.LookConfused(state, actor, procedures, host, host.Random); break;
                case 3: result = ThugAwarenessBehavior.ShouldShootPlayer(state, actor, host) ? 1 : 0; break;
                case 4: result = ThugAwarenessBehavior.BumpedIntoPlayer(state, actor, host, 100) ? 1 : 0; break;
            }
            ActorBodyMotion body = actor.Body;
            long[] actual = [result, state.State, state.Substate, state.Health, state.RangedCooldownTicks, state.AlertTicks,
                state.Timer334Ticks, state.GunSoundCooldownTicks, state.ConversationTicks, state.CollisionDelayTicks,
                state.ClosePlayerTicks, state.GunSoundDelayTicks, state.FightSuppressionTicks, state.DistanceSampleClock,
                state.CachedPlayerDistance, state.FightPollInterval, state.ActionCounter, state.TurnRate,
                actor.BehaviorFlags, state.BodyFlags, state.CombatFlags, state.ActionFlags, actor.CompletionFlags,
                state.PathPointIndex, state.LastSeenPlayerPosition.X, state.LastSeenPlayerPosition.Y,
                state.LastSeenPlayerPosition.Z, host.HasFightOwner ? 1 : 0, flags.CachedIndex,
                body.Velocity.X, body.Velocity.Y, body.Velocity.Z, body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z,
                body.LocalVelocity.X, body.LocalVelocity.Y, body.LocalVelocity.Z, body.AngularVelocity.X, body.AngularVelocity.Y,
                body.AngularVelocity.Z, body.AngularAcceleration.X, body.AngularAcceleration.Y, body.AngularAcceleration.Z];
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(44).Select(value => value.GetInt64())),
                $"Awareness case {caseIndex}, input [{string.Join(",", input)}], actual [{string.Join(",", actual)}], expected {expected}.");
            assert(host.Events.SequenceEqual(expected[45].EnumerateArray().Select(value => value.GetInt32())),
                "AI hit/contact/timer/conversation and caution perception callbacks preserve source order and short circuits.");
            JsonElement[] expectedProcedures = expected[44].EnumerateArray().ToArray();
            assert(procedures.Count == expectedProcedures.Length, "Confusion schedules real yaw LookAt with source continuation.");
            for (int i = 0; i < existing.Length; i++)
                assert(existing[i].Flags == expectedProcedures[^(i + 1)][1].GetInt32(),
                    "Awareness neutralization and yaw attachment preserve ordinary/category cancellation.");
            if (expectedProcedures.Length == 4)
            {
                int targetYaw = expectedProcedures[0][3].GetInt32();
                body.Angles = body.Angles with { Y = unchecked((short)targetYaw) };
                procedures.Execute(2);
                assert((actor.CompletionFlags & 2) != 0, "Confusion yaw procedure signals the source generated heading.");
            }
            caseIndex++;
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

    private sealed class Host(ThugState state, ActorProcedureState actor, ActorProcedureList procedures) : IThugAwarenessHost
    {
        private int adds;
        private int randomIndex;
        public ThugPlayerState Player { get; init; }
        public int AttackClock { get; init; }
        public bool HasFightOwner { get; set; }
        public short PlayerSurfaceClearance { get; init; }
        public bool LineClear { get; init; }
        public int TargetPathResult { get; init; }
        public int LastPathResult { get; init; }
        public bool FirstAddResult { get; init; }
        public bool SecondAddResult { get; init; }
        public bool HitMutation { get; init; }
        public bool ConversationMutation { get; init; }
        public required int[] RandomValues { get; init; }
        public int AnimationId { get; set; }
        public int AnimationFrame { get; set; }
        public byte AnimationFinished { get; set; }
        public List<int> Events { get; } = [];
        public int Random(int bound)
        {
            Events.AddRange([8, bound]);
            return RandomValues[Math.Min(randomIndex++, RandomValues.Length - 1)] % bound;
        }
        public bool LineOfSight(FixedVector3 from, FixedVector3 to, uint flags) { Events.Add(1); return LineClear; }
        public int PathCheck(FixedVector3 from, FixedVector3 to, int clearance)
        {
            Events.AddRange([2, to.X, to.Y, to.Z, clearance]);
            return to == state.LastSeenPlayerPosition ? LastPathResult : TargetPathResult;
        }
        public bool AddPointToPath(FixedVector3 point, int argument)
        {
            bool result = adds++ == 0 ? FirstAddResult : SecondAddResult;
            Events.AddRange([3, point.X, point.Y, point.Z, argument, result ? 1 : 0]);
            if (result) state.PathPointIndex++;
            return result;
        }
        public void SetFightOwner() => HasFightOwner = true;
        public void ClearAttackOwnership() { state.AttackOwnershipFlags = 0; state.AttackSlotMask = 0; }
        public void Neutralize() { Events.Add(4); actor.Neutralize(procedures); }
        public void SendSignal() => Events.Add(5);
        public void ApplyHit(uint hitFlags, int damage)
        {
            Events.AddRange([6, (int)hitFlags, damage]);
            if (HitMutation)
            {
                state.Health = unchecked((short)(state.Health - damage));
                state.State = 12;
                state.Substate = 0;
                state.AlertTicks = 600;
            }
        }
        public void StrikeUpConversation()
        {
            Events.Add(7);
            if (ConversationMutation) { state.State = 20; state.Substate = 0; state.ConversationTicks = 160; }
        }
        public void PlaySound(ushort sound) => Events.AddRange([9, sound]);
        public void RunAnimation(ushort clip, int from = 0, int to = -1)
        {
            Events.AddRange([10, clip, from, to]);
            AnimationId = clip;
            AnimationFrame = from;
            AnimationFinished = 0;
        }
        public void CycleAnimation(ushort clip, sbyte direction = 1)
        {
            Events.AddRange([11, clip, direction]);
            AnimationId = clip;
            AnimationFinished = 0;
        }
        public bool MonitorSpitPlease() => throw new NotSupportedException("Awareness does not request spit effects.");
    }
}
