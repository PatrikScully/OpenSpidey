using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

internal static class ActorPathControllerTests
{
    private sealed class Host(int pattern, int factor, int groundFail, FixedVector3 waypoint) : IActorPathHost
    {
        private int losCalls, groundCalls;
        public int CollisionCheckMode { get; set; }
        public bool ThrowOnRay { get; set; }
        public List<long[]> Rays { get; } = [];
        public List<long[]> Ground { get; } = [];
        public bool LineOfSight(FixedVector3 from, FixedVector3 to, int flags, ref FixedVector3 hit)
        {
            if (ThrowOnRay) throw new InvalidOperationException("A real world query failed.");
            bool clear = (pattern & (1 << (losCalls++ & 31))) == 0;
            if (!clear) hit = from + ((to - from) >> 2) * factor;
            Rays.Add([from.X, from.Y, from.Z, to.X, to.Y, to.Z, flags, CollisionCheckMode,
                clear ? 1 : 0, hit.X, hit.Y, hit.Z]);
            return clear;
        }
        public int GroundHeight(FixedVector3 position, int above, int below)
        {
            Ground.Add([position.X, position.Y, position.Z, above, below, CollisionCheckMode]);
            return groundCalls++ == groundFail ? -1 : 1234;
        }
        public FixedVector3 WaypointPosition(int node) => waypoint;
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorPathControllerTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-path.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var actor = new ActorProcedureState(new ActorBodyMotion { Position = Vector(15) }) { BehaviorFlags = (uint)input[1] };
            var host = new Host((int)input[8], (int)input[9], (int)input[10], Vector(27)) { CollisionCheckMode = (int)input[7] };
            var path = new ActorPathController(actor, host)
            {
                Radius = (ushort)input[2], Clearance = (short)input[3], StoredBodyY = (int)input[4],
                CurrentWaypoint = (int)input[5], PathCount = (int)input[6]
            };
            for (int index = 0; index < 6; index++) path.SetPoint(index, Vector(30 + index * 3));
            int result = 0;
            FixedVector3 hit = default;
            if (input[0] == 0)
            {
                ActorPathCheckResult check = path.PathCheck(Vector(18), Vector(21), input[14] != 0, (int)input[11], Vector(24));
                result = check.Code;
                hit = check.HitPoint;
            }
            else if (input[0] == 1) result = path.AddPointToPath(Vector(21), (int)input[11]) ? 1 : 0;
            else hit = ActorPathController.GetWaypointNearTarget(Vector(21), (int)input[13], (int)input[12]);
            JsonElement expected = test.GetProperty("expected");
            long[] actual = [result, hit.X, hit.Y, hit.Z, host.CollisionCheckMode, actor.BehaviorFlags, path.PathCount];
            long[] expectedState = [expected[0].GetInt64(), expected[1][0].GetInt64(), expected[1][1].GetInt64(), expected[1][2].GetInt64(),
                expected[2].GetInt64(), expected[3].GetInt64(), expected[4].GetInt64()];
            assert(actual.SequenceEqual(expectedState), "Enemy path result, hit interpolation, global restoration, point count and behavior flags match source.");
            assert(Enumerable.Range(0, 6).All(index => path.GetPoint(index) == new FixedVector3(
                expected[5][index][0].GetInt32(), expected[5][index][1].GetInt32(), expected[5][index][2].GetInt32())),
                "Source point insertion and truncation preserve all six bounded path slots.");
            assert(TraceMatches(host.Rays, expected[6]), "Capsule center and side rays preserve exact source endpoints, order, flags and hit outputs.");
            assert(TraceMatches(host.Ground, expected[7]), "Ground-gap sampling preserves source subdivisions, sample height and collision mode.");
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }

        var failedActor = new ActorProcedureState(new ActorBodyMotion()) { BehaviorFlags = 0x2000 };
        var failedHost = new Host(0, 2, -1, default) { CollisionCheckMode = 9, ThrowOnRay = true };
        var failedPath = new ActorPathController(failedActor, failedHost) { Radius = 150 };
        try
        {
            failedPath.PathCheck(default, new(409600, 0, 0), true, 55);
            assert(false, "A collision host failure must propagate.");
        }
        catch (InvalidOperationException)
        {
            assert(failedHost.CollisionCheckMode == 9, "Collision mode is restored if the real host throws.");
        }
        Reject(() => failedPath.PathCount = 6);
        Reject(() => failedPath.GetPoint(-1));
        Reject(() => failedPath.SetPoint(6, default));
        void Reject(Action action)
        {
            try { action(); assert(false, "An invalid path slot must be rejected."); }
            catch (ArgumentOutOfRangeException) { assert(true, "Managed path bounds reject an invalid source slot."); }
        }
    }

    private static bool TraceMatches(List<long[]> actual, JsonElement expected) => actual.Count == expected.GetArrayLength() &&
        actual.Zip(expected.EnumerateArray()).All(pair => pair.First.SequenceEqual(pair.Second.EnumerateArray().Select(value => value.GetInt64())));
}
