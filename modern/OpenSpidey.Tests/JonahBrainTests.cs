using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class JonahBrainTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(JonahBrainTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.jonah-brain.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        HashSet<int> callbackKinds = [];
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var jonah = new JonahState(0)
            {
                State = I(test, "state"), Substate = I(test, "substate"), CurrentWaypoint = 15,
                SignalFlags = U(test, "signals"), BehaviorFlags = U(test, "behavior"), SurfaceClearance = (short)I(test, "clearance"),
                Animation = AnimationPlaybackState.Initial with
                { ClipId = (ushort)I(test, "clip"), Frame = (short)I(test, "frame"), Finished = (byte)I(test, "finished") }
            };
            var actor = new ActorProcedureState(new()
            {
                Position = new(100001, -200002, 300003),
                Velocity = new(0x60 * 101, 0x64 * 101, 0x68 * 101),
                Acceleration = new(0x6C * 101, 0x70 * 101, 0x74 * 101),
                LocalVelocity = new(0x27C * 101, 0x280 * 101, 0x284 * 101),
                AngularVelocity = new(-4, -2, 0), AngularAcceleration = new(2, 4, 6)
            }) { CompletionFlags = 0xABCDEF09 };
            var brain = new JonahBrainState(jonah, actor, new(), new())
            {
                WaitTicks = I(test, "wait"), SpeechSuppressionTicks = I(test, "suppression"), PanicSoundTicks = I(test, "panic"),
                PreviousDisplacementX = 17, PreviousDisplacementZ = -19, LastFootstepSound = I(test, "lastSound"),
                PleaClockOrAlternateVoice = U(test, "pleaWord"), LastRegularPleaVoice = I(test, "regularVoice"),
                StandingY = 111, GroundY = 222, PanicTarget = new(333, 444, 555),
                InputSignals = (ushort)I(test, "collision")
            };
            brain.SelectAnimationSet(0x02020202, unchecked(2u | ((uint)I(test, "mode") << 8)));
            var host = new Host(brain, test);
            brain.ScorpionHandle = new(I(test, "handlePointer") != 0 ? host.Scorpion : null, I(test, "validHandle") != 0 ? 321u : 999u);
            if (I(test, "hasProcedure") != 0) brain.Procedures.Attach(host.Procedure);
            if (I(test, "method") == 3)
                for (int message = 2; message >= 0; message--) brain.Messages.Enqueue(host.Messages[message]);
            int elapsed = I(test, "elapsed");
            switch (I(test, "method"))
            {
                case 0: JonahBrain.Initialize(brain, host, elapsed); break;
                case 1: JonahBrain.Plead(brain, host, elapsed); break;
                case 2: JonahBrain.Fret(brain, host); break;
                case 3: JonahBrain.Update(brain, host, elapsed); break;
            }
            JsonElement expected = test.GetProperty("expected");
            long[] sourceState = Numbers(expected.GetProperty("state"));
            long[] currentState = host.Snapshot();
            int mismatch = -1;
            for (int field = 0; field < currentState.Length; field++)
                if (currentState[field] != sourceState[field]) { mismatch = field; break; }
            assert(mismatch < 0,
                $"Whole PC Jonah case {index} preserves all {currentState.Length} canonical decision/motion/animation/message/handle fields; mismatch field {mismatch}: " +
                (mismatch < 0 ? "none" : $"actual {currentState[mismatch]}, expected {sourceState[mismatch]}.") );
            long[][] sourceEvents = expected.GetProperty("events").EnumerateArray().Select(Numbers).ToArray();
            assert(host.Events.Count == sourceEvents.Length && host.Events.Zip(sourceEvents).All(pair => pair.First.SequenceEqual(pair.Second)),
                $"Whole PC Jonah case {index++} preserves source host call order, operands, and all fields visible before each callback; " +
                $"actual kinds [{string.Join(',', host.Events.Select(e => e[0]))}], expected [{string.Join(',', sourceEvents.Select(e => e[0]))}].");
            foreach (long[] call in sourceEvents) callbackKinds.Add((int)call[0]);
            assert(actor.CompletionFlags == 0xABCDEF09,
                "Jonah actor signals0x218 remain separate from procedure completion0x288.");
        }
        assert(Enumerable.Range(1, 27).All(callbackKinds.Contains), "The original corpus covers every required Jonah host boundary and all outer frame dispatch leaves.");
        VerifyCanonicalOwnership(assert);
        VerifyInputOwnership(assert,document.RootElement[0]);
    }

    private static void VerifyCanonicalOwnership(Action<bool, string> assert)
    {
        var jonah = new JonahState(0) { SignalFlags = 0x400, BehaviorFlags = 0x02002001 };
        var actor = new ActorProcedureState(new()) { CompletionFlags = 0xDEADBEEF };
        var brain = new JonahBrainState(jonah, actor, new(), new());
        actor.BehaviorFlags |= 0x40000000;
        assert(jonah.BehaviorFlags == actor.BehaviorFlags, "Jonah constructor and AI procedures share the same source0x2A8 owner.");
        jonah.ShouldPlead();
        assert(jonah.SignalFlags == 0x40C && actor.CompletionFlags == 0xDEADBEEF,
            "The real module ShouldPlead signals Jonah without overwriting unrelated procedure completion flags.");
        brain.Neutralize();
        assert(jonah.BehaviorFlags == (0x42002001u & 0xB7FFFFFB) && actor.CompletionFlags == 0xDEADBEEF,
            "The recovered neutralizer updates the shared behavior owner while preserving completion flags.");
    }

    private static void VerifyInputOwnership(Action<bool,string> assert,JsonElement test)
    {
        var actor = new ActorProcedureState(new()) { CompletionFlags = 0xA5A5A5A5 };
        var brain = new JonahBrainState(new(0) { State=0,Substate=1 },actor,new(),new());
        assert(brain.InputSignals==0,"Unbound Jonah input storage starts with the source constructor's zero44 word.");
        brain.InputSignals=0x41;
        actor.Body.CollisionFlags=0xFFFF;
        actor.Body.Integrate(2);
        assert(actor.Body.CollisionFlags==0 && brain.InputSignals==0x41,
            "Physical DoPhysics clears mCollisionE0 while retaining Jonah's unbound input word44.");
        var source = new ActorScriptState { ActorType=316,NodeId=2,PendingSignals=0x8001 };
        brain.BindInputSignals(source);
        actor.Body.CollisionFlags=0xFFFF;
        actor.Body.Integrate(2);
        assert(brain.InputSignals==0x8001 && source.PendingSignals==0x8001 && actor.Body.CollisionFlags==0,
            "Binding preserves already queued registry input signals and physical integration cannot consume the canonical script44 owner.");
        JonahBrain.Initialize(brain,new Host(brain,test),2);
        assert(source.PendingSignals==0x8000 && brain.InputSignals==0x8000 && brain.Jonah.Substate==2 &&
            brain.Jonah.Animation.ClipId==25 && actor.Body.CollisionFlags==0 && actor.CompletionFlags==0xA5A5A5A5,
            "Actual Jonah Init consumes only input44 bit1, selects clip25 and advances while preserving independent collisionE0/procedure288 storage.");
        source.PendingSignals|=1;
        assert(brain.InputSignals==0x8001,"A real registry signal write is immediately visible to Jonah's bound source input getter.");
        brain.InputSignals=0x1234;
        assert(source.PendingSignals==0x1234,"Jonah input writes reach the same script/registry word without synchronization copies.");
        brain.BindInputSignals(source);
        bool rejected=false;
        try { brain.BindInputSignals(new()); }
        catch(InvalidOperationException) { rejected=true; }
        assert(rejected && source.PendingSignals==0x1234,"A captured Jonah input owner cannot transfer to a replacement source body.");
        brain.Jonah.WaitUpdates=55;
        brain.WaitTicks=37;
        assert(actor.Body.LimitedMotionTicks==37 && actor.Body.ScriptWaitTicks==55,
            "Jonah's AI wait1F8 aliases physical limited motion while constructor/script wait230 retains its distinct source word.");
        actor.Body.Integrate(2,limitElapsed:true);
        assert(brain.WaitTicks==35 && actor.Body.LimitedMotionTicks==35 && brain.Jonah.WaitUpdates==55 && source.PendingSignals==0x1234,
            "The actual limited DoPhysics path decrements canonical Jonah wait1F8 without consuming script wait230 or input44.");
        actor.Body.LimitedMotionTicks=9;
        assert(brain.WaitTicks==9,"A timed-motion command's physical wait assignment is immediately visible to the same Jonah AI timer.");
    }

    private static int I(JsonElement test, string property) => test.GetProperty(property).GetInt32();
    private static uint U(JsonElement test, string property) => test.GetProperty(property).GetUInt32();
    private static long[] Numbers(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64()).ToArray();

    private sealed class Host : IJonahBrainHost
    {
        private readonly JonahBrainState brain;
        private readonly JsonElement test;
        private bool valid;
        private int addCalls;
        public object Scorpion { get; } = new();
        public List<long[]> Events { get; } = [];
        public FrameProcedure Procedure { get; }
        public ThugMessage[] Messages { get; } = [new(20), new(8), new(22)];
        public Host(JonahBrainState brain, JsonElement test)
        {
            this.brain = brain;
            this.test = test;
            valid = I(test, "validHandle") != 0;
            Procedure = new(this);
            if (I(test, "method") == 3)
                for (int index = 0; index < Messages.Length; index++) Messages[index].Flags = index * 4;
        }
        public uint Clock => U(test, "clock");
        public bool PlayerSynthesizingInput => I(test, "synthesizing") != 0;
        public object? RecoverScorpion(PlayerKickHandle handle) { Event(1); return valid && ReferenceEquals(handle.Target, Scorpion) ? Scorpion : null; }
        public object? FindFirstScorpion() => I(test, "hasScorpion") != 0 ? Scorpion : null;
        public PlayerKickHandle MakeScorpionHandle(object scorpion) { Event(2); valid = true; return new(Scorpion, 321); }
        public JonahState? ScorpionCurrentTarget(object scorpion) { Event(3); return I(test, "isTarget") != 0 ? brain.Jonah : null; }
        public bool ScorpionVictorious(object scorpion) { Event(4); return I(test, "victorious") != 0; }
        public bool ScorpionContactSignal(object scorpion) => I(test, "contact") != 0;
        public int Random(int exclusiveMaximum) { Event(6, exclusiveMaximum); return I(test, "random") % exclusiveMaximum; }
        public void RunAnimation(ushort clip, int startFrame, int endFrame)
        { Event(7, clip, startFrame, endFrame); brain.Jonah.Animation = brain.Jonah.Animation with { ClipId = clip, Frame = (short)startFrame, Finished = 0 }; }
        public void CycleAnimation(ushort clip, sbyte direction)
        { Event(8, clip, direction); brain.Jonah.Animation = brain.Jonah.Animation with { ClipId = clip, Frame = 0, Finished = 0 }; }
        public void TryLookAtScorpion(object scorpion, uint completionMask, int rate, int tolerance)
        { if (I(test, "allocation") != 0) Event(5, completionMask, rate, tolerance); }
        public void TrySendWaypointMessage(object scorpion, int code, int positionMode, FixedVector3 position, FixedVector3 waypoint)
        { if (I(test, "allocation") != 0) Event(25, code, positionMode, position.X, position.Y, position.Z, waypoint.X, waypoint.Y, waypoint.Z); }
        public void PlayVoice(int bank, int index, FixedVector3 position, int flags) => Event(9, bank, index, flags);
        public void PlaySound(uint sound, FixedVector3 position, int pitchOffset) => Event(10, sound, pitchOffset);
        public int GroundHeight(FixedVector3 position, int above, int below, int flags) { Event(11, above, below, flags); return I(test, "ground"); }
        public FixedVector3 WaypointPosition(int waypoint) { Event(12, waypoint); return new(1000001, -2000002, 3000003); }
        public JonahPathTrace TracePath(FixedVector3 start, FixedVector3 end, int clearance)
        { Event(13, start.X, start.Y, start.Z, end.X, end.Y, end.Z, clearance); int trace = I(test, "trace"); return new(trace, start with { X = unchecked(start.X + (trace == 2 ? 600000 : 100000)) }); }
        public bool ScorpionBlocksPath(FixedVector3 start, FixedVector3 end, object scorpion)
        { Event(14, start.X, start.Y, start.Z, end.X, end.Y, end.Z); return I(test, "blocked") != 0; }
        public bool AddPointToPath(FixedVector3 position, int flags)
        { Event(15, position.X, position.Y, position.Z, flags); return I(test, addCalls++ == 0 ? "addFirst" : "addSecond") != 0; }
        public void DoPhysics(int limitElapsed) => Event(16, limitElapsed);
        public void BuildTransform() => Event(17);
        public void SetMovementHeight(int flags, int clearance, int distance) => Event(18, flags, clearance, distance);
        public void FollowWaypoints() => Event(19);
        public void Hide() => Event(20);
        public void TakeHit() => Event(21);
        public void Die() => Event(22);
        public void GoAroundScorpion(object scorpion) => Event(23);
        public void DrawShadow() => Event(24);
        public void SourceDiagnostic(string message) => Event(26);
        private void Event(int kind, params long[] arguments) => Events.Add(new long[] { kind }.Concat(arguments).Concat(Snapshot()).ToArray());
        public long[] Snapshot()
        {
            JonahState j = brain.Jonah;
            ActorBodyMotion b = brain.Actor.Body;
            int count = 0;
            for (ThugMessage? message = brain.Messages.Head; message is not null; message = message.Next) count++;
            return [j.State, j.Substate, brain.WaitTicks, brain.SpeechSuppressionTicks, brain.PanicSoundTicks,
                brain.PreviousDisplacementX, brain.PreviousDisplacementZ, brain.LastFootstepSound, j.SignalFlags, j.BehaviorFlags,
                j.MovementAnimations, j.IdleAnimations, brain.PleaClockOrAlternateVoice, brain.LastRegularPleaVoice,
                brain.StandingY, brain.GroundY, b.Position.X, b.Position.Y, b.Position.Z, brain.PanicTarget.X, brain.PanicTarget.Y, brain.PanicTarget.Z,
                j.Animation.ClipId, j.Animation.Frame, j.Animation.Finished, brain.InputSignals, brain.CachedStateIndex, brain.Actor.CompletionFlags,
                b.Velocity.X, b.Velocity.Y, b.Velocity.Z, b.Acceleration.X, b.Acceleration.Y, b.Acceleration.Z,
                b.LocalVelocity.X, b.LocalVelocity.Y, b.LocalVelocity.Z, b.AngularVelocity.X, b.AngularVelocity.Y, b.AngularVelocity.Z,
                b.AngularAcceleration.X, b.AngularAcceleration.Y, b.AngularAcceleration.Z,
                ReferenceEquals(brain.ScorpionHandle.Target, Scorpion) ? 1 : 0, brain.ScorpionHandle.Validation,
                Messages[0].Flags, Messages[1].Flags, Messages[2].Flags, count, brain.Procedures.Count != 0 ? 1 : 0, Procedure.Flags];
        }
        public sealed class FrameProcedure(Host host) : IActorProcedure
        {
            public int SourceType => 0x20000;
            public int Flags { get; set; }
            public Action? ExecuteNext { get; set; }
            public void Execute(int elapsedTicks)
            {
                host.Event(27);
                host.brain.Jonah.State = I(host.test, "procedureState");
                host.brain.Jonah.Substate = I(host.test, "procedureSubstate");
                host.brain.Position = host.brain.Position with { Z = unchecked(host.brain.Position.Z - 23) };
                if (I(host.test, "procedureRetire") != 0) Flags |= 1;
            }
        }
    }
}
