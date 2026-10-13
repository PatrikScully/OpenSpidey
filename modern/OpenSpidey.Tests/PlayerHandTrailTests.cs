using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerHandTrailTests
{
    private sealed record Trail(int Identity);
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerHandTrailTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-hand-trails.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            long[] input = row.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
            var state = new PlayerHandTrailState { Colour = (uint)input[4] };
            state.Current[0] = input[2] != 0 ? new Trail(1) : null;
            state.Current[1] = input[3] != 0 ? new Trail(2) : null;
            state.PreviousOffsets[0] = Vector(14); state.PreviousOffsets[1] = Vector(17);
            var host = new Host(Vector(8), Vector(11));
            PlayerHandTrails.Update(state, host, Vector(5), (ushort)input[0], (short)input[1]);
            long[] actual = [(state.Current[0] as Trail)?.Identity ?? 0, (state.Current[1] as Trail)?.Identity ?? 0,
                state.PreviousOffsets[0].X, state.PreviousOffsets[0].Y, state.PreviousOffsets[0].Z,
                state.PreviousOffsets[1].X, state.PreviousOffsets[1].Y, state.PreviousOffsets[1].Z];
            JsonElement expected = row.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64())),
                $"Original hand trails{index}: source104/106 signed animation/frame gates, two owners and exact previous offsets.");
            assert(host.Events.SequenceEqual(expected[actual.Length].EnumerateArray().Select(value => value.GetInt64())),
                $"Original hand trails{index++}: hook1 before0, real six-ribbon constructors/destructors and unpatched source midpoint projection before final endpoints.");
        }
    }
    private sealed class Host(FixedVector3 left, FixedVector3 right) : IPlayerHandTrailHost
    {
        private int next = 3;
        public List<long> Events { get; } = [];
        public FixedVector3 HookPosition(int hook)
        {
            FixedVector3 position = hook == 1 ? left : right;
            Events.AddRange([1, hook, position.X, position.Y, position.Z]); return position;
        }
        public object CreateTrail(FixedVector3 position, int count, int red, int green, int blue)
        {
            var trail = new Trail(next++);
            Events.AddRange([5, 88, 2, trail.Identity, position.X, position.Y, position.Z, count, red, green, blue]);
            return trail;
        }
        public void DeleteTrail(object trail) => Events.AddRange([4, ((Trail)trail).Identity, 1]);
        public void SetTrailPosition(object trail, FixedVector3 position) =>
            Events.AddRange([3, ((Trail)trail).Identity, position.X, position.Y, position.Z]);
    }
}
