using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class ScorpionIdleTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ScorpionIdleTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.scorpion-idle.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var host = new Host(test);
            JsonElement expected = test.GetProperty("expected");
            ScorpionIdle.Step(host.State, host);
            long[] actual = host.Snapshot(), original = Numbers(expected.GetProperty("snapshot"));
            int mismatch = Enumerable.Range(0, Math.Min(actual.Length, original.Length)).FirstOrDefault(field => actual[field] != original[field], -1);
            assert(actual.SequenceEqual(original),
                $"Whole PC idle case{index}, substate{host.Read("substate")}: field{mismatch}; source state, 0x1F8 counter, 0x21D phase, path point1 and count match.");
            JsonElement events = expected.GetProperty("events");
            int eventMismatch = Enumerable.Range(0, Math.Min(host.Events.Count, events.GetArrayLength())).FirstOrDefault(
                item => !host.Events[item].SequenceEqual(Numbers(events[item])), -1);
            assert(host.Events.Count == events.GetArrayLength() && eventMismatch == -1,
                $"Whole PC idle case{index++}, callback{eventMismatch} of{host.Events.Count}/{events.GetArrayLength()}: DetermineTarget, visibility, ScorpPathCheck, CycleAnim and diagnostic order match.");
        }
    }

    private static long[] Numbers(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private sealed class Body(int identity, FixedVector3 position)
    {
        public int Identity { get; } = identity;
        public FixedVector3 Position { get; } = position;
    }
    private sealed class Host : IScorpionIdleHost
    {
        private readonly JsonElement input;
        private readonly long[][] paths;
        private readonly Body player, jonah, body;
        private FixedVector3 point;
        private int pathCount, pathCalls;
        public ScorpionTargetState State { get; }
        public List<long[]> Events { get; } = [];
        public int Read(string name) => unchecked((int)input.GetProperty(name).GetInt64());
        private uint Unsigned(string name) => unchecked((uint)input.GetProperty(name).GetInt64());
        private FixedVector3 Vector(string prefix) => new(Read(prefix + "X"), Read(prefix + "Y"), Read(prefix + "Z"));

        public Host(JsonElement test)
        {
            input = test.GetProperty("input");
            paths = test.GetProperty("paths").EnumerateArray().Select(Numbers).ToArray();
            player = new(1, Vector("player"));
            jonah = new(2, Vector("jonah"));
            body = new(3, Vector("body"));
            var scorpion = new ScorpionState(0)
            {
                State = 2, Substate = Read("substate"), BehaviorFlags = Unsigned("behavior"), SurfaceClearance = 100,
                MinorRadius = unchecked((ushort)Read("radius")), ActorPhase = unchecked((byte)Read("phase"))
            };
            var actor = new ActorProcedureState(new() { Position = new(Read("px"), Read("py"), Read("pz")) });
            var damage = new ScorpionDamageState(scorpion, actor, new())
            {
                TargetKind = Read("targetKind"), CachedJonah = Read("cachedJonah") != 0 ? jonah : null,
                SlideUpdates = Read("counter"), ReactionFlags = Unsigned("reaction")
            };
            State = new(new(damage, new()) { StandingY = Read("standingY") });
            State.CurrentTarget = new(FindBody(Read("currentPointer")), 777);
        }

        private Body? FindBody(int identity) => identity switch { 1 => player, 2 => jonah, 3 => body, _ => null };
        public bool PlayerIsCrawling => Read("crawling") != 0;
        public object PlayerIdentity => player;
        public FixedVector3 PlayerPosition => player.Position;
        public object? FindJonah() => Read("hasJonah") != 0 ? jonah : null;
        public FixedVector3 JonahPosition(object identity) => ((Body)identity).Position;
        public ushort JonahRadius(object identity) => unchecked((ushort)Read("jonahRadius"));
        public object? RecoverHandle(ref PlayerKickHandle handle)
        {
            if (Read("currentValid") != 0) return handle.Target;
            handle = handle with { Target = null };
            return null;
        }
        public FixedVector3 BodyPosition(object identity) => ((Body)identity).Position;
        public void DetermineTarget() => Event(1);
        public bool LineOfSight(FixedVector3 start, FixedVector3 end, int flags, int argument)
        {
            if (start != State.Brain.Damage.Actor.Body.Position || flags != 0) throw new InvalidDataException("Unexpected source visibility query.");
            Event(2, argument, end.X, end.Y, end.Z);
            return Read(argument != 0 ? "losTarget" : "losPlayer") != 0;
        }
        public int PathCheckWithHit(FixedVector3 start, FixedVector3 end, int clearance, out FixedVector3 hit)
        {
            Event(3, start.X, start.Y, start.Z, end.X, end.Y, end.Z, clearance);
            long[] entry = paths[pathCalls++];
            hit = new(unchecked((int)entry[1]), unchecked((int)entry[2]), unchecked((int)entry[3]));
            return unchecked((int)entry[0]);
        }
        public void CycleAnimation(ushort clip, sbyte direction) => Event(4, clip, direction);
        public void SourceDiagnostic(string message) => Event(5, message switch
        {
            "Unknown substate." => 0, "Stuck goddammit." => 1, "Unknown method." => 2,
            _ => throw new InvalidDataException(message)
        });
        public void StorePathDestination(FixedVector3 destination) { point = destination; pathCount = 1; }

        public uint CallJonahDecisionModule(object identity) => throw new InvalidOperationException("Idle calls no Jonah module.");
        public PlayerKickHandle JonahHidingPlace(object identity) => throw new InvalidOperationException("Idle reads no hiding place.");
        public PlayerKickHandle MakeHandle(object identity) => throw new InvalidOperationException("Idle makes no handle.");
        public object? FirstEnvironment => throw new InvalidOperationException("Idle walks no environment list.");
        public object? NextEnvironment(object identity) => throw new InvalidOperationException("Idle walks no environment list.");
        public ushort BodyType(object identity) => throw new InvalidOperationException("Idle reads no body type.");
        public void TryAttachTargetLookAt(object? target, uint completionMask, int rate, int tolerance) => throw new InvalidOperationException("Idle attaches no LookAt.");
        public int Difficulty => throw new InvalidOperationException("Idle reads no difficulty.");
        public uint PlayerFlags => throw new InvalidOperationException("Idle reads no player flags.");
        public object? MakePlayerHandle() => throw new InvalidOperationException("Idle makes no player handle.");
        public void TrySendJonahMessage(object identity, int code) => throw new InvalidOperationException("Idle sends no message.");
        public bool PlayPositionSpeech(int bank, int slot, FixedVector3 position, int priority) => throw new InvalidOperationException("Idle starts no speech.");
        public void AttachPlayerSpeech(int bank, int slot) => throw new InvalidOperationException("Idle attaches no speech.");
        public void PlayPositionSound(uint sound, FixedVector3 position, int pitch) => throw new InvalidOperationException("Idle plays no sound.");
        public void RunAnimation(ushort clip, int from, int to) => throw new InvalidOperationException("Idle uses CycleAnim only.");
        public void TryAttachPlayerLookAt(uint mask, int rate, int tolerance) => throw new InvalidOperationException("Idle attaches no LookAt.");
        public void ReportUnknownRecoverySubstate(int substate) => throw new InvalidOperationException("Idle never dispatches recovery.");

        private void Event(int kind, params long[] arguments) => Events.Add(new long[] { kind }.Concat(arguments).Concat(Snapshot()).ToArray());
        public long[] Snapshot()
        {
            var damage = State.Brain.Damage;
            PlayerKickHandle current = State.CurrentTarget;
            FixedVector3 position = damage.Actor.Body.Position;
            return [damage.Body.State, damage.Body.Substate, damage.TargetKind, (current.Target as Body)?.Identity ?? 0, current.Validation,
                (damage.CachedJonah as Body)?.Identity ?? 0, damage.SlideUpdates, damage.Body.ActorPhase, damage.ReactionFlags,
                damage.Body.BehaviorFlags, pathCount, point.X, point.Y, point.Z, position.X, position.Y, position.Z];
        }
    }
}
