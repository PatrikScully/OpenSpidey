using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class ScorpionBehaviorTests
{
    private static readonly string[] Names = ["GetTugged", "WindUp", "ReactToJonah", "AvoidObstacle", "Die", "PursueTarget", "FollowWaypoints"];

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ScorpionBehaviorTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.scorpion-behavior.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var host = new Host(test);
            JsonElement expected = test.GetProperty("expected");
            int method = host.Read("method");
            switch (method)
            {
                case 0: ScorpionBehavior.GetTugged(host.State, host); break;
                case 1: ScorpionBehavior.WindUpEnvironmentalAttack(host.State, host, host.Random); break;
                case 2: ScorpionBehavior.ReactToJonah(host.State, host); break;
                case 3: ScorpionBehavior.AvoidObstacle(host.State, host); break;
                case 4: ScorpionBehavior.Die(host.State, host, host.Read("elapsed")); break;
                case 5: ScorpionBehavior.PursueTarget(host.State, host, host.Target); break;
                default: ScorpionBehavior.FollowWaypoints(host.State, host); break;
            }
            long[] actual = host.Snapshot(), original = Numbers(expected.GetProperty("snapshot"));
            int mismatch = Enumerable.Range(0, Math.Min(actual.Length, original.Length)).FirstOrDefault(field => actual[field] != original[field], -1);
            assert(actual.SequenceEqual(original),
                $"Whole PC Scorpion {Names[method]} case{index}, substate{host.Read("substate")}: field{mismatch}; state, counters, flags, path point, handles, camera targets and vectors match.");
            JsonElement events = expected.GetProperty("events");
            int eventMismatch = Enumerable.Range(0, Math.Min(host.Events.Count, events.GetArrayLength())).FirstOrDefault(
                item => !host.Events[item].SequenceEqual(Numbers(events[item])), -1);
            assert(host.Events.Count == events.GetArrayLength() && eventMismatch == -1,
                $"Whole PC Scorpion {Names[method]} case{index++}, callback{eventMismatch} of{host.Events.Count}/{events.GetArrayLength()}: procedure, animation, speech, path, camera and diagnostic order match.");
        }
    }

    private static long[] Numbers(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private sealed class Body(int identity, FixedVector3 position)
    {
        public int Identity { get; } = identity;
        public FixedVector3 Position { get; } = position;
    }

    private sealed class Host : IScorpionBehaviorHost
    {
        private readonly JsonElement input;
        private readonly long[][] paths;
        private readonly Body player, jonah, web, body;
        private readonly FixedVector3[] points = new FixedVector3[4];
        private int pathCount, pathCalls, randomCalls;
        private bool tracked;
        public ScorpionTargetState State { get; }
        public object Target { get; }
        public List<long[]> Events { get; } = [];
        public int Read(string name) => unchecked((int)input.GetProperty(name).GetInt64());
        private uint Unsigned(string name) => unchecked((uint)input.GetProperty(name).GetInt64());
        private short Short(string name) => unchecked((short)input.GetProperty(name).GetInt64());
        private FixedVector3 Vector(string x, string y, string z) => new(Read(x), Read(y), Read(z));

        public Host(JsonElement test)
        {
            input = test.GetProperty("input");
            paths = test.GetProperty("paths").EnumerateArray().Select(Numbers).ToArray();
            player = new(1, Vector("playerX", "playerY", "playerZ"));
            body = new(3, Vector("bodyX", "bodyY", "bodyZ"));
            jonah = new(2, Vector("jonahX", "jonahY", "jonahZ"));
            web = new(4, default);
            pathCount = Read("pathCount");
            for (int index = 1; index < 4; index++) points[index] = Vector($"pt{index}X", $"pt{index}Y", $"pt{index}Z");
            Target = Read("target") switch { 1 => player, 2 => jonah, _ => body };
            BaddyCollisionCheck = Read("baddyCheck");
            var scorpion = new ScorpionState(0)
            {
                State = Read("state"), Substate = Read("substate"), SurfaceClearance = Short("clearance"), BodyFlags = unchecked((ushort)Read("bodyFlags")),
                MinorRadius = unchecked((ushort)Read("radius")), ActorPhase = unchecked((byte)Read("phase")), CurrentWaypoint = Read("waypoint"),
                Animation = AnimationPlaybackState.Initial with { Frame = Short("frame"), Finished = unchecked((byte)Read("finished")) }
            };
            // The constructor-owned setters are internal; these fixtures vary them directly.
            typeof(ScorpionEncounterBodyState).GetProperty(nameof(ScorpionEncounterBodyState.SourceNode))!.SetValue(scorpion, unchecked((ushort)Read("sourceNode")));
            typeof(ScorpionEncounterBodyState).GetProperty(nameof(ScorpionEncounterBodyState.IdleAnimations))!.SetValue(scorpion, Unsigned("idleByte"));
            typeof(ScorpionEncounterBodyState).GetProperty(nameof(ScorpionEncounterBodyState.MovementAnimations))!.SetValue(scorpion, Unsigned("movementByte") << 8);
            var actor = new ActorProcedureState(new()
            {
                Position = Vector("px", "py", "pz"), Velocity = Vector("vx", "vy", "vz"), Acceleration = Vector("ax", "ay", "az"),
                LocalVelocity = Vector("lx", "ly", "lz"), AngularVelocity = new(Short("avx"), Short("avy"), Short("avz")),
                AngularAcceleration = new(Short("aax"), Short("aay"), Short("aaz")), Angles = new(0, Short("angleY"), 0)
            }) { CompletionFlags = Unsigned("completion") };
            var damage = new ScorpionDamageState(scorpion, actor, new())
            {
                CachedJonah = Read("cachedJonah") != 0 ? jonah : null, SlideUpdates = Read("counter"), ReactionFlags = Unsigned("reaction"),
                Field310 = Read("field310"), FireQuipSuppressionUpdates = Read("quipSuppression"), TargetKind = Read("targetKind"),
                PlayerTargetUpdates = Read("playerTargetUpdates")
            };
            scorpion.BehaviorFlags = Unsigned("behavior");
            var brain = new ScorpionBrainState(damage, new())
            {
                StandingY = Read("standingY"), SignalFlags = Unsigned("signals"), DeathPulseSent = unchecked((byte)Read("pulseSent")),
                TugWeb = new(Read("tugPointer") != 0 ? web : null, Unsigned("tugValidation")), LastWindUpQuip = Read("lastWindUp"),
                ObstaclePosition = Vector("obsX", "obsY", "obsZ"), ObstacleWaypoint = Vector("obwX", "obwY", "obwZ"),
                ObstacleAxis = Vector("axisX", "axisY", "axisZ"), ObstacleSide = Vector("sideX", "sideY", "sideZ"),
                ChargeIndex = Read("chargeIndex"), ChargeTarget = Vector("chargeX", "chargeY", "chargeZ"), ChargeStart = Vector("startX", "startY", "startZ"),
                ChargeAttempts = Read("chargeAttempts"), BreakWallChecksum = Unsigned("checksum")
            };
            State = new(brain) { EnvironmentAimPosition = Vector("envX", "envY", "envZ") };
        }

        public int Random(int bound)
        {
            int result = Read(randomCalls++ == 0 ? "rand0" : "rand1");
            Event(1, bound, result);
            return result;
        }
        private bool Allocate(int size)
        {
            if (Read("allocAccepted") != 0) return true;
            Event(2, size);
            return false;
        }
        public void TryAttachPointLookAt(FixedVector3 target, uint completionMask, int rate, int tolerance)
        { if (Allocate(64)) Event(3, 0, 1, target.X, target.Y, target.Z, completionMask, rate, tolerance); }
        public void TryAttachStateSwitchMessage(int code) { if (Allocate(40)) Event(4, code); }
        public void RunAnimation(ushort clip, int from, int to) => Event(5, clip, from, to);
        public void CycleAnimation(ushort clip, sbyte direction) => Event(6, clip, direction);
        public void PlayPositionSound(uint sound, FixedVector3 position, int pitch)
        {
            if (position != State.Brain.Damage.Actor.Body.Position) throw new InvalidDataException("Sound must use the Scorpion position.");
            Event(7, sound, pitch);
        }
        public bool PlayPositionSpeech(int bank, int slot, FixedVector3 position, int priority)
        {
            if (position != State.Brain.Damage.Actor.Body.Position) throw new InvalidDataException("Speech must use the Scorpion position.");
            Event(8, bank, slot, priority);
            return Read("speechAccepted") != 0;
        }
        public void AttachSpeech(int bank, int slot) => Event(9, bank, slot);
        public void StopSpeech() => Event(10);
        public object? RecoverWeb(PlayerKickHandle handle)
        {
            Event(11);
            return Read("tugValid") != 0 ? handle.Target : null;
        }
        public void BurstWeb(object identity) => Event(12, ((Body)identity).Identity);
        public void RunAppropriateAnimation() => Event(13);
        public FixedVector3 WaypointPosition(int node) { Event(14, node); return Vector("wpX", "wpY", "wpZ"); }
        public int PathCheckWithHit(FixedVector3 start, FixedVector3 end, int clearance, out FixedVector3 hit)
        {
            Event(15, start.X, start.Y, start.Z, end.X, end.Y, end.Z, clearance);
            long[] entry = paths[pathCalls++];
            hit = new(unchecked((int)entry[1]), unchecked((int)entry[2]), unchecked((int)entry[3]));
            return unchecked((int)entry[0]);
        }
        public void SuppressPlayerInput(int ticks) => Event(16, ticks);
        public void SendPulse(ushort node) => Event(17, node);
        public bool HasCamera => Read("hasCamera") != 0;
        public short CameraAngle => Short("cameraAngle");
        public void SetCameraMode(int mode) => Event(18, mode);
        public void TrackScorpionPositionAndOrientation(ScorpionState scorpion)
        {
            if (!ReferenceEquals(scorpion, State.Brain.Body)) throw new InvalidDataException("Camera must track the Scorpion.");
            tracked = true;
        }
        public void SetCameraAngle(short angle, ushort ticks) => Event(19, angle, ticks);
        public void SetCameraXOffset(short offset, ushort ticks) => Event(20, offset, ticks);
        public void SetCameraYOffset(short offset, ushort ticks) => Event(21, offset, ticks);
        public void SetCameraZOffset(short offset, ushort ticks) => Event(22, offset, ticks);
        public void SetCameraHorizontalDistance(ushort distance, ushort ticks) => Event(23, distance, ticks);
        public void SetCameraVerticalDistance(short distance, ushort ticks) => Event(24, distance, ticks);
        public void SourceDiagnostic(string message) => Event(25, message switch
        {
            "Unknown substate." => 0, "Unknown substate!" => 1, "Stuck again. Emergency fix required." => 2, "Unknown method." => 3,
            "Not detecting baddy polys!" => 4, "No checksum in ID_BREAK_WALL type scriptpoint!" => 5, "No link through wall to next room!" => 6,
            "No link through wall into next room!" => 7,
            _ => throw new InvalidDataException(message)
        });
        public void StorePathDestination(FixedVector3 destination) { points[1] = destination; pathCount = 1; }
        public object? FindJonah() => Read("hasJonah") != 0 ? jonah : null;
        public FixedVector3 JonahPosition(object identity) => ((Body)identity).Position;
        public ushort JonahRadius(object identity) => unchecked((ushort)Read("jonahRadius"));

        public bool PlayerIsCrawling => Read("crawling") != 0;
        public void DetermineTarget()
        {
            Event(26);
            if (Read("determineKind") >= 0) State.Brain.Damage.TargetKind = Read("determineKind");
        }
        public object PlayerIdentity => player;
        public FixedVector3 PlayerPosition => player.Position;
        public object? RecoverHandle(ref PlayerKickHandle handle) => throw new InvalidOperationException("These leaves recover no target.");
        public FixedVector3 BodyPosition(object identity) => ((Body)identity).Position;
        public bool LineOfSight(FixedVector3 start, FixedVector3 end, int flags, int argument)
        {
            if (start != State.Brain.Damage.Actor.Body.Position || flags != 0 || argument != 0) throw new InvalidDataException("Visibility starts at the Scorpion.");
            Event(27, end.X, end.Y, end.Z);
            return Read("losPlayer") != 0;
        }
        public uint CallJonahDecisionModule(object identity) => throw new InvalidOperationException("No Jonah module.");
        public PlayerKickHandle JonahHidingPlace(object identity) => throw new InvalidOperationException("No hiding place.");
        public PlayerKickHandle MakeHandle(object identity)
        {
            Event(30, ((Body)identity).Identity);
            return new(identity, 0x9999);
        }
        public object? FirstEnvironment => throw new InvalidOperationException("No environment list.");
        public object? NextEnvironment(object identity) => throw new InvalidOperationException("No environment list.");
        public ushort BodyType(object identity) => ((Body)identity).Identity switch
        {
            1 => Read("bodyType") == 401 ? (ushort)0 : unchecked((ushort)Read("bodyType")), 2 => 316, _ => unchecked((ushort)Read("bodyType"))
        };
        public void TryAttachTargetLookAt(object? target, uint completionMask, int rate, int tolerance)
        { if (Allocate(64)) Event(3, (target as Body)?.Identity ?? 0, 0, 0, 0, 0, completionMask, rate, tolerance); }
        public int AttackClock => Read("attackClock");
        public int BaddyCollisionCheck { get; set; }
        public uint BodySignalFlags(object identity) => ReferenceEquals(identity, Target) ? Unsigned("targetSignals") : 0;
        public void DoPhysics(int limitElapsed)
        {
            Event(28, limitElapsed);
            var motion = State.Brain.Damage.Actor.Body;
            motion.Position = motion.Position with { X = unchecked(motion.Position.X + Read("physicsX")), Z = unchecked(motion.Position.Z + Read("physicsZ")) };
        }
        public ScorpionLineHit LineToItem(FixedVector3 initialStart, FixedVector3 initialEnd, FixedVector3 start, FixedVector3 end)
        {
            Event(29, start.X, start.Y, start.Z, end.X, end.Y, end.Z, Math.Min(initialStart.X, initialEnd.X), Math.Min(initialStart.Y, initialEnd.Y),
                Math.Min(initialStart.Z, initialEnd.Z), Math.Max(initialStart.X, initialEnd.X), Math.Max(initialStart.Y, initialEnd.Y), Math.Max(initialStart.Z, initialEnd.Z), 1);
            return new(Read("lineItem") switch { 1 => player, 2 => jonah, 3 => body, _ => null }, Read("lineFace") != 0);
        }
        public void TryAttachYawRotation(int angle, int rate, uint completionMask) { if (Allocate(40)) Event(31, angle, rate, completionMask); }
        public void TryAttachAcceleration(int acceleration, int start, int completionMask) { if (Allocate(40)) Event(32, acceleration, start, completionMask); }
        public void TryMoveToPoint(FixedVector3 destination, int speed, int turnRate, int facingTolerance, uint completionMask)
        { if (Allocate(64)) Event(33, destination.X, destination.Y, destination.Z, speed, turnRate, facingTolerance, completionMask); }
        public int PathPointCount { get => pathCount; set => pathCount = value; }
        public FixedVector3 PathPoint(int index) => points[index];
        public void RunCurrentScript() => Event(34);
        public bool GetNextWaypoint() { Event(35); return Read("nextWaypoint") != 0; }
        public IReadOnlyList<JonahWaypointLink> ReadWaypointLinks(int node, int maximumCount)
        {
            Event(36, node, maximumCount);
            return Read("linkCount") != 0 ? [new(Read("linkNode"), 0, 0, 0)] : [];
        }
        public ScorpionBreakWall? ReadBreakWall(int node)
        {
            if (node != Read("waypoint") || Read("breakWall") == 0) return null;
            Event(14, node);
            Event(37);
            int offset = Read("squirtOffset") / 2, aligned = (offset + 1) & ~1;
            ushort Word(int index) => unchecked((ushort)Read($"word{index}"));
            return new(Word(offset), (uint)(Word(aligned) | Word(aligned + 1) << 16));
        }
        public void ChunkItemByChecksum(uint checksum) => Event(38, checksum);
        public int Difficulty => throw new InvalidOperationException("No difficulty.");
        public uint PlayerFlags => throw new InvalidOperationException("No player flags.");
        public object? MakePlayerHandle() => throw new InvalidOperationException("No player handle.");
        public void TrySendJonahMessage(object identity, int code) => throw new InvalidOperationException("No message.");
        public void AttachPlayerSpeech(int bank, int slot) => throw new InvalidOperationException("No player speech.");
        public void TryAttachPlayerLookAt(uint mask, int rate, int tolerance) => throw new InvalidOperationException("No player LookAt.");
        public void ReportUnknownRecoverySubstate(int substate) => throw new InvalidOperationException("No recovery.");

        private void Event(int kind, params long[] arguments) => Events.Add(new long[] { kind }.Concat(arguments).Concat(Snapshot()).ToArray());
        public long[] Snapshot()
        {
            var brain = State.Brain;
            var damage = brain.Damage;
            var body = damage.Body;
            var motion = damage.Actor.Body;
            int self = tracked ? 5 : 0;
            List<long> values =
            [
                body.State, body.Substate, damage.SlideUpdates, body.BehaviorFlags, damage.Actor.CompletionFlags, damage.ReactionFlags,
                brain.SignalFlags, damage.Field310, body.BodyFlags, brain.DeathPulseSent, pathCount,
                (brain.TugWeb.Target as Body)?.Identity ?? 0, brain.TugWeb.Validation, brain.LastWindUpQuip,
                (damage.CachedJonah as Body)?.Identity ?? 0, body.ActorPhase, tracked ? 1 : 0, self, tracked ? 1 : 0, self,
                brain.ChargeIndex, brain.ChargeAttempts, brain.BreakWallChecksum, damage.TargetKind, (State.CurrentTarget.Target as Body)?.Identity ?? 0,
                State.CurrentTarget.Validation, damage.PlayerTargetUpdates, BaddyCollisionCheck, body.CurrentWaypoint
            ];
            foreach (FixedVector3 vector in new[] { points[1], motion.Position, motion.Velocity, motion.Acceleration, motion.LocalVelocity, brain.ObstacleAxis,
                brain.ObstacleSide, brain.ChargeTarget, brain.ChargeStart, brain.PreviousPosition, State.EnvironmentAimPosition, points[2], points[3] })
                values.AddRange([vector.X, vector.Y, vector.Z]);
            foreach (ActorAngles angles in new[] { motion.AngularVelocity, motion.AngularAcceleration })
                values.AddRange([angles.X, angles.Y, angles.Z]);
            return values.ToArray();
        }
    }
}
