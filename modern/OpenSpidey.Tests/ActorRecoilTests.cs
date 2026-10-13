using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ActorRecoilTests
{
    private sealed class Host : IActorRecoilCollisionHost, IThugFallBackHost
    {
        public IReadOnlyList<ActorCollisionBody> SourcePlayerBodies { get; set; } = [];
        public IReadOnlyList<ActorCollisionBody> SourceBaddyBodies { get; set; } = [];
        public bool IgnoreSphereRadius { get; set; }
        public bool LineOfSightCheck { get; set; }
        public ActorCollisionBody? Environment;
        public int[] EnvironmentHits = [];
        public int EnvironmentIndex, PathResult;
        public List<int> Events { get; } = [];
        public object[] Identities = [];
        public ActorCollisionBody? TraceEnvironmental(FixedVector3 start, FixedVector3 end)
        {
            Events.AddRange([3, start.X, start.Y, start.Z, end.X, end.Y, end.Z, LineOfSightCheck ? 1 : 0]);
            return EnvironmentHits[EnvironmentIndex++] != 0 ? Environment : null;
        }
        public void ApplyElasticHit(ActorCollisionBody target, ActorElasticHit hit)
        {
            var sourceHit = hit.ToActorHit();
            if (sourceHit.Damage != 0 || sourceHit.PartIndex != 0) throw new InvalidOperationException("Elastic collision enabled uninitialized source hit fields.");
            Events.AddRange([7, Array.IndexOf(Identities, target.Identity), sourceHit.Flags, sourceHit.AttackKind,
                sourceHit.Direction.X, sourceHit.Direction.Y, sourceHit.Direction.Z, sourceHit.SlideDistance, sourceHit.SlideFrames]);
        }
        public ActorPathCheckResult PathCheck(FixedVector3 start, FixedVector3 end, bool wantHitPoint, int groundRange)
        {
            Events.AddRange([8, start.X, start.Y, start.Z, end.X, end.Y, end.Z, wantHitPoint ? 1 : 0, groundRange]);
            return new(PathResult, default);
        }
    }
    private sealed class Tail : IActorProcedure
    {
        public int SourceType => 0x40005;
        public int Flags { get; set; }
        public Action? ExecuteNext { get; set; }
        public int Calls;
        public void Execute(int elapsedTicks) { Calls++; ExecuteNext?.Invoke(); }
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorRecoilTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-recoil.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            var host = new Host { Identities = Enumerable.Range(0, 6).Select(_ => new object()).ToArray() };
            if (input[0] == 0)
            {
                ActorCollisionBody[] bodies = Enumerable.Range(0, 6).Select(i => new ActorCollisionBody(host.Identities[i], (int)input[10 + i * 7 + 6],
                    Vector(10 + i * 7), (ushort)input[10 + i * 7 + 3], (uint)input[10 + i * 7 + 4], (short)input[10 + i * 7 + 5])).ToArray();
                host.SourcePlayerBodies = [bodies[1], bodies[2]];
                host.SourceBaddyBodies = [bodies[0], bodies[3], bodies[4]];
                host.Environment = bodies[5]; host.IgnoreSphereRadius = input[3] != 0; host.LineOfSightCheck = input[4] != 0;
                host.EnvironmentHits = [(int)input[5], (int)input[6]];
                ActorCollisionBody? result = ActorRecoilCollision.StruckGameObject(host.Identities[0], Vector(7), bodies[0].Position, input[1] != 0, input[2] != 0, host);
                int id = result is null ? -1 : Array.IndexOf(host.Identities, result.Identity);
                assert(id == expected[0].GetInt32() && host.LineOfSightCheck == (expected[1].GetInt32() != 0) &&
                    host.Events.SequenceEqual(expected[2].EnumerateArray().Select(value => value.GetInt32())),
                    $"StruckGameObject{index} preserves source player/baddy priority, self exclusion, nearest sphere, each-list environment fallback and LOS flag retention/reset.");
            }
            else if (input[0] == 1)
            {
                var source = new ActorCollisionBody(host.Identities[0], 304, Vector(1), 96, 0, 90);
                var target = new ActorCollisionBody(host.Identities[1], 312, Vector(4), 150, 0, (short)input[7]);
                ActorRecoilCollision.ElasticCollision(source, target, Vector(8), (int)input[11], host);
                assert(host.Events.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt32())),
                    $"ElasticCollision{index} preserves original PC health gate, velocity-biased horizontal normalized direction, signed slide distance and unsigned frame narrowing.");
            }
            else if (input[0] == 2)
            {
                var actor = new ActorProcedureState(new() { Position = Vector(1), Angles = new(0, (short)input[5], 0) }) { BehaviorFlags = unchecked((uint)input[4]) };
                var procedures = new ActorProcedureList(); host.PathResult = (int)input[6];
                ThugFallBackBehavior.Check(actor, procedures, host);
                if (procedures.Count != 0) host.Events.AddRange([9, 2047, 4, 0]);
                assert(host.Events.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt32())) && procedures.Count == (input[6] == 2 ? 1 : 0),
                    $"FallBack{index} preserves actual75-unit yaw probe, behavior reverse bit, source PathCheck false/55 and only-ground-gap rotation attachment.");
                if (procedures.Count != 0)
                {
                    for (int tick = 1; tick <= 4; tick++) procedures.Execute(2);
                    assert(actor.Body.Angles.Y == unchecked((short)((short)input[5] + 2044)) && procedures.Count == 0,
                        "Actual fall-back attaches source RotY2047/4, truncates to511 per invocation and retires after four calls.");
                }
            }
            else
            {
                var actor = new ActorProcedureState(new() { Angles = new(0, (short)input[1], 0), MovementFlags = (uint)input[7] }) { CompletionFlags = (uint)input[6] };
                var rotation = new ActorYawRotationProcedure(actor, (int)input[3], (int)input[2], (uint)input[5]) { Flags = (int)input[4] };
                int next = 0; rotation.ExecuteNext = () => next++;
                foreach (JsonElement frame in expected.EnumerateArray())
                {
                    rotation.Execute(99);
                    long[] actual = [actor.Body.Angles.Y, actor.Body.MovementFlags, rotation.RemainingFrames, actor.CompletionFlags, rotation.CompletionMask, rotation.Flags];
                    assert(actual.SequenceEqual(frame.EnumerateArray().Select(value => value.GetInt64())) && next == 0,
                        "RotY preserves original PC signed yaw wrap, no Wait, invocation countdown, movement flags, completion retention and no next traversal even when premarked.");
                }
            }
            index++;
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }
        var state = new ActorProcedureState(new()); var list = new ActorProcedureList(); var tail = new Tail();
        list.Attach(tail); list.Attach(new ActorYawRotationProcedure(state, 2047, 4, 0));
        for (int i = 0; i < 4; i++) list.Execute(2);
        assert(tail.Calls == 0 && list.Count == 1, "Original RotY blocks following source procedures through its retirement invocation.");
        list.Execute(2);
        assert(tail.Calls == 1, "Following source procedures resume after RotY is removed.");
        bool rejected = false;
        try { _ = new ActorYawRotationProcedure(state, 2047, 0, 0); } catch (ArgumentOutOfRangeException) { rejected = true; }
        assert(rejected, "Managed RotY rejects the source illegal zero divisor before attaching anything.");
    }
}
