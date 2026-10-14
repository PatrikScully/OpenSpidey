using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

internal static class ScorpionDamageTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ScorpionDamageTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.scorpion-damage.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var host = new Host(test.GetProperty("input"));
            JsonElement expected = test.GetProperty("expected");
            ActorHit hit = host.InitialHit;
            host.ReadHit = () => hit;
            int result = 0;
            switch (host.Read("mode"))
            {
                case 0:
                    result = ScorpionDamageBehavior.Hit(host.State, host, ref hit, host.Random) ? 1 : 0;
                    break;
                case 1:
                    ScorpionDamageBehavior.SlideFromHit(host.State, host, host.Read("slideDistance"), host.Read("slideFrames"),
                        host.Vector("slideX", "slideY", "slideZ"));
                    break;
                case 2:
                    ScorpionDamageBehavior.TakeHit(host.State, host, host.Read("elapsedTicks"));
                    break;
                case 3:
                    result = ScorpionDamageBehavior.PathCheck(host.State, host, host.State.Actor.Body.Position,
                        host.Vector("slideX", "slideY", "slideZ"), 55, out _);
                    break;
                default: throw new InvalidDataException("Unexpected original damage fixture method.");
            }
            long[] original = Numbers(expected.GetProperty("snapshot"));
            long[] actual = host.Snapshot();
            int mismatch = Enumerable.Range(0, Math.Min(original.Length, actual.Length)).FirstOrDefault(
                field => original[field] != actual[field], -1);
            assert(result == expected.GetProperty("result").GetInt32() && hit.Damage == expected.GetProperty("hitDamage").GetUInt16() &&
                actual.SequenceEqual(original),
                $"Whole PC Scorpion damage case {index}: method{host.Read("mode")}, state/procedure mismatch{mismatch}; shared health/state/reaction/target/cache/slide/animation/motion/RNG and mutable incoming damage match.");
            assert(host.Events.Count == expected.GetProperty("events").GetArrayLength() &&
                host.Events.Zip(expected.GetProperty("events").EnumerateArray()).All(pair => pair.First.SequenceEqual(Numbers(pair.Second))),
                $"Whole PC Scorpion damage case {index++} preserves callback order and every visible state/AI-procedure/by-reference hit field, including rejected speech/message/LookAt transport and host mutation.");
        }
        VerifyCanonicalOwnership(assert);
    }

    private static void VerifyCanonicalOwnership(Action<bool, string> assert)
    {
        var body = new ScorpionState(0) { BehaviorFlags = 0xFFFFFFFF };
        var actor = new ActorProcedureState(new());
        var state = new ScorpionDamageState(body, actor, new());
        actor.Neutralize(state.Procedures);
        assert(body.BehaviorFlags == 0xB7FFFFFB, "Scorpion behavior and canonical procedure Neutralize share source0x2A8 without a frame-boundary copy.");
        body.BehaviorFlags = 7;
        actor.Body.ScriptWaitTicks = 19;
        actor.Body.LimitedMotionTicks = 23;
        assert(body.WaitUpdates == 19 && state.SlideUpdates == 23,
            "Recovery timer0x230 and slide countdown0x1F8 read the canonical motion fields.");
        body.WaitUpdates = 31;
        state.SlideUpdates = 37;
        body.BindProcedures(actor);
        assert(actor.BehaviorFlags == 7 && actor.Body.ScriptWaitTicks == 31 && actor.Body.LimitedMotionTicks == 37,
            "Repeated binding preserves the shared behavior/timer/slide owner values.");
        bool rejected = false;
        try { body.BindProcedures(new(new())); }
        catch (InvalidOperationException) { rejected = true; }
        assert(rejected && body.BehaviorFlags == 7, "A body cannot silently rebind source behavior flags to a different actor owner.");
        var jonah = new JonahState(0) { SignalFlags = 3 };
        var jonahActor = new ActorProcedureState(new()) { CompletionFlags = 0x12345678 };
        jonah.BindProcedures(jonahActor);
        jonah.ShouldPlead();
        assert(jonah.SignalFlags == 15 && jonahActor.CompletionFlags == 0x12345678,
            "Jonah's source0x218 plea signals stay distinct from procedure0x288 completion flags.");
    }

    private static long[] Numbers(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64()).ToArray();

    private sealed class TargetIdentity(int value)
    {
        public int Value { get; } = value;
    }

    private sealed class ExistingProcedure(int type) : IActorProcedure
    {
        public int SourceType { get; } = type;
        public int Flags { get; set; }
        public Action? ExecuteNext { get; set; }
        public void Execute(int elapsedTicks) => throw new InvalidOperationException("The damage leaf marks existing procedures; it never advances the actor scheduler.");
    }

    private sealed class Host : IScorpionDamageHost
    {
        private readonly JsonElement input;
        private readonly GameRandom random = new(0);
        private readonly TargetIdentity playerHandle = new(1), otherHandle = new(2), jonah = new(1), cachedJonah = new(2);
        private readonly ActorBodyMotion player = new();
        public ScorpionDamageState State { get; }
        public ActorHit InitialHit { get; }
        public Func<ActorHit> ReadHit { get; set; } = null!;
        public List<long[]> Events { get; } = [];
        public int Difficulty => Read("difficulty");
        public uint PlayerFlags { get; private set; }
        public FixedVector3 PlayerPosition => player.Position;
        public int Read(string name) => unchecked((int)input.GetProperty(name).GetInt64());
        private uint Unsigned(string name) => input.GetProperty(name).GetUInt32();
        public FixedVector3 Vector(string x, string y, string z) => new(Read(x), Read(y), Read(z));

        public Host(JsonElement input)
        {
            this.input = input;
            var body = new ScorpionState(0)
            {
                Health = unchecked((short)Read("health")), State = Read("state"), Substate = Read("substate"),
                BehaviorFlags = Unsigned("behaviorFlags"), WaitUpdates = Read("waitUpdates"),
                SurfaceClearance = unchecked((short)Read("surfaceClearance")),
                Animation = OpenSpidey.Core.Animation.AnimationPlaybackState.Initial with
                { ClipId = unchecked((ushort)Read("clip")), Frame = unchecked((short)Read("frame")) }
            };
            var actor = new ActorProcedureState(new()
            {
                Position = Vector("px", "py", "pz"), Velocity = Vector("vx", "vy", "vz"),
                Acceleration = Vector("ax", "ay", "az"), LocalVelocity = Vector("lx", "ly", "lz"),
                AngularVelocity = new(unchecked((short)Read("avx")), unchecked((short)Read("avy")), unchecked((short)Read("avz"))),
                AngularAcceleration = new(unchecked((short)Read("aax")), unchecked((short)Read("aay")), unchecked((short)Read("aaz")))
            }) { CompletionFlags = Unsigned("completionFlags") };
            var procedures = new ActorProcedureList();
            foreach (int type in new[] { 0x20001, 0x40200, 0x101 }) procedures.Attach(new ExistingProcedure(type));
            State = new(body, actor, procedures)
            {
                ReactionContext = Read("reactionContext"), ReactionFlags = Unsigned("reactionFlags"),
                Field310 = Read("field310"), SlideUpdates = Read("slideUpdates"), HitFlashUpdates = Read("hitFlashUpdates"),
                TargetKind = Read("targetKind"), PlayerTargetUpdates = Read("targetUpdates"),
                CurrentTargetHandle = Read("targetHandle") != 0 ? otherHandle : null,
                CachedJonah = Read("cachedJonah") != 0 ? cachedJonah : null,
                LastFireQuip = Read("lastFireQuip"), FireQuipSuppressionUpdates = Read("quipSuppression")
            };
            InitialHit = new(unchecked((byte)Read("hitFlags")), unchecked((byte)Read("hitPart")), Read("hitAttack"),
                unchecked((ushort)Read("hitDamage")), Vector("hitX", "hitY", "hitZ"),
                unchecked((short)Read("hitDistance")), unchecked((ushort)Read("hitFrames")));
            PlayerFlags = Unsigned("playerFlags");
            player.Position = Vector("playerX", "playerY", "playerZ");
            random.Restore(new(Read("seed"), Read("multiplier"), Read("increment")));
        }

        public int Random(int bound)
        {
            int result = random.Next(bound);
            Event(0, bound, result);
            return result;
        }
        public object? MakePlayerHandle()
        {
            Event(1);
            Mutate(1);
            return Read("handleAccepted") != 0 ? playerHandle : null;
        }
        public object? FindJonah() => Read("jonahAvailable") != 0 ? jonah : null;
        public void TrySendJonahMessage(object recipient, int code)
        {
            int accepted = Read("messageAccepted");
            Event(6, 68, accepted);
            if (accepted != 0) Event(7, code, ((TargetIdentity)recipient).Value);
        }
        public bool PlayPositionSpeech(int bank, int slot, FixedVector3 position, int priority)
        {
            Event(2, bank, slot, position.X, position.Y, position.Z, priority);
            Mutate(5);
            return Read("speechAccepted") != 0;
        }
        public void AttachPlayerSpeech(int bank, int slot) => Event(3, bank, slot);
        public void PlayPositionSound(uint sound, FixedVector3 position, int pitch)
        {
            Event(4, sound, position.X, position.Y, position.Z, pitch);
            Mutate(2);
        }
        public void RunAnimation(ushort clip, int from, int to)
        {
            Event(5, clip, from, to);
            State.Body.Animation = State.Body.Animation with { ClipId = clip, Frame = unchecked((short)from) };
            Mutate(3);
        }
        public int PathCheckWithHit(FixedVector3 from, FixedVector3 to, int clearance, out FixedVector3 hit)
        {
            Event(8, from.X, from.Y, from.Z, to.X, to.Y, to.Z, clearance);
            hit = Vector("contactX", "contactY", "contactZ");
            Mutate(4);
            return Read("pathResult");
        }
        public void TryAttachPlayerLookAt(uint completionMask, int rate, int tolerance)
        {
            int accepted = Read("lookAtAccepted");
            Event(9, 64, accepted);
            if (accepted != 0)
                State.Procedures.Attach(ActorLookAtProcedure.ForBody(State.Actor,
                    () => Read("handleAccepted") != 0 ? player : null, completionMask, rate, tolerance));
        }
        public void ReportUnknownRecoverySubstate(int substate) => Event(10, substate);
        private void Mutate(int kind)
        {
            if (Read("mutation") != kind) return;
            switch (kind)
            {
                case 1: State.Body.Health = 451; break;
                case 2: State.Body.Health = -1; State.Body.State = 15; break;
                case 3:
                    State.Body.State = 15; State.Body.Substate = 8;
                    State.Actor.Body.Position += new FixedVector3(4096, 0, 0);
                    State.Body.BehaviorFlags |= 0x48000004;
                    break;
                case 4: State.Actor.Body.Position += new FixedVector3(8192, 0, -4096); break;
                case 5:
                    State.Body.Health = 449;
                    player.Position += new FixedVector3(4096, 0, -8192);
                    PlayerFlags |= 0x80;
                    break;
            }
        }
        private void Event(int kind, params long[] arguments) => Events.Add(new long[] { kind }.Concat(arguments).Concat(Snapshot()).ToArray());
        public long[] Snapshot()
        {
            var body = State.Body;
            var motion = State.Actor.Body;
            var hit = ReadHit();
            List<long> values = [body.Health, body.State, body.Substate, body.BehaviorFlags, State.Actor.CompletionFlags,
                State.ReactionContext, State.ReactionFlags, State.Field310, State.SlideUpdates, State.HitFlashUpdates,
                State.TargetKind, State.PlayerTargetUpdates, (State.CurrentTargetHandle as TargetIdentity)?.Value ?? 0,
                (State.CachedJonah as TargetIdentity)?.Value ?? 0, State.LastFireQuip, State.FireQuipSuppressionUpdates,
                body.Animation.ClipId, body.Animation.Frame, body.WaitUpdates, State.StateFlags.CachedIndex,
                motion.Position.X, motion.Position.Y, motion.Position.Z, motion.Velocity.X, motion.Velocity.Y, motion.Velocity.Z,
                motion.Acceleration.X, motion.Acceleration.Y, motion.Acceleration.Z, motion.LocalVelocity.X, motion.LocalVelocity.Y, motion.LocalVelocity.Z,
                motion.AngularVelocity.X, motion.AngularVelocity.Y, motion.AngularVelocity.Z,
                motion.AngularAcceleration.X, motion.AngularAcceleration.Y, motion.AngularAcceleration.Z,
                random.State.Value, random.State.Multiplier, random.State.Increment, PlayerFlags,
                player.Position.X, player.Position.Y, player.Position.Z, hit.Flags, hit.AttackKind, hit.Damage, State.Procedures.Count];
            foreach (IActorProcedure procedure in State.Procedures.SourceOrder)
            {
                var lookAt = procedure as ActorLookAtProcedure;
                values.AddRange([procedure.SourceType, procedure.Flags, lookAt?.CompletionMask ?? 0,
                    lookAt?.WaitTicks ?? 0, lookAt?.Tolerance ?? 0, lookAt?.Rate ?? 0]);
            }
            return values.ToArray();
        }
    }
}
