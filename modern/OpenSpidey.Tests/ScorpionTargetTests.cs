using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class ScorpionTargetTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ScorpionTargetTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.scorpion-targets.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var host = new Host(test);
            JsonElement expected = test.GetProperty("expected");
            int result = 0;
            switch (host.Read("method"))
            {
                case 0: ScorpionTargetSelection.DetermineTarget(host.State, host, host.Random); break;
                case 1: ScorpionTargetSelection.Intro(host.State, host, host.Read("elapsed")); break;
                case 2: result = ScorpionTargetSelection.SelectEnvironment(host.State, host, host.Random) ? 1 : 0; break;
                default: throw new InvalidDataException("Unexpected target oracle method.");
            }
            long[] actual = host.Snapshot(), original = Numbers(expected.GetProperty("snapshot"));
            int mismatch = Enumerable.Range(0, Math.Min(actual.Length, original.Length)).FirstOrDefault(field => actual[field] != original[field], -1);
            string detail = mismatch < 0 ? $"lengths{actual.Length}/{original.Length}" : $"field{mismatch}: actual{actual[mismatch]}, original{original[mismatch]}";
            assert(result == expected.GetProperty("result").GetInt32() && actual.SequenceEqual(original),
                $"Whole PC target case{index}, method{host.Read("method")}/state{host.Read("state")}/Jonah{host.Read("jonahState")}: {detail}; target pointer/validation, source decisions,600-tick intro, local hiding handle and original aim arithmetic match.");
            int eventMismatch = Enumerable.Range(0, Math.Min(host.Events.Count, expected.GetProperty("events").GetArrayLength())).FirstOrDefault(
                item => !host.Events[item].SequenceEqual(Numbers(expected.GetProperty("events")[item])), -1);
            int originalEvents = expected.GetProperty("events").GetArrayLength();
            string extra = host.Events.Count > originalEvents ? $"extra actual kind{host.Events[originalEvents][0]}"
                : host.Events.Count < originalEvents ? $"missing original kind{expected.GetProperty("events")[host.Events.Count][0]}" : "";
            assert(host.Events.Count == originalEvents && eventMismatch == -1,
                $"Whole PC target case{index++}, callback{eventMismatch} of{host.Events.Count}/{originalEvents} {extra}: original module/handle/visibility/path/LookAt/RNG order and live canonical/current versus copied-Jonah handle boundaries match.");
        }
    }

    private static long[] Numbers(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private sealed class Body(int identity, ushort type, FixedVector3 position)
    {
        public int Identity { get; } = identity;
        public ushort Type { get; } = type;
        public FixedVector3 Position { get; set; } = position;
        public ActorBodyMotion Motion { get; } = new() { Position = position };
    }
    private sealed class Host : IScorpionTargetHost
    {
        private readonly JsonElement input;
        private readonly JsonElement[] queries;
        private readonly Body player = new(1, 0, default), jonah = new(2, 316, default);
        private readonly Body[] bodies;
        private readonly GameRandom random = new(0);
        private readonly JonahState jonahState;
        private PlayerKickHandle hiding;
        private bool jonahAvailable;
        private int recoverCalls;
        public ScorpionTargetState State { get; }
        public List<long[]> Events { get; } = [];
        public int Read(string name) => unchecked((int)input.GetProperty(name).GetInt64());
        private uint Unsigned(string name) => input.GetProperty(name).GetUInt32();
        public object PlayerIdentity => player;
        public int Difficulty => 0;
        public uint PlayerFlags => 0;
        public FixedVector3 PlayerPosition => player.Position;
        public object? FirstEnvironment => bodies[0];

        public Host(JsonElement test)
        {
            input = test.GetProperty("input");
            queries = test.GetProperty("bodies").EnumerateArray().ToArray();
            bodies = queries.Select((query, index) =>
            {
                long[] position = Numbers(query.GetProperty("position"));
                return new Body(index + 3, query.GetProperty("type").GetUInt16(),
                    new(unchecked((int)position[0]), unchecked((int)position[1]), unchecked((int)position[2])));
            }).ToArray();
            var body = new ScorpionState(0)
            { State = Read("state"), Substate = Read("substate"), BehaviorFlags = Unsigned("behavior"), SurfaceClearance = 100 };
            var actor = new ActorProcedureState(new() { Position = new(Read("px"), Read("py"), Read("pz")) })
            { CompletionFlags = Unsigned("completion") };
            var damage = new ScorpionDamageState(body, actor, new())
            {
                PlayerTargetUpdates = Read("playerTicks"), TargetKind = Read("targetKind"),
                CachedJonah = Read("cachedJonah") != 0 ? jonah : null, SlideUpdates = Read("slideTicks")
            };
            State = new(new(damage, new())
            { EnvironmentalTargetSuppressionUpdates = Read("environmentTicks"), SignalFlags = Unsigned("signals") })
            { EnvironmentAimPosition = new(Read("aimX"), Read("aimY"), Read("aimZ")) };
            State.CurrentTarget = new(FindBody(Read("currentPointer")), Unsigned("currentValidation"));
            jonahAvailable = Read("hasJonah") != 0;
            jonahState = new(0) { State = Read("jonahState") };
            hiding = new(Read("hidingPointer") != 0 ? bodies[0] : null, Unsigned("hidingValidation"));
            random.Restore(new(Read("seed"), Read("multiplier"), Read("increment")));
        }

        private Body? FindBody(int identity) => identity switch
        { 1 => player, 2 => jonah, >= 3 => bodies[identity - 3], _ => null };
        public object? FindJonah() => jonahAvailable ? jonah : null;
        public uint CallJonahDecisionModule(object identity)
        {
            Event(4);
            uint result = JonahState.WhatShouldScorpionDo(jonahState.State);
            if (Read("mutation") == 2) { State.Brain.Damage.CachedJonah = null; jonahAvailable = false; }
            else if (Read("mutation") == 3) State.Brain.Damage.PlayerTargetUpdates = 900;
            return result;
        }
        public PlayerKickHandle JonahHidingPlace(object identity) => hiding;
        public PlayerKickHandle MakeHandle(object identity)
        {
            var body = (Body)identity;
            Event(3, body.Identity);
            if (Read("mutation") == 1)
            {
                foreach (Body target in new[] { body, bodies[0] })
                {
                    target.Position += new FixedVector3(4096, -8192, 12288);
                    target.Motion.Position = target.Position;
                }
            }
            return Read("makeAccepted") != 0 ? new(body, unchecked((uint)(100 + body.Identity))) : default;
        }
        public object? RecoverHandle(ref PlayerKickHandle handle)
        {
            int role = Read("method") == 0 && Read("currentPointer") != 0 && recoverCalls == 0 ? 0 : 1;
            recoverCalls++;
            Event(1, role, (handle.Target as Body)?.Identity ?? 0, handle.Validation);
            bool valid = Read(role == 0 ? "currentValid" : "hidingValid") != 0;
            if (!valid) handle = handle with { Target = null };
            Event(2, role, (handle.Target as Body)?.Identity ?? 0, handle.Validation);
            return valid ? handle.Target : null;
        }
        public object? NextEnvironment(object identity)
        {
            int index = ((Body)identity).Identity - 3;
            return index + 1 < bodies.Length ? bodies[index + 1] : null;
        }
        public ushort BodyType(object identity) => ((Body)identity).Type;
        public FixedVector3 BodyPosition(object identity) => ((Body)identity).Position;
        public bool LineOfSight(FixedVector3 start, FixedVector3 end, int flags, int argument)
        {
            int index = Array.FindIndex(bodies, value => value.Position == end);
            if (index < 0 || flags != 0 || argument != 1) throw new InvalidDataException("Unexpected source visibility query.");
            Event(7, index + 3);
            return queries[index].GetProperty("visible").GetInt32() != 0;
        }
        public int PathCheckWithHit(FixedVector3 start, FixedVector3 end, int clearance, out FixedVector3 hit)
        {
            int index = Array.FindIndex(bodies, value => value.Position.X == end.X && value.Position.Z == end.Z);
            if (index < 0) throw new InvalidDataException("Unexpected source environmental path point.");
            Event(8, index + 3, start.X, start.Y, start.Z, end.X, end.Y, end.Z, clearance);
            long[] contact = Numbers(queries[index].GetProperty("contact"));
            hit = new(unchecked((int)contact[0]), unchecked((int)contact[1]), unchecked((int)contact[2]));
            return queries[index].GetProperty("path").GetInt32();
        }
        public void TryAttachTargetLookAt(object? target, uint completionMask, int rate, int tolerance)
        {
            int accepted = Read("lookAtAccepted");
            Event(5, 64, accepted);
            if (accepted == 0) return;
            var body = target as Body ?? throw new InvalidOperationException("The source accepted a null-body LookAt with a null point.");
            Event(6, body.Identity, completionMask, rate, tolerance);
            State.Brain.Damage.Procedures.Attach(ActorLookAtProcedure.ForBody(State.Brain.Damage.Actor,
                () => Read("makeAccepted") != 0 ? body.Motion : null, completionMask, rate, tolerance));
        }
        public void SourceDiagnostic(string message)
        { if (message != "Unknown substate!") throw new InvalidDataException(message); Event(9); }
        public int Random(int bound) { int result = random.Next(bound); Event(0, bound, result); return result; }
        public object? MakePlayerHandle() => throw new InvalidOperationException("Target selection uses the typed pointer/validation MakeHandle service.");
        public void TrySendJonahMessage(object identity, int code) => throw new InvalidOperationException("These source target methods send no CMessage.");
        public bool PlayPositionSpeech(int bank, int slot, FixedVector3 position, int priority) => throw new InvalidOperationException("These source target methods start no speech.");
        public void AttachPlayerSpeech(int bank, int slot) => throw new InvalidOperationException("These source target methods attach no player speech.");
        public void PlayPositionSound(uint sound, FixedVector3 position, int pitch) => throw new InvalidOperationException("These source target methods play no positional sound.");
        public void RunAnimation(ushort clip, int from, int to) => throw new InvalidOperationException("These source target methods select no animation.");
        public void TryAttachPlayerLookAt(uint mask, int rate, int tolerance) => throw new InvalidOperationException("Intro attaches to the actual current raw target.");
        public void ReportUnknownRecoverySubstate(int substate) => throw new InvalidOperationException("This slice never dispatches damage recovery.");
        private void Event(int kind, params long[] arguments) => Events.Add(new long[] { kind }.Concat(arguments).Concat(Snapshot()).ToArray());
        public long[] Snapshot()
        {
            var brain = State.Brain;
            var damage = brain.Damage;
            PlayerKickHandle current = State.CurrentTarget;
            List<long> values = [brain.Body.State, brain.Body.Substate, damage.PlayerTargetUpdates, brain.EnvironmentalTargetSuppressionUpdates, damage.TargetKind,
                (current.Target as Body)?.Identity ?? 0, current.Validation, damage.CachedJonah is null ? 0 : 2,
                brain.SignalFlags, damage.Actor.CompletionFlags, brain.Body.BehaviorFlags, damage.SlideUpdates, damage.StateFlags.CachedIndex,
                (hiding.Target as Body)?.Identity ?? 0, hiding.Validation, jonahState.State, jonahAvailable ? 1 : 0,
                random.State.Value, random.State.Multiplier, random.State.Increment];
            Append(values, damage.Actor.Body.Position);
            Append(values, State.EnvironmentAimPosition);
            foreach (Body body in bodies) Append(values, body.Position);
            values.Add(damage.Procedures.Count);
            foreach (IActorProcedure procedure in damage.Procedures.SourceOrder)
            {
                var lookAt = procedure as ActorLookAtProcedure;
                values.AddRange([procedure.SourceType, procedure.Flags, lookAt?.CompletionMask ?? 0, lookAt?.WaitTicks ?? 0,
                    lookAt?.Tolerance ?? 0, lookAt?.Rate ?? 0]);
            }
            return values.ToArray();
        }
        private static void Append(List<long> values, FixedVector3 vector) => values.AddRange([vector.X, vector.Y, vector.Z]);
    }
}
