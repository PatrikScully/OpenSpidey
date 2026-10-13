using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

internal static class ThugReactionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugReactionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-reaction.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new ThugState(0, 0)
            {
                State = (int)input[2], Substate = (int)input[3], Health = (short)input[4], ActionCounter = (int)input[5],
                WorkDelayCounter = (int)input[6], WorkCounter = (int)input[7], YankOscillationTicks = (int)input[8],
                ConversationTicks = (int)input[9], DialogueClip = (int)input[10], DialogueBank = (int)input[11],
                BodyFlags = (uint)input[13], CombatFlags = (uint)input[16], ActionFlags = (uint)input[17],
                TurnRate = (int)input[18], LandingY = (int)input[19], PathPointIndex = (int)input[20]
            };
            state.SetThugType((int)input[1]);
            var actor = new ActorProcedureState(new ActorBodyMotion
            {
                Position = Vector(25), Velocity = Vector(28), Acceleration = new(11, 17, 13), LocalVelocity = new(11, 17, 13),
                Angles = new((short)input[31], (short)input[32], (short)input[33]), AngularVelocity = new(11, 17, 13),
                AngularAcceleration = new(11, -23, 13)
            }) { BehaviorFlags = (uint)input[14], CompletionFlags = (uint)input[15] };
            var procedures = new ActorProcedureList();
            var existing = new[] { new Procedure(257), new Procedure(1794), new Procedure(0x20501) };
            foreach (Procedure procedure in existing) { procedure.Flags = (int)input[63]; procedures.Attach(procedure); }
            var host = new Host(state, actor, procedures)
            {
                AnimationId = (int)input[21], AnimationFrame = (int)input[22], AnimationFinished = (byte)input[23], RunFinished = (byte)input[24],
                PlayerPosition = Vector(34), PlayerYaw = (short)input[37], PlayerAnimation = (int)input[38], PlayerActionState = (int)input[39],
                AttackClock = (int)input[43], GroundResult = (int)input[44], PhysicsCounter = (int)input[45], CollisionType = (int)input[46],
                PathResult = (int)input[47], AddResults = input.Skip(48).Take(3).Select(value => value != 0).ToArray(), GrabResult = input[51] != 0,
                Buddy304 = Buddy(304, input[52], input[54]), Buddy312 = Buddy(312, input[53], input[55]),
                WebPresent = input[56] != 0, StrandCount = (int)input[57], PlayerWebComboValue = (int)input[58],
                PartnerPresent = input[59] != 0, RandomValues = input.Skip(60).Take(3).Select(value => (int)value).ToArray()
            };
            host.Points[state.PathPointIndex] = Vector(40);
            ThugAnimationBehavior.SetMode(state, (int)input[64], false, host);
            host.Events.Clear();
            switch (input[0])
            {
                case 0: ThugReactionBehavior.BackpedalPlease(state, actor, host); break;
                case 1: ThugReactionBehavior.GettingGrabbed(state, actor, host, host.Random); break;
                case 2: ThugReactionBehavior.GetYankedBySpidey(state, actor, procedures, host, (int)input[12]); break;
                case 3: ThugReactionBehavior.WebThrow(state, actor, host); break;
                case 4: ThugReactionBehavior.GetTrapped(state, actor, procedures, host, host.Random); break;
                case 5: ThugReactionBehavior.Acknowledge(state, actor, procedures, host); break;
            }
            ActorBodyMotion body = actor.Body;
            long[] actual = [state.State, state.Substate, state.Health, state.ActionCounter, state.WorkDelayCounter, state.WorkCounter,
                state.YankOscillationTicks, state.ConversationTicks, state.BodyFlags, actor.BehaviorFlags, actor.CompletionFlags,
                state.CombatFlags, state.ActionFlags, state.TurnRate, state.LandingY, state.PathPointIndex,
                host.AnimationId, host.AnimationFrame, host.AnimationFinished, host.ShadowPresent ? 1 : 0,
                host.WebBurst ? 1 : 0, host.ComboDuration, host.ComboAmount,
                state.PreviousPosition.X, state.PreviousPosition.Y, state.PreviousPosition.Z,
                body.Position.X, body.Position.Y, body.Position.Z, body.Velocity.X, body.Velocity.Y, body.Velocity.Z,
                body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z, body.Angles.X, body.Angles.Y, body.Angles.Z,
                body.LocalVelocity.X, body.LocalVelocity.Y, body.LocalVelocity.Z,
                body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z,
                body.AngularAcceleration.X, body.AngularAcceleration.Y, body.AngularAcceleration.Z];
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(47).Select(value => value.GetInt64())),
                $"Reaction case {caseIndex}, input [{string.Join(",", input)}], actual [{string.Join(",", actual)}], expected {expected}.");
            assert(host.Events.SequenceEqual(expected[48].EnumerateArray().Select(value => value.GetInt32())),
                "Grab/yank/web reactions preserve buddy warnings, physics/collision/hit order, path retries, pose offsets, web score and dialogue callbacks.");
            JsonElement[] expectedProcedures = expected[47].EnumerateArray().ToArray();
            assert(procedures.Count == expectedProcedures.Length, "Reactions attach source persistent exit messages and recoverable acknowledgement LookAt.");
            for (int i = 0; i < existing.Length; i++)
                assert(existing[i].Flags == expectedProcedures[^(i + 1)][1].GetInt32(),
                    "Reaction neutralization and LookAt attachment preserve deferred category/ordinary cancellation.");
            caseIndex++;
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }
    }

    private static ThugState? Buddy(int type, long present, long alert)
    {
        if (present == 0) return null;
        var state = new ThugState(0, 0) { AlertTicks = (int)alert };
        state.SetThugType(type);
        return state;
    }
    private sealed class Procedure(int sourceType) : IActorProcedure
    {
        public int SourceType { get; } = sourceType;
        public int Flags { get; set; }
        public Action? ExecuteNext { get; set; }
        public void Execute(int elapsedTicks) => ExecuteNext?.Invoke();
    }
    private sealed class ImpactBody(int type) : IThugInjuryBody { public int Type { get; } = type; }

    private sealed class Host(ThugState state, ActorProcedureState actor, ActorProcedureList procedures) : IThugReactionHost, IThugTrapWeb
    {
        private int randomIndex, addIndex;
        public FixedVector3 PlayerPosition { get; init; }
        public short PlayerYaw { get; init; }
        public int PlayerAnimation { get; init; }
        public int PlayerActionState { get; init; }
        public int AttackClock { get; init; }
        public bool GrabResult { get; init; }
        public ThugState? Buddy304 { get; init; }
        public ThugState? Buddy312 { get; init; }
        public int GroundResult { get; init; }
        public int PhysicsCounter { get; init; }
        public int CollisionType { get; init; }
        public int PathResult { get; init; }
        public bool WebPresent { get; init; }
        public int StrandCount { get; init; }
        public bool PartnerPresent { get; init; }
        public int PlayerWebComboValue { get; init; }
        public int ComboDuration { get; private set; }
        public int ComboAmount { get; private set; }
        public bool WebBurst { get; private set; }
        public bool ShadowPresent { get; private set; } = true;
        public int AnimationId { get; set; }
        public int AnimationFrame { get; set; }
        public byte AnimationFinished { get; set; }
        public byte RunFinished { get; init; }
        public ushort ItemFlags { get; set; }
        public bool HasJoints => false;
        public short JointOnePitch { get; set; }
        public required int[] RandomValues { get; init; }
        public required bool[] AddResults { get; init; }
        public Dictionary<int, FixedVector3> Points { get; } = [];
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
        public int GroundHeight(FixedVector3 position, int above, int below)
        {
            Events.AddRange([10, position.X, position.Y, position.Z, above, below, 0]);
            return GroundResult;
        }
        public void KillShadow() { Events.Add(11); ShadowPresent = false; }
        public void ClearAttackFlags() { Events.Add(16); state.AttackOwnershipFlags = 0; state.AttackSlotMask = 0; }
        public void SetAttacker() => Events.Add(21);
        public bool GrabUpdate(ref FixedVector3 position, ref short yaw)
        {
            Events.Add(28);
            position += new FixedVector3(11, -23, 17);
            yaw = unchecked((short)(yaw + 7));
            return GrabResult;
        }
        public int PathCheck(FixedVector3 from, FixedVector3 to, int clearance)
        {
            Events.AddRange([29, from.X, from.Y, from.Z, to.X, to.Y, to.Z, clearance]);
            return PathResult;
        }
        public bool AddPointToPath(FixedVector3 point, int argument)
        {
            Events.AddRange([30, point.X, point.Y, point.Z, argument]);
            bool result = AddResults[Math.Min(addIndex++, AddResults.Length - 1)];
            if (result) Points[++state.PathPointIndex] = point;
            return result;
        }
        public FixedVector3 GetPathPoint(int sourceIndex) => Points.GetValueOrDefault(sourceIndex);
        public FixedVector3 GetThrowPoint(int sourceIndex) => new(409600 * sourceIndex, 100000 * sourceIndex, -819200 * sourceIndex);
        public ThugState? FindClosest(int type)
        {
            Events.AddRange([20, type, 0]);
            return type == 304 ? Buddy304 : Buddy312;
        }
        public void SendMessage(ThugState target, int code) => Events.AddRange([26, target.Type, code]);
        public void SendSelfMessage(int code) => throw new NotSupportedException("Reaction exit monitors execute on later actor updates.");
        public void ApplyHitToBody(IThugInjuryBody body, ActorHit hit) =>
            Events.AddRange([25, body.Type, hit.Flags, hit.AttackKind, actor.Body.Position.X, actor.Body.Position.Y, actor.Body.Position.Z]);
        public IThugTrapWeb? RecoverTrapWeb() { Events.Add(22); return WebPresent ? this : null; }
        public void Burst() { Events.Add(24); WebBurst = true; }
        public void SetPlayerWebCombo(int duration, int amount) { ComboDuration = duration; ComboAmount = amount; }
        public ActorBodyMotion? RecoverConversationPartnerBody() { Events.Add(23); return PartnerPresent ? new() : null; }
        public void PlayDialogue(int bank, int clip, FixedVector3 position, int attenuation) =>
            Events.AddRange([27, bank, clip, position.X, position.Y, position.Z, attenuation]);
        public void PlayPositionalSound(ushort sound, FixedVector3 position, int attenuation) =>
            Events.AddRange([9, sound, position.X, position.Y, position.Z, attenuation]);
        public int SetHeight(bool initial, int clearance, int range) => throw new NotSupportedException("These reactions do not set height directly.");
        public void ApplyPoseForType(int type) => throw new NotSupportedException("These reactions do not apply joint pose packets.");
        public void CheckFallBack() => throw new NotSupportedException("These reactions do not test fallback.");
        public void NotifyKill(int type) => throw new NotSupportedException("These reactions do not notify kills directly.");
        public bool Die(int stage) => throw new NotSupportedException("These reactions transition to the death state.");
        public void MarkDead() => throw new NotSupportedException("These reactions transition to the death state.");
        public void SendDeathPulse() => Events.Add(13);
        public void PlayGlobalSound(ushort sound, int volume, int pan) => throw new NotSupportedException("These reactions use positional sounds.");
        public bool MonitorSpitPlease() => throw new NotSupportedException("These reactions do not request spit effects.");
    }
}
