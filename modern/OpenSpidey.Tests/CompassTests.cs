using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.UI;

internal static class CompassTests
{
    public static void Run(Action<bool, string> assert)
    {
        using JsonDocument document = JsonDocument.Parse(typeof(CompassTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.compass.json")!);
        assert(document.RootElement.GetProperty("originalPcDisplayVerified").GetBoolean(), "Compass gold must be verified against original PC463860.");
        int index = 0;
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var compass = new CompassState { FlashTimer = (int)input[10] };
            compass.Create(new FixedVector3((int)input[1], (int)input[2], (int)input[3]) << 12);
            if (input[0] == 0) compass.Destroy();
            (short sine, short cosine) = GameAngle.Heading((int)input[7]);
            var matrix = new FixedJointTransform(cosine, 0, sine, 0, 4096, 0, (short)-sine, 0, cosine, 0, 0, 0);
            var host = new Host((int)input[11]);
            compass.Display(new(new((int)input[4], (int)input[5], (int)input[6]), matrix, (uint)input[8], (int)input[9]), new(-32, -32, 64, 64, 0x33CADC71), host);
            long[] actual = [compass.FlashTimer, ..host.Events];
            long[] expected = test.GetProperty("events").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(expected), $"Compass PC case{index}, input[{string.Join(',', input)}]: actual[{string.Join(',', actual)}] expected[{string.Join(',', expected)}].");
            index++;
        }
        var state = new CompassState { FlashTimer = 13 };
        state.Create(new(-4097, 4095, 8193));
        assert(state.Active && state.TargetPosition == new FixedVector3(-2, 0, 2) && state.FlashTimer == 13, "Create stores source fixed target shifted12 and does not initialize flash.");
        state.Destroy(); assert(!state.Active && state.TargetPosition == new FixedVector3(-2, 0, 2) && state.FlashTimer == 13, "Destroy clears only compass status.");
        var small = new HudViewportTransform(640, 480); var wide = new HudViewportTransform(1280, 480);
        ScreenRectangle source = new(406, 183, 51, 40);
        ScreenRectangle first = small.Project(source, HudHorizontalAnchor.Right, HudVerticalAnchor.Bottom);
        ScreenRectangle second = wide.Project(source, HudHorizontalAnchor.Right, HudVerticalAnchor.Bottom);
        assert(second.X - first.X == 640 && second.Y == first.Y && second.Width == first.Width && second.Height == first.Height, "Right-bottom compass viewport group retains proportional dimensions and exact wider-screen edge offset.");
    }
    private sealed class Host(int failure) : ICompassDrawHost
    {
        public List<long> Events { get; } = [];
        public bool TryAllocateNeedle(int half) { Events.AddRange([1, half]); return half != failure; }
        public void EmitQuad(CompassQuad quad) => Events.AddRange([3, quad.First.X, quad.First.Y, quad.Second.X, quad.Second.Y,
            quad.Third.X, quad.Third.Y, quad.Fourth.X, quad.Fourth.Y, quad.Argb, (int)Math.Round(quad.Depth * 10000),
            (int)Math.Round(quad.LeftU * 10000), (int)Math.Round(quad.RightU * 10000)]);
    }
}
