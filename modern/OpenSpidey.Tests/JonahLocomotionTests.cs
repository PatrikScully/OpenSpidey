using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class JonahLocomotionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(JonahLocomotionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.jonah-locomotion.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        HashSet<int> callbacks = [];
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var jonah = new JonahState(0)
            {
                State = I(test, "state"), Substate = I(test, "substate"), CurrentWaypoint = 15, MinorRadius = 100,
                Health = (short)I(test, "health"),
                SignalFlags = U(test, "signals"), BehaviorFlags = U(test, "behavior"), SurfaceClearance = (short)I(test, "clearance"),
                Animation = AnimationPlaybackState.Initial with
                { ClipId = (ushort)I(test, "clip"), Frame = (short)I(test, "frame"), Finished = (byte)I(test, "finished") }
            };
            var actor = new ActorProcedureState(new()
            {
                Position = new(100001, -200002, 300003),
                Velocity = new(0x60 * 101, 0x64 * 101, 0x68 * 101), Acceleration = new(0x6C * 101, 0x70 * 101, 0x74 * 101),
                LocalVelocity = new(0x27C * 101, 0x280 * 101, 0x284 * 101),
                AngularVelocity = new(-4, -2, 0), AngularAcceleration = new(2, 4, 6)
            }) { CompletionFlags = U(test, "completion") };
            var brain = new JonahBrainState(jonah, actor, new(), new())
            {
                WaitTicks = I(test, "wait"), SpeechSuppressionTicks = I(test, "suppression"), PanicSoundTicks = I(test, "panic"),
                PreviousDisplacementX = 17, PreviousDisplacementZ = -19, LastFootstepSound = I(test, "lastSound"),
                PleaClockOrAlternateVoice = U(test, "pleaWord"), LastRegularPleaVoice = I(test, "regularVoice"),
                StandingY = 111, GroundY = 222, PanicTarget = new(333, 444, 555), InputSignals = (ushort)I(test,"collision")
            };
            brain.SelectAnimationSet(0x02020202, unchecked(2u | ((uint)I(test, "mode") << 8)));
            var state = new JonahLocomotionState(brain)
            {
                WaypointRole = I(test, "role"), MovementRestart = 55, HitReactionContext = I(test, "reaction"),
                ObstructionUpdates = I(test, "obstruction"), NextRoomWaypoint = I(test, "nextNode"), HasNextRoomWaypoint = I(test, "hasNext")
            };
            var host = new Host(state, test);
            brain.ScorpionHandle = new(I(test, "handlePointer") != 0 ? host.Scorpion : null, I(test, "validHandle") != 0 ? 321u : 999u);
            state.HidingPlace = new(host.Environment, 999);
            bool result = false;
            switch (I(test, "method"))
            {
                case 0: JonahLocomotion.FollowWaypoints(state, host); break;
                case 1: result = JonahLocomotion.Arrival(state, host); break;
                case 2: result = JonahLocomotion.GetNextWaypoint(state, host); break;
                case 3: result = JonahLocomotion.LinkedHidingPlaceStillExists(state, host, I(test, "successor") != 0); break;
                case 4: JonahLocomotion.TakeHit(state, host); break;
                case 5: JonahLocomotion.Hide(state, host); break;
                case 6: JonahLocomotion.Die(state, host, I(test, "elapsed")); break;
                case 7: new JonahEncounterBehavior(state, host).Update(I(test, "elapsed")); break;
                case 8: result = JonahDamage.Hit(state, host, ReadHit(test)); break;
                case 9:
                    result = JonahDamage.Hit(state, host, ReadHit(test));
                    new JonahEncounterBehavior(state, host).Update(I(test, "elapsed"));
                    break;
            }
            JsonElement expected = test.GetProperty("expected");
            long[] source = Numbers(expected.GetProperty("state")), current = host.Snapshot();
            int mismatch = -1;
            for (int field = 0; field < current.Length; field++)
                if (current[field] != source[field]) { mismatch = field; break; }
            assert(mismatch < 0 && (result ? 1 : 0) == expected.GetProperty("result").GetInt32(),
                $"Whole PC Jonah locomotion case {index} method{I(test, "method")} role{I(test, "role")} substate{I(test, "substate")} " +
                $"preserves all69 state fields and return; result {result}/{expected.GetProperty("result")}, mismatch field {mismatch}: " +
                (mismatch < 0 ? "none" : $"actual {current[mismatch]}, expected {source[mismatch]}."));
            long[][] sourceEvents = expected.GetProperty("events").EnumerateArray().Select(Numbers).ToArray();
            assert(host.Events.Count == sourceEvents.Length && host.Events.Zip(sourceEvents).All(pair => pair.First.SequenceEqual(pair.Second)),
                $"Whole PC Jonah locomotion case {index++} preserves source callback arguments and69 fields visible at every stage; " +
                $"actual kinds [{string.Join(',', host.Events.Select(e => e[0]))}], expected [{string.Join(',', sourceEvents.Select(e => e[0]))}].");
            foreach (long[] call in sourceEvents) callbacks.Add((int)call[0]);
        }
        int[] required = [1, 2, 5, 6, 7, 8, 10, 12, 14, 26, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42];
        assert(required.All(callbacks.Contains), "The original locomotion corpus reaches every actual link/environment/move/message/player/camera transport boundary.");
    }

    private static int I(JsonElement test, string property) => test.GetProperty(property).GetInt32();
    private static uint U(JsonElement test, string property) => test.GetProperty(property).GetUInt32();
    private static long[] Numbers(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private static ActorHit ReadHit(JsonElement test) => new((byte)I(test, "hitFlags"), 0, I(test, "attackKind"), (ushort)I(test, "damage"), default);
    private static JonahWaypointLink[] Links(JsonElement values) => values.EnumerateArray().Select(link =>
        new JonahWaypointLink(link[0].GetInt32(), link[1].GetInt32(), link[2].GetInt32(), link[3].GetInt32())).ToArray();

    private sealed class Host : IJonahLocomotionHost
    {
        private readonly JonahLocomotionState state;
        private readonly JsonElement test;
        private readonly JonahWaypointLink[] links, secondaryLinks;
        private bool valid;
        private int inputSuppression, levelStatus, addCalls;
        private bool trackPosition, trackOrientation;
        private short cameraAngle;
        public object Scorpion { get; } = new();
        public object Environment { get; } = new();
        public List<long[]> Events { get; } = [];
        public int PathPointCount { get; set; }
        private JonahBrainState Brain => state.Brain;
        public Host(JonahLocomotionState state, JsonElement test)
        {
            this.state = state;
            this.test = test;
            valid = I(test, "validHandle") != 0;
            links = Links(test.GetProperty("links"));
            secondaryLinks = Links(test.GetProperty("secondaryLinks"));
            PathPointCount = I(test, "pathCount");
            cameraAngle = (short)I(test, "cameraAngle");
        }
        public uint Clock => U(test, "clock");
        public bool PlayerSynthesizingInput => I(test, "synthesizing") != 0;
        public object? RecoverScorpion(PlayerKickHandle handle) { Event(1); return valid && ReferenceEquals(handle.Target, Scorpion) ? Scorpion : null; }
        public object? FindFirstScorpion() => I(test, "hasScorpion") != 0 ? Scorpion : null;
        public PlayerKickHandle MakeScorpionHandle(object scorpion) { Event(2); valid = true; return new(Scorpion, 321); }
        public int Random(int exclusiveMaximum) { Event(6, exclusiveMaximum); return exclusiveMaximum == 0 ? 0 : I(test, "random") % exclusiveMaximum; }
        public void RunAnimation(ushort clip, int startFrame, int endFrame)
        { Event(7, clip, startFrame, endFrame); state.Jonah.Animation = state.Jonah.Animation with { ClipId = clip, Frame = (short)startFrame, Finished = 0 }; }
        public void CycleAnimation(ushort clip, sbyte direction)
        { Event(8, clip, direction); state.Jonah.Animation = state.Jonah.Animation with { ClipId = clip, Frame = 0, Finished = 0 }; }
        public void TryLookAtScorpion(object scorpion, uint completionMask, int rate, int tolerance)
        { if (I(test, "allocation") != 0) Event(5, completionMask, rate, tolerance); }
        public void PlaySound(uint sound, FixedVector3 position, int pitchOffset) => Event(10, sound, pitchOffset);
        public FixedVector3 WaypointPosition(int waypoint) { Event(12, waypoint); return new(1000001, -2000002, 3000003); }
        public bool ScorpionBlocksPath(FixedVector3 start, FixedVector3 end, object scorpion)
        { Event(14, start.X, start.Y, start.Z, end.X, end.Y, end.Z); return I(test, "blocked") != 0; }
        public IReadOnlyList<JonahWaypointLink> ReadWaypointLinks(int node, int maximumCount)
        { Event(28, node, maximumCount); return node == 15 ? links : secondaryLinks; }
        public object? FindEnvironmentBody(int node) { Event(31, node); return I(test, "environmentExists") != 0 && node == I(test, "environmentNode") ? Environment : null; }
        public PlayerKickHandle MakeEnvironmentHandle(object body) { Event(29); return new(Environment, 777); }
        public object? RecoverEnvironmentBody(PlayerKickHandle handle)
        { Event(30); return I(test, "environmentRecovery") != 0 && ReferenceEquals(handle.Target, Environment) ? Environment : null; }
        public object? PlayerHeldObject => I(test, "held") != 0 ? Environment : null;
        public short PlayerHealth => I(test, "synthesizing") != 0 ? (short)0 : (short)100;
        public FixedVector3 ScorpionPosition(object scorpion) => new(unchecked(100001 + I(test, "scorpionDistance") * 4096), -200002, 300003);
        public FixedVector3 PathPoint(int index) => new(10000 * (index + 1), -20000 * (index + 1), 30000 * (index + 1));
        public void ExecuteWaypointScript(int node) => Event(34, node);
        public void TryMoveToPoint(FixedVector3 destination, int speed, int turnRate, int facingTolerance, uint completionMask)
        { if (I(test, "allocation") != 0) Event(33, destination.X, destination.Y, destination.Z, speed, turnRate, facingTolerance, completionMask); }
        public void TrySendScorpionParameterMessage(object scorpion, int code, int parameter)
        { if (I(test, "allocation") != 0) Event(32, code, parameter); }
        public void SuppressPlayerInput(int ticks) { Event(35, ticks); inputSuppression = ticks; }
        public bool HasCamera => I(test, "hasCamera") != 0;
        public short CameraAngle => cameraAngle;
        public void SetCameraMode(int mode) => Event(36, mode);
        public void TrackJonahPositionAndOrientation(JonahState jonah) { trackPosition = true; trackOrientation = true; }
        public void SetCameraAngle(short angle, ushort ticks) { Event(37, angle, ticks); cameraAngle = angle; }
        public void SetCameraXOffset(short offset, ushort ticks) => Event(38, offset, ticks);
        public void SetCameraYOffset(short offset, ushort ticks) => Event(39, offset, ticks);
        public void SetCameraZOffset(short offset, ushort ticks) => Event(40, offset, ticks);
        public void SetCameraHorizontalDistance(ushort distance, ushort ticks) => Event(41, distance, ticks);
        public void SetCameraVerticalDistance(short distance, ushort ticks) => Event(42, distance, ticks);
        public void SetLevelStatus(int status) => levelStatus = status;
        public void SourceDiagnostic(string message) => Event(26);
        private void Event(int kind, params long[] arguments) => Events.Add(new long[] { kind }.Concat(arguments).Concat(Snapshot()).ToArray());
        public long[] Snapshot()
        {
            JonahState j = state.Jonah;
            ActorBodyMotion b = Brain.Actor.Body;
            return [j.State, j.Substate, Brain.WaitTicks, Brain.SpeechSuppressionTicks, Brain.PanicSoundTicks,
                Brain.PreviousDisplacementX, Brain.PreviousDisplacementZ, Brain.LastFootstepSound, j.SignalFlags, j.BehaviorFlags,
                j.MovementAnimations, j.IdleAnimations, Brain.PleaClockOrAlternateVoice, Brain.LastRegularPleaVoice,
                Brain.StandingY, Brain.GroundY, b.Position.X, b.Position.Y, b.Position.Z, Brain.PanicTarget.X, Brain.PanicTarget.Y, Brain.PanicTarget.Z,
                j.Animation.ClipId, j.Animation.Frame, j.Animation.Finished, Brain.InputSignals, Brain.CachedStateIndex, Brain.Actor.CompletionFlags,
                b.Velocity.X, b.Velocity.Y, b.Velocity.Z, b.Acceleration.X, b.Acceleration.Y, b.Acceleration.Z,
                b.LocalVelocity.X, b.LocalVelocity.Y, b.LocalVelocity.Z, b.AngularVelocity.X, b.AngularVelocity.Y, b.AngularVelocity.Z,
                b.AngularAcceleration.X, b.AngularAcceleration.Y, b.AngularAcceleration.Z,
                ReferenceEquals(Brain.ScorpionHandle.Target, Scorpion) ? 1 : 0, Brain.ScorpionHandle.Validation,
                0, 0, 0, 0, 0, 0,
                state.WaypointRole, state.MovementRestart, state.HitReactionContext, state.ObstructionUpdates,
                ReferenceEquals(state.HidingPlace.Target, Environment) ? 1 : 0, state.HidingPlace.Validation,
                state.NextRoomWaypoint, state.HasNextRoomWaypoint, PathPointCount, j.MinorRadius,
                trackPosition ? 1 : 0, trackPosition ? 1 : 0, trackOrientation ? 1 : 0, trackOrientation ? 1 : 0,
                cameraAngle, inputSuppression, levelStatus, j.Health];
        }
        public JonahState? ScorpionCurrentTarget(object scorpion) { Event(3); return I(test, "isTarget") != 0 ? state.Jonah : null; }
        public bool ScorpionVictorious(object scorpion) { Event(4); return I(test, "victorious") != 0; }
        public bool ScorpionContactSignal(object scorpion) => I(test, "contact") != 0;
        public void TrySendWaypointMessage(object scorpion, int code, int positionMode, FixedVector3 position, FixedVector3 waypoint)
        { if (I(test, "allocation") != 0) Event(25, code, positionMode, position.X, position.Y, position.Z, waypoint.X, waypoint.Y, waypoint.Z); }
        public void PlayVoice(int bank, int index, FixedVector3 position, int flags) => Event(9, bank, index, flags);
        public int GroundHeight(FixedVector3 position, int above, int below, int flags)
        { Event(11, above, below, flags); return I(test, "ground"); }
        public JonahPathTrace TracePath(FixedVector3 start, FixedVector3 end, int clearance)
        { Event(13, start.X, start.Y, start.Z, end.X, end.Y, end.Z, clearance); int trace = I(test, "trace"); return new(trace, start with { X = unchecked(start.X + (trace == 2 ? 600000 : 100000)) }); }
        public bool AddPointToPath(FixedVector3 position, int flags)
        { Event(15, position.X, position.Y, position.Z, flags); return I(test, addCalls++ == 0 ? "addFirst" : "addSecond") != 0; }
        public void DoPhysics(int limitElapsed) => Event(16, limitElapsed);
        public void BuildTransform() => Event(17);
        public void SetMovementHeight(int flags, int clearance, int distance) => Event(18, flags, clearance, distance);
        // Composed AI must call its concrete leaves rather than these obsolete outer host slots.
        public void FollowWaypoints() => throw new InvalidOperationException();
        public void Hide() => throw new InvalidOperationException();
        public void TakeHit() => throw new InvalidOperationException();
        public void Die() => throw new InvalidOperationException();
        public void GoAroundScorpion(object scorpion) => Event(23);
        public void DrawShadow() => Event(24);
    }
}
