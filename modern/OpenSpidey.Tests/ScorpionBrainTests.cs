using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class ScorpionBrainTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ScorpionBrainTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.scorpion-brain.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var host = new Host(test);
            JsonElement expected = test.GetProperty("expected");
            int result = ScorpionBrain.Update(host.Brain, host, host.Read("elapsedTicks"), host.Random);
            long[] actual = host.Snapshot(), original = Numbers(expected.GetProperty("snapshot"));
            int mismatch = Enumerable.Range(0, Math.Min(actual.Length, original.Length)).FirstOrDefault(field => actual[field] != original[field], -1);
            string mismatchDetail = mismatch >= 0 ? $"{SnapshotField(mismatch)}({mismatch}) actual={actual[mismatch]}, original={original[mismatch]}" : $"lengths actual={actual.Length}, original={original.Length}";
            assert(result == expected.GetProperty("result").GetInt32() && actual.SequenceEqual(original),
                $"Whole PC Scorpion AI visit{index}, requested state={host.Read("state")}/substate={host.Read("substate")}/difficulty={host.Read("difficulty")}, mismatch {mismatchDetail}; canonical countdowns, physics, signals/messages/procedures, startup/slide, camera/music, target/web identity and shared trail match.");
            int eventMismatch = Enumerable.Range(0, Math.Min(host.Events.Count, expected.GetProperty("events").GetArrayLength())).FirstOrDefault(
                item => !host.Events[item].SequenceEqual(Numbers(expected.GetProperty("events")[item])), -1);
            assert(host.Events.Count == expected.GetProperty("events").GetArrayLength() && eventMismatch == -1,
                $"Whole PC Scorpion AI visit{index} callback mismatch{eventMismatch}: camera/asset/physics/pose/procedure/audio/behavior/tail/effect order and shared-state snapshots match.");
            assert(host.TailSnapshot().SequenceEqual(Numbers(expected.GetProperty("tail"))),
                $"Whole PC Scorpion AI visit{index++} preserves every control/node/history word with tail update after behavior, trail before update and deferred impact position after allocation.");
        }
    }

    private static long[] Numbers(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private static string SnapshotField(int field)
    {
        string[] fields = ["state", "substate", "behaviorFlags", "completionFlags", "reactionFlags", "signalFlags", "hitFlashTicks", "playerTargetTicks",
            "environmentTargetTicks", "gloatTicks", "fieldC1C", "voiceSuppressionTicks", "standingY", "groundY", "stateFlagCache", "tailGlowEnabled",
            "savedHorizontal", "savedVertical", "cameraDistancesSaved", "lastFootstep", "bossCameraEnabled", "tailGeometryRegion", "tailGlowRegion", "slideTicks",
            "waypoint", "clip", "frame", "waitTicks", "targetKind", "targetIdentity", "cachedJonah", "targetIsPlayer", "trapIdentity", "trapValidation", "tugIdentity", "tugValidation"];
        return field < fields.Length ? fields[field] : "motion/camera/message/procedure/trail field";
    }
    private sealed class Identity(int value) { public int Value { get; } = value; }
    private sealed class HiddenItem(int value) : IScorpionCameraHiddenItem
    {
        public object Identity => this;
        public ushort ItemFlags { get; set; } = unchecked((ushort)(0xF800 + value));
    }

    private sealed class FrameProcedure(Host host, int sourceType) : IActorProcedure
    {
        public int SourceType { get; } = sourceType;
        public int Flags { get; set; }
        public Action? ExecuteNext { get; set; }
        public void Execute(int elapsedTicks)
        {
            host.Event(7);
            host.Brain.Body.State = host.Read("procedureState");
            host.Brain.Body.Substate = host.Read("procedureSubstate");
            host.Brain.Damage.Actor.Body.Position -= new FixedVector3(0, 0, 23);
            if (host.Read("procedureRetire") != 0) Flags |= 1;
            ExecuteNext?.Invoke();
        }
    }

    private sealed class Host : IScorpionBrainHost, IScorpionBossCameraHost
    {
        private readonly JsonElement input;
        private readonly GameRandom random = new(0);
        private readonly Identity other = new(2), jonah = new(1), cachedJonah = new(2), trap = new(1), tug = new(2);
        private readonly HiddenItem[] items = Enumerable.Range(0, 8).Select(value => new HiddenItem(value)).ToArray();
        private readonly HiddenItem?[] hidden = new HiddenItem?[8];
        private readonly ThugMessage?[] messages = new ThugMessage?[4];
        private readonly CameraParameterTransition horizontal, vertical;
        private readonly FixedVector3[] trail = new FixedVector3[6];
        private ScorpionState? globalBoss;
        public ScorpionBrainState Brain { get; }
        public List<long[]> Events { get; } = [];
        public PlayerCombatState Player { get; } = new(100, 100);
        public SourceMusicState Music { get; }
        public CameraModeState Mode { get; }
        public IScorpionBossCameraHost? Camera => this;
        public int SpinRate { get; set; }
        public int StationaryRadius { get; set; }
        public Memory<FixedVector3> TailTrail => trail;
        public short PlayerFieldAa => unchecked((short)Read("playerAa"));
        public object? PlayerHeldItemIdentity
        {
            get { int held = Read("heldItem"); return held != 0 ? items[held - 1] : null; }
        }
        public int Difficulty => Read("difficulty");
        public uint PlayerFlags => Player.ActionState;
        public FixedVector3 PlayerPosition => Player.Position;
        public int Read(string name) => unchecked((int)input.GetProperty(name).GetInt64());
        private uint Unsigned(string name) => input.GetProperty(name).GetUInt32();
        private FixedVector3 Vector(string x, string y, string z) => new(Read(x), Read(y), Read(z));
        public Host(JsonElement test)
        {
            input = test.GetProperty("input");
            var body = new ScorpionState(0)
            {
                Health = unchecked((short)Read("health")), State = Read("state"), Substate = Read("substate"),
                BehaviorFlags = Unsigned("behaviorFlags"), WaitUpdates = Read("waitUpdates"),
                SurfaceClearance = unchecked((short)Read("surfaceClearance")), MinorRadius = unchecked((ushort)Read("radius")),
                BossCameraEnabled = unchecked((byte)Read("cameraEnabled")), TailGeometryRegion = unchecked((byte)Read("geometryRegion")),
                TailGlowRegion = unchecked((byte)Read("glowRegion")),
                Animation = OpenSpidey.Core.Animation.AnimationPlaybackState.Initial with
                { ClipId = unchecked((ushort)Read("clip")), Frame = unchecked((short)Read("frame")) }
            };
            var actor = new ActorProcedureState(new()
            {
                Position = Vector("px", "py", "pz"), Velocity = Vector("vx", "vy", "vz"),
                Acceleration = Vector("ax", "ay", "az"), LocalVelocity = Vector("lx", "ly", "lz"),
                AngularVelocity = new(unchecked((short)Read("avx")), unchecked((short)Read("avy")), unchecked((short)Read("avz"))),
                AngularAcceleration = new(unchecked((short)Read("aax")), unchecked((short)Read("aay")), unchecked((short)Read("aaz"))),
                LinearDamping = new(1, 1, 1), AngularDamping = new(1, 1, 1)
            }) { CompletionFlags = Unsigned("completionFlags") };
            var procedures = new ActorProcedureList();
            var damage = new ScorpionDamageState(body, actor, procedures)
            {
                ReactionContext = Read("reactionContext"), ReactionFlags = Unsigned("reactionFlags"),
                Field310 = Read("field310"), SlideUpdates = Read("slideUpdates"), HitFlashUpdates = Read("hitFlashUpdates"),
                TargetKind = Read("targetKind"), PlayerTargetUpdates = Read("targetUpdates"),
                CurrentTargetHandle = Read("targetHandle") != 0 ? other : null, CachedJonah = Read("cachedJonah") != 0 ? cachedJonah : null,
                LastFireQuip = Read("lastFireQuip"), FireQuipSuppressionUpdates = Read("quipSuppression")
            };
            Brain = new(damage, new())
            {
                SignalFlags = Unsigned("signals"), StandingY = Read("standingY"), GroundY = Read("groundY"),
                EnvironmentalTargetSuppressionUpdates = Read("environmentTimer"), GloatSuppressionUpdates = Read("gloatTimer"), FieldC1CUpdates = Read("timerC1C"),
                TailGlowEnabled = unchecked((byte)Read("glowEnabled")), SavedCameraHorizontalDistance = unchecked((ushort)Read("savedHorizontal")),
                SavedCameraVerticalDistance = unchecked((ushort)Read("savedVertical")), CameraDistancesSaved = Read("distancesSaved"),
                LastFootstepSound = Read("lastFootstep"), TrapWeb = new(Read("trapHandle") != 0 ? trap : null, 901),
                TugWeb = new(Read("tugHandle") != 0 ? tug : null, 902)
            };
            if (Read("hasProcedure") != 0) procedures.Attach(new FrameProcedure(this, Read("procedureType")));
            int messageIndex = 0;
            foreach (JsonElement value in test.GetProperty("messages").EnumerateArray())
            {
                long[] v0 = Numbers(value.GetProperty("vector0")), v1 = Numbers(value.GetProperty("vector1"));
                var message = new ThugMessage(value.GetProperty("code").GetInt32())
                {
                    Flags = value.GetProperty("flags").GetInt32(), Parameter32 = value.GetProperty("parameter").GetInt32(),
                    Vector0 = new(unchecked((int)v0[0]), unchecked((int)v0[1]), unchecked((int)v0[2])),
                    Vector1 = new(unchecked((int)v1[0]), unchecked((int)v1[1]), unchecked((int)v1[2]))
                };
                messages[messageIndex++] = message;
                Brain.Messages.Enqueue(message);
            }
            for (int index = 0; index < 4; index++) body.TailControlPoints[index] = new(100 + index, 200 + index, 300 + index);
            for (int index = 0; index < 23; index++) body.TailNodes[index] = new(10000 + index, -20000 - index, 30000 + index);
            for (int index = 0; index < 128; index++) body.TailHistory[index] = new(40000 + index, -50000 - index, 60000 + index);
            for (int index = 0; index < 6; index++) trail[index] = new(70000 + index, -80000 - index, 90000 + index);
            Player.IsCrawling = Read("crawling") != 0; Player.FieldE18 = Read("playerLock"); Player.SynthesizedInputActive = Read("synthesizedInput") != 0;
            Player.Position = Vector("playerX", "playerY", "playerZ"); Player.ActionState = Unsigned("playerFlags");
            Mode = new() { Mode = Unsigned("cameraMode"), ItemHidingEnabled = unchecked((byte)Read("cameraFlag")),
                CollisionFlag = unchecked((byte)Read("cameraCollision")) };
            SpinRate = Read("cameraSpin"); StationaryRadius = Read("cameraRadius");
            horizontal = new(Read("cameraHorizontal"), false); vertical = new(Read("cameraVertical"), false);
            for (int index = 0; index < 8; index++) hidden[index] = (Read("hiddenPattern") & (1 << index)) != 0 ? items[index] : null;
            globalBoss = Read("globalBoss") != 0 ? body : null;
            Music = new() { Phase = Read("musicPhase"), TrackClock = Read("musicClock"), TrackEnding = unchecked((byte)Read("trackEnding")) };
            random.Restore(new(Read("seed"), Read("multiplier"), Read("increment")));
        }

        public ushort GetHorizontalDistance() => unchecked((ushort)horizontal.Value);
        public short GetVerticalDistance() => unchecked((short)vertical.Value);
        public void SetHorizontalDistance(ushort distance, ushort duration) => horizontal.Set(distance, duration);
        public void SetVerticalDistance(short distance, ushort duration) => vertical.Set(distance, duration);
        public void SetMode(uint mode) { Event(1, mode); Mode.SetMode(mode); }
        public IScorpionCameraHiddenItem? GetHiddenItem(int index) => hidden[index];
        public void ClearHiddenItem(int index) => hidden[index] = null;
        public void SetGlobalBoss(ScorpionState? boss) => globalBoss = boss;
        public void PutPlayerCameraBehind(int argument) => Event(2, argument);
        public void InitializeTailGeometry() { Event(3); Brain.Body.TailGeometryRegion = 7; }
        public void InitializeTailGlow() { Event(4); Brain.Body.TailGlowRegion = 8; }
        public void DoPhysics(int limitElapsed)
        {
            Event(5, limitElapsed);
            Brain.Damage.Actor.Body.UseLocalVelocity = (Brain.Body.BehaviorFlags & 1) != 0;
            Brain.Damage.Actor.Body.Integrate(Read("elapsedTicks"), limitElapsed != 0);
        }
        public void BuildTransform()
        {
            Event(6);
            if (Read("brainMutation") == 1) { Brain.SignalFlags |= 0x4C; Brain.Damage.TargetKind = 1; }
        }
        public void PlayPositionSound(uint sound, FixedVector3 position, int pitch)
        {
            Event(8, sound, pitch);
            if (Read("brainMutation") == 2) { Brain.Body.State = 20; Brain.Body.Substate = 0; Brain.SignalFlags |= 0x801; }
        }
        public object? RecoverHandle(object? handle)
        {
            Event(9, 0);
            if (Read("targetValid") != 0) return handle;
            Brain.Damage.CurrentTargetHandle = null;
            return null;
        }
        public object? RecoverWeb(PlayerKickHandle handle)
        {
            int role = ReferenceEquals(handle.Target, trap) ? 1 : 2;
            Event(9, role);
            return Read(role == 1 ? "trapValid" : "tugValid") != 0 ? handle.Target : null;
        }
        public void BurstWeb(object web)
        {
            Event(10, ((Identity)web).Value);
            if (Read("brainMutation") == 3)
            {
                Brain.SignalFlags |= 0x801;
                if (messages[0] is { } message) message.Vector1 = new(777, 888, 999);
            }
        }
        public void RunAnimation(ushort clip, int from, int to)
        {
            Event(11, clip, from, to);
            Brain.Body.Animation = Brain.Body.Animation with { ClipId = clip, Frame = unchecked((short)from) };
            if (Read("brainMutation") == 4) { Brain.Body.Substate = 4; Brain.Damage.Actor.Body.Position += new FixedVector3(4096, 0, 0); }
        }
        public void CycleAnimation(ushort clip, sbyte direction)
        {
            Event(12, clip, direction);
            Brain.Body.Animation = Brain.Body.Animation with { ClipId = clip, Frame = 0 };
        }
        public int GroundHeight(FixedVector3 position, int above, int below, int flags)
        {
            Event(13, above, below, flags);
            if (Read("brainMutation") == 5) Brain.Body.Substate = 5;
            return Read("groundResult");
        }
        public void StepBehavior(ScorpionBehaviorRoutine routine, object? target = null)
        {
            Event(14, (int)routine, (target as Identity)?.Value ?? 0);
            if (Read("brainMutation") == 6) { Brain.SignalFlags |= 0x801; Brain.Damage.TargetKind = 2; Brain.Damage.ReactionFlags |= 4; }
        }
        public void DetermineTarget() { Event(15); Brain.Body.State = 3; Brain.Body.Substate = 42; Brain.Damage.TargetKind = 2; }
        public FixedVector3 JonahPosition(object identity) => Vector("jonahX", "jonahY", "jonahZ");
        public ushort JonahRadius(object identity) => unchecked((ushort)Read("jonahRadius"));
        public object? FindJonah() => Read("jonahAvailable") != 0 ? jonah : null;
        public void UpdateFootprint() { Event(16); if (Read("brainMutation") == 7) Brain.SignalFlags |= 0x801; }
        public void UpdateTailPath()
        {
            Event(17);
            ScorpionTailPath.Update(Brain.Body.TailControlPoints, Brain.Body.TailNodes, Brain.Body.TailHistory, ReadHook);
        }
        private FixedVector3 ReadHook(ushort part)
        {
            Event(18, part);
            return Brain.Damage.Actor.Body.Position + new FixedVector3(part * 12345, Brain.Body.Animation.Frame * 200, -part * 7777);
        }
        public void TryAttachPlayerLookAt(uint mask, int rate, int tolerance)
        {
            int accepted = Read("lookAtAccepted");
            Event(19, 64, accepted);
            if (accepted != 0) Brain.Damage.Procedures.Attach(ActorLookAtProcedure.ForBody(Brain.Damage.Actor,
                () => Read("handleAccepted") != 0 ? new ActorBodyMotion { Position = Player.Position } : null, mask, rate, tolerance));
        }
        public void TryCreateTailImpact(Func<FixedVector3> readPosition)
        {
            int accepted = Read("impactAccepted");
            Event(20, 76, accepted);
            if (Read("brainMutation") == 8) Brain.Body.TailNodes[22] = new(1234567, -2345678, 3456789);
            if (accepted != 0) { FixedVector3 position = readPosition(); Event(21, position.X, position.Y, position.Z); }
        }
        public void SourceDiagnostic(string message) => Event(22, message switch
        { "Unknown state!" => 0, "Unknown substate!" => 1, "Unknown sub-state!" => 2, _ => throw new InvalidDataException(message) });
        public void ReportUnknownRecoverySubstate(int substate) => SourceDiagnostic("Unknown substate!");
        public int Random(int bound) { int result = random.Next(bound); Event(0, bound, result); return result; }
        public object? MakePlayerHandle() => throw new InvalidOperationException("Outer AI does not invoke Hit's player retarget transport.");
        public void TrySendJonahMessage(object jonahIdentity, int code) => throw new InvalidOperationException("Outer AI consumes its own inbox; its required behavior bodies own outgoing messages.");
        public bool PlayPositionSpeech(int bank, int slot, FixedVector3 position, int priority) => throw new InvalidOperationException("Outer AI delegates voice-producing behavior bodies explicitly.");
        public void AttachPlayerSpeech(int bank, int slot) => throw new InvalidOperationException("Outer AI never performs Hit's player speech attachment.");
        public int PathCheckWithHit(FixedVector3 from, FixedVector3 to, int clearance, out FixedVector3 hit) => throw new InvalidOperationException("Outer AI delegates path-consuming behavior bodies explicitly.");

        public void Event(int kind, params long[] arguments) => Events.Add(new long[] { kind }.Concat(arguments).Concat(Snapshot()).ToArray());
        public long[] Snapshot()
        {
            var body = Brain.Body;
            var damage = Brain.Damage;
            var motion = damage.Actor.Body;
            List<long> values = [body.State, body.Substate, body.BehaviorFlags, damage.Actor.CompletionFlags, damage.ReactionFlags, Brain.SignalFlags,
                damage.HitFlashUpdates, damage.PlayerTargetUpdates, Brain.EnvironmentalTargetSuppressionUpdates, Brain.GloatSuppressionUpdates, Brain.FieldC1CUpdates,
                damage.FireQuipSuppressionUpdates, Brain.StandingY, Brain.GroundY, damage.StateFlags.CachedIndex,
                Brain.TailGlowEnabled, Brain.SavedCameraHorizontalDistance, Brain.SavedCameraVerticalDistance, Brain.CameraDistancesSaved, Brain.LastFootstepSound,
                body.BossCameraEnabled, body.TailGeometryRegion, body.TailGlowRegion, damage.SlideUpdates, body.CurrentWaypoint,
                body.Animation.ClipId, body.Animation.Frame, body.WaitUpdates, damage.TargetKind, (damage.CurrentTargetHandle as Identity)?.Value ?? 0,
                (damage.CachedJonah as Identity)?.Value ?? 0, Brain.CurrentTargetIsPlayer,
                (Brain.TrapWeb.Target as Identity)?.Value ?? 0, Brain.TrapWeb.Validation, (Brain.TugWeb.Target as Identity)?.Value ?? 0, Brain.TugWeb.Validation];
            foreach (FixedVector3 vector in new[] { motion.Position, motion.Velocity, motion.Acceleration, motion.LocalVelocity,
                Brain.PreviousPosition, Brain.ObstaclePosition, Brain.ObstacleWaypoint }) Append(values, vector);
            values.AddRange([motion.Angles.X, motion.Angles.Y, motion.Angles.Z, motion.AngularVelocity.X, motion.AngularVelocity.Y, motion.AngularVelocity.Z,
                motion.AngularAcceleration.X, motion.AngularAcceleration.Y, motion.AngularAcceleration.Z,
                motion.CollisionFlags, motion.MovementFlags, random.State.Value, random.State.Multiplier, random.State.Increment,
                Music.Phase, Music.TrackClock, Music.TrackEnding, Mode.Mode, Mode.ItemHidingEnabled, Mode.CollisionFlag, SpinRate, StationaryRadius, horizontal.Value, vertical.Value,
                ReferenceEquals(globalBoss, body) ? 1 : 0]);
            for (int index = 0; index < 8; index++) values.AddRange([hidden[index] is null ? 0 : index + 1, items[index].ItemFlags]);
            foreach (ThugMessage? message in messages) values.Add(message?.Flags ?? 0);
            uint alive = 0;
            for (ThugMessage? message = Brain.Messages.Head; message is not null; message = message.Next)
                for (int index = 0; index < messages.Length; index++) if (ReferenceEquals(message, messages[index])) alive |= 1u << index;
            values.Add(alive);
            values.Add(damage.Procedures.Count);
            foreach (IActorProcedure procedure in damage.Procedures.SourceOrder)
            {
                var lookAt = procedure as ActorLookAtProcedure;
                values.AddRange([procedure.SourceType, procedure.Flags, lookAt?.CompletionMask ?? 0, lookAt?.WaitTicks ?? 0, lookAt?.Tolerance ?? 0, lookAt?.Rate ?? 0]);
            }
            foreach (FixedVector3 point in trail) Append(values, point);
            return values.ToArray();
        }
        public long[] TailSnapshot()
        {
            List<long> values = [];
            foreach (FixedVector3 point in Brain.Body.TailControlPoints.Concat(Brain.Body.TailNodes).Concat(Brain.Body.TailHistory)) Append(values, point);
            return values.ToArray();
        }
        private static void Append(List<long> values, FixedVector3 vector) => values.AddRange([vector.X, vector.Y, vector.Z]);
    }
}
