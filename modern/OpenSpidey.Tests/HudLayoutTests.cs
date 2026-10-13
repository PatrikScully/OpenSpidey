using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Presentation;

internal static class HudLayoutTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(HudLayoutTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.hud-layout.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var frame = new SpriteAnimationFrame((sbyte)input[4], (sbyte)input[5], (byte)input[6], (byte)input[7], 0);
            HudSourceQuad quad = HudSpriteLayout.Stretch(frame, input[0], input[1], input[2], input[3]);
            int[] actual = [quad.Left, quad.Top, quad.Right, quad.Top, quad.Left, quad.Bottom, quad.Right, quad.Bottom];
            assert(actual.SequenceEqual(test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt32())),
                $"HUD sprite{index++} matches original PC rounding, offset fallback and short vertex storage.");
        }

        var face = new SpriteAnimationFrame(-32, -32, 64, 64, 0);
        var end = new SpriteAnimationFrame(-16, -16, 32, 32, 0);
        HudSourceQuad faceQuad = HudSpriteLayout.Stretch(face, 67, 45, 28, 30);
        HudSourceQuad endQuad = HudSpriteLayout.Stretch(end, 132, 36, 11, 16);
        var baseline = new HudViewportTransform(640, 480);
        ScreenRectangle baselineFace = baseline.Project(faceQuad), baselineEnd = baseline.Project(endQuad);
        foreach ((int width, int height) in new[] { (640, 480), (1280, 720), (2560, 1080), (3840, 2160) })
        {
            var transform = new HudViewportTransform(width, height);
            ScreenRectangle projectedFace = transform.Project(faceQuad), projectedEnd = transform.Project(endQuad);
            float relativeScale = height / 480f;
            assert(Math.Abs(projectedFace.Width - baselineFace.Width * relativeScale) < 0.0001f &&
                Math.Abs(projectedFace.Height - baselineFace.Height * relativeScale) < 0.0001f &&
                Math.Abs(projectedEnd.X - projectedFace.X - (baselineEnd.X - baselineFace.X) * relativeScale) < 0.0001f,
                "Wide HUD keeps original icon proportions and every inter-piece gap in one shared height-based transform.");
            ScreenRectangle right = transform.Project(endQuad, HudHorizontalAnchor.Right);
            assert(Math.Abs(width - right.X - (640 - baselineEnd.X) * relativeScale) < 0.0001f,
                "Right HUD group preserves its original distance from the viewport edge.");
            ScreenRectangle menu = transform.MenuFrame;
            assert(Math.Abs(menu.Width / menu.Height - 4f / 3) < 0.0001f &&
                Math.Abs(menu.X * 2 + menu.Width - width) < 0.0001f && Math.Abs(menu.Y * 2 + menu.Height - height) < 0.0001f,
                "Main menu remains centered4:3 independently of the full gameplay viewport.");
        }
        var portrait = new HudViewportTransform(320, 640);
        assert(portrait.MenuFrame == new ScreenRectangle(0, 200, 320, 240) &&
            portrait.Project(endQuad, HudHorizontalAnchor.Right, HudVerticalAnchor.Bottom).Y == endQuad.Top + 400,
            "A narrow viewport fits the complete source UI and bottom-anchors its group without distorting dimensions.");
    }
}
