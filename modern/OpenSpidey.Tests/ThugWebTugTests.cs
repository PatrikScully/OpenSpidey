using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugWebTugTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugWebTugTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-web-tug.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            var state = new ThugState(1, 1) { Health = (short)input[1], ActionFlags = uint.MaxValue,
                ActionCounter = 777, State = 7, Substate = 8, SurfaceClearance = 100 };
            var actor = new ActorProcedureState(new ActorBodyMotion { Position = new(123456, -54321, 76543), Velocity = new(55, 66, 77) })
                { BehaviorFlags = (uint)input[2] };
            var host = new Host(input, actor.Body.Position);
            var direction = new FixedVector3(input[10], input[11], input[12]);
            int result = input[0] != 0 ? ThugWebTugBehavior.GetLaunched(state, actor, direction, input[9] != 0, 80, host) :
                ThugWebTugBehavior.TugImpulse(state, actor, direction, input[3] != 0 ? new FixedVector3[8] : null, host) ? 1 : 0;
            long[] actual = [result, state.Health, actor.BehaviorFlags, state.ActionFlags, state.ActionCounter,
                state.State, state.Substate, actor.Body.Velocity.X, actor.Body.Velocity.Y, actor.Body.Velocity.Z, host.ThrowPointsPresent ? 1 : 0];
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(11).Select(value => value.GetInt64())),
                "Actual PC thug tug/launch preserves web bit8, state16 path ownership, health50 fatal-launch flag, path duration and wall state flags.");
            assert(host.Events.SequenceEqual(expected[11].EnumerateArray().Select(value => value.GetInt32())),
                "Actual PC thug tug/launch uses exact elevated path endpoints, short-circuited path allocation, neutralization and LookAt/audio callback order.");
        }
        var degenerate = new ThugState(1, 1) { Health = 10, SurfaceClearance = 100 };
        var body = new ActorProcedureState(new ActorBodyMotion());
        var blocked = new Host([0, 10, 0, 0, 0, 2, 1, 1, 0, 0, 0, 0, 0], default);
        assert(!ThugWebTugBehavior.TugImpulse(degenerate, body, default, null, blocked) && degenerate.ActionCounter == 0,
            "A blocked zero-horizontal-speed tug safely rejects the original divide-by-zero crash.");
    }
    private sealed class Host(int[] input, FixedVector3 position) : IThugWebTugHost
    {
        private int additions;
        public List<int> Events { get; } = [];
        public bool ThrowPointsPresent { get; private set; } = input[4] != 0;
        public int PathCheckWithHit(FixedVector3 from, FixedVector3 to, int clearance, out FixedVector3 hit)
        {
            Events.AddRange([1, from.X, from.Y, from.Z, to.X, to.Y, to.Z, clearance]);
            hit = position + new FixedVector3(input[6], 0, input[7]); return input[5];
        }
        public bool AddPointToPath(FixedVector3 point, int argument)
        { Events.AddRange([2, point.X, point.Y, point.Z, argument]); return ++additions != input[8]; }
        public void Neutralize() => Events.Add(3);
        public void LookAtPoint(FixedVector3 point, uint completionMask, int rate, int tolerance) =>
            Events.AddRange([4, point.X, point.Y, point.Z, (int)completionMask, rate, tolerance]);
        public void ReplaceThrowPoints(IReadOnlyList<FixedVector3> points)
        { if (ThrowPointsPresent) Events.Add(6); ThrowPointsPresent = true; }
        public void PlayPositionSound(ushort sound, FixedVector3 point, int pitchOffset) =>
            Events.AddRange([5, sound, point.X, point.Y, point.Z, pitchOffset]);
    }
}
