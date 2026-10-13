using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugInjuryTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugInjuryTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-injury.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new ThugState(0, 0)
            {
                Substate = (int)input[2], State = (int)input[3], Health = (short)input[4], ActionCounter = (int)input[5],
                PauseUpdates = (int)input[6], BodyFlags = (uint)input[8], TurnRate = (int)input[11], CombatFlags = (uint)input[12],
                ActionFlags = (uint)input[13], ReactionContext = (int)input[14], SlidePitchRate = (int)input[15], SlideYawRate = (int)input[16],
                LandingY = (int)input[17], CollisionDelayTicks = (int)input[18], DeathSoundTicks = (int)input[19], MinorRadius = (int)input[20],
                TintColor = (uint)input[22], Transparency = (uint)input[23]
            };
            state.SetThugType((int)input[1]);
            var actor = new ActorProcedureState(new ActorBodyMotion
            {
                Position = Vector(30), Velocity = Vector(33), Acceleration = Vector(36),
                Angles = new((short)input[39], (short)input[40], (short)input[41]), LocalVelocity = new(11, 17, 13),
                AngularVelocity = new(11, 17, 13), AngularAcceleration = new(11, -23, 13)
            }) { BehaviorFlags = (uint)input[9], CompletionFlags = (uint)input[10] };
            var procedures = new ActorProcedureList();
            var existing = new[] { new Procedure(257), new Procedure(1794), new Procedure(0x20501) };
            foreach (Procedure procedure in existing) { procedure.Flags = (int)input[53]; procedures.Attach(procedure); }
            var host = new Host(state, actor, procedures)
            {
                ItemFlags = (ushort)input[21], AnimationId = (int)input[24], AnimationFrame = (int)input[25],
                AnimationFinished = (byte)input[26], RunFinished = (byte)input[27], HasJoints = input[28] != 0,
                JointOnePitch = (short)input[29], PlayerPosition = Vector(42), GroundResult = (int)input[45],
                HeightResult = (int)input[46], PhysicsCounter = (int)input[47], CollisionType = (int)input[48],
                RandomValues = input.Skip(50).Take(3).Select(value => (int)value).ToArray()
            };
            ThugAnimationBehavior.SetMode(state, (int)input[54], false, host);
            host.Events.Clear();
            int result = 0;
            switch (input[0])
            {
                case 0: ThugInjuryBehavior.Fall(state, actor, procedures, host, host.Random, (int)input[7]); break;
                case 1: ThugInjuryBehavior.FlyAcrossRoom(state, actor, host, (int)input[7]); break;
                case 2: ThugInjuryBehavior.DieThug(state, actor, host, host.Random, (int)input[7], input[49] != 0); break;
                case 3: ThugInjuryBehavior.DieAfterFlyingAcrossRoom(state, actor, host); break;
                case 4: result = host.Die((int)input[49]) ? 1 : 0; break;
                case 5: result = ThugInjuryBehavior.ShouldFall(state, actor, host, 200, 389120); break;
            }
            ActorBodyMotion body = actor.Body;
            long[] actual = [result, state.Substate, state.State, state.Health, state.ActionCounter, state.PauseUpdates,
                state.BodyFlags, actor.BehaviorFlags, actor.CompletionFlags, state.TurnRate, state.CombatFlags, state.ActionFlags,
                state.LandingY, state.CollisionDelayTicks, state.DeathSoundTicks, state.MinorRadius, host.ItemFlags,
                state.TintColor, state.Transparency, host.JointOnePitch, host.AnimationId, host.AnimationFrame,
                host.AnimationFinished, host.ShadowPresent ? 1 : 0, host.MarkedDead ? 1 : 0,
                state.PreviousPosition.X, state.PreviousPosition.Y, state.PreviousPosition.Z,
                body.Position.X, body.Position.Y, body.Position.Z, body.Velocity.X, body.Velocity.Y, body.Velocity.Z,
                body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z, body.Angles.X, body.Angles.Y, body.Angles.Z,
                body.LocalVelocity.X, body.LocalVelocity.Y, body.LocalVelocity.Z, body.AngularVelocity.X, body.AngularVelocity.Y,
                body.AngularVelocity.Z, body.AngularAcceleration.X, body.AngularAcceleration.Y, body.AngularAcceleration.Z];
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(49).Select(value => value.GetInt64())),
                $"Injury case {caseIndex}, input [{string.Join(",", input)}], actual [{string.Join(",", actual)}], expected {expected}.");
            assert(host.Events.SequenceEqual(expected[50].EnumerateArray().Select(value => value.GetInt32())),
                "Fall/flight/death preserve physics/collision/pose/sound/pulse ordering, drop thresholds and real staged death fades.");
            JsonElement[] expectedProcedures = expected[49].EnumerateArray().ToArray();
            assert(procedures.Count == expectedProcedures.Length, "Fall schedules the real source landing procedure.");
            for (int i = 0; i < existing.Length; i++)
                assert(existing[i].Flags == expectedProcedures[^(i + 1)][1].GetInt32(),
                    "Injury neutralization and fall attachment preserve deferred procedure cancellation.");
            if (expectedProcedures.Length == 4 && (actor.BehaviorFlags & 0x400) != 0)
            {
                body.Position = body.Position with { Y = state.LandingY };
                procedures.Execute(2);
                assert((actor.CompletionFlags & 4) != 0, "Fall landing procedure reads the source landing Y and signals contact.");
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
    private sealed class ImpactBody(int type) : IThugInjuryBody { public int Type { get; } = type; }

    private sealed class Host(ThugState state, ActorProcedureState actor, ActorProcedureList procedures) : IThugInjuryHost
    {
        private int randomIndex;
        public FixedVector3 PlayerPosition { get; init; }
        public ushort ItemFlags { get; set; }
        public bool HasJoints { get; init; }
        public short JointOnePitch { get; set; }
        public int GroundResult { get; init; }
        public int HeightResult { get; init; }
        public int PhysicsCounter { get; init; }
        public int CollisionType { get; init; }
        public bool ShadowPresent { get; private set; } = true;
        public bool MarkedDead { get; private set; }
        public int AnimationId { get; set; }
        public int AnimationFrame { get; set; }
        public byte AnimationFinished { get; set; }
        public byte RunFinished { get; init; }
        public required int[] RandomValues { get; init; }
        public List<int> Events { get; } = [];
        public int Random(int bound)
        {
            Events.AddRange([8, bound]);
            return RandomValues[Math.Min(randomIndex++, RandomValues.Length - 1)] % bound;
        }
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
        public void DoPhysics(int mode)
        {
            Events.AddRange([4, mode]);
            state.ActionCounter = PhysicsCounter;
            actor.Body.Position += new FixedVector3(11, -23, 17);
            actor.Body.Velocity += new FixedVector3(1, -2, 3);
        }
        public IThugInjuryBody? StruckGameObject(int first, int second)
        {
            Events.AddRange([5, first, second]);
            return CollisionType < 0 ? null : new ImpactBody(CollisionType);
        }
        public void ElasticCollision(IThugInjuryBody target, FixedVector3 velocity, int duration) =>
            Events.AddRange([6, target.Type, velocity.X, velocity.Y, velocity.Z, duration]);
        public void CheckFallBack() => Events.Add(7);
        public int GroundHeight(FixedVector3 position, int above, int below)
        {
            Events.AddRange([10, position.X, position.Y, position.Z, above, below, 0]);
            return GroundResult;
        }
        public void KillShadow() { Events.Add(11); ShadowPresent = false; }
        public int SetHeight(bool initial, int clearance, int range)
        {
            Events.AddRange([12, initial ? 1 : 0, clearance, range]);
            return HeightResult;
        }
        public void SendDeathPulse() => Events.Add(13);
        public bool Die(int stage)
        {
            Events.AddRange([14, stage]);
            return ThugDeathLifecycle.Step(state, actor, this, stage);
        }
        public void MarkDead() { Events.Add(15); MarkedDead = true; }
        public void ClearAttackFlags() { Events.Add(16); state.AttackOwnershipFlags = 0; state.AttackSlotMask = 0; }
        public void ApplyPoseForType(int type) => Events.AddRange([17, type == 304 ? 304 : 312]);
        public void NotifyKill(int type) => Events.AddRange([18, type]);
        public void PlayPositionalSound(ushort sound, FixedVector3 position, int attenuation) =>
            Events.AddRange([9, sound, position.X, position.Y, position.Z, attenuation]);
        public void PlayGlobalSound(ushort sound, int volume, int pan) => Events.AddRange([19, sound, volume, pan]);
        public bool MonitorSpitPlease() => throw new NotSupportedException("Injury behavior does not request spit effects.");
    }
}
