using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerStartOrientationTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerStartOrientationTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-start-orientation.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            short[] matrix = Values(row, "matrix").Select(value => (short)value).ToArray();
            FixedVector3 normal = Vector(Values(row, "normal")), cache = Vector(Values(row, "cache"));
            int delta = row.GetProperty("delta").GetInt32();
            int[] angles = Values(row, "angles");
            var orientation = new PlayerOrientation(0);
            orientation.Restore(new(Basis(matrix), cache, true));
            int stands = 0, frame = 0;
            foreach (JsonElement expected in row.GetProperty("expected").EnumerateArray())
            {
                PlayerStartOrientationResult result = orientation.SetStartOrientation((short)angles[1], normal, delta);
                if (result.StandRequired) stands++;
                var actual = new List<int>(34);
                actual.AddRange(Matrix(orientation.Basis));
                actual.AddRange(Vector(orientation.Capture().LastNormal));
                actual.AddRange(Vector(orientation.Forward)); actual.AddRange(Vector(orientation.Right)); actual.AddRange(Vector(orientation.Up));
                actual.AddRange(Vector(result.NegativeNormal)); actual.AddRange(Matrix(result.InverseBasis)); actual.Add(stands);
                int[] values = expected.EnumerateArray().Select(value => value.GetInt32()).ToArray();
                assert(actual.SequenceEqual(values),
                    $"Original SetStartOrientation{index}/visit{frame++}: current-matrix yaw, source normal cache16/17 threshold, heading twist, cached axes/negative normal, pre-fallback transpose and genuine stand boundary. Actual=[{string.Join(',', actual)}], expected=[{string.Join(',', values)}].");
            }
            // The real controller API reads its existing normal and heading delta; it cannot query the world or re-place the player.
            if (normal == new FixedVector3(0, 4096, 0) && delta == 0)
            {
                var driver = new Animation();
                var controller = new PlayerTerrainController(new(123456, -345678, 567890), 0, new NoWorldQueries(), driver,
                    new(false, new(false, 0, false)));
                controller.Orientation.Restore(new(Basis(matrix), cache, true));
                int visit = 0;
                foreach (JsonElement expected in row.GetProperty("expected").EnumerateArray())
                {
                    controller.SetStartOrientation((short)angles[1]);
                    int[] values = expected.EnumerateArray().Select(value => value.GetInt32()).ToArray();
                    assert(Matrix(controller.Orientation.Basis).SequenceEqual(values.Take(9)) &&
                        Vector(controller.Orientation.Capture().LastNormal).SequenceEqual(values.Skip(9).Take(3)) &&
                        controller.Position == new FixedVector3(123456, -345678, 567890) && controller.Velocity == default,
                        $"Original controller SetStartOrientation{index}/visit{visit++} must preserve position/velocity and apply the current matrix and genuine retained source normal cache.");
                }
            }
            index++;
        }
    }

    private static int[] Values(JsonElement row, string name) => row.GetProperty(name).EnumerateArray().Select(value => value.GetInt32()).ToArray();
    private static FixedVector3 Vector(IReadOnlyList<int> values) => new(values[0], values[1], values[2]);
    private static int[] Vector(FixedVector3 value) => [value.X, value.Y, value.Z];
    private static FixedJointTransform Basis(IReadOnlyList<short> m) => new(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], 0, 0, 0);
    private static int[] Matrix(FixedJointTransform m) => [m.M00, m.M01, m.M02, m.M10, m.M11, m.M12, m.M20, m.M21, m.M22];
    private sealed class NoWorldQueries : IPlayerWorldQuery
    {
        public PlayerWorldHit? Trace(FixedVector3 start, FixedVector3 end) =>
            throw new InvalidOperationException("Source SetStartOrientation must not query world collision.");
    }
    private sealed class Animation : IPlayerAnimationDriver
    {
        public AnimationPlaybackState Playback { get; private set; } = AnimationPlaybackState.Initial;
        public int Speed { get => Playback.Speed; set => Playback = Playback with { Speed = value }; }
        public void Advance(int ticks) => Playback = ActorAnimationPlayback.Advance(Playback, ticks).State;
        public void Run(int clip, int from = 0, int to = -1) => Playback = ActorAnimationPlayback.Run(Playback, (ushort)clip, 22, from, to);
    }
}
