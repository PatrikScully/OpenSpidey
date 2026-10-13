using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.UI;

internal static class PlayerLookaroundReticleTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerLookaroundReticleTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.lookaround-reticle.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            var view = new PlayerLookaroundReticleView((byte)input[0], new((int)input[1], (int)input[2], (int)input[3]), (uint)input[10]);
            FixedVector3 relative = PlayerLookaroundReticle.ProjectionVector(view.TargetPosition, new((int)input[4], (int)input[5], (int)input[6]));
            FixedVector3 wantedRelative = new(expected[1].GetInt32(), expected[2].GetInt32(), expected[3].GetInt32());
            assert(expected[0].GetInt32() == (view.Type == 0 ? 0 : 1) && (view.Type == 0 || relative == wantedRelative),
                "Actual RenderLookaroundReticle keeps each target component's whole-world arithmetic shift before subtracting the original whole camera position.");
            var frame = new SpriteAnimationFrame((sbyte)input[11], (sbyte)input[12], (byte)input[13], (byte)input[14], 1);
            PlayerLookaroundReticleQuad? quad = PlayerLookaroundReticle.Build(view, frame, (ushort)input[7], (ushort)input[8], (int)input[9], input[15] == 0);
            List<long> events = [];
            if (view.Type != 0) events.Add(1);
            if (quad is { } drawn)
            {
                var q = drawn.Rectangle;
                events.AddRange([2, 7, 1, 3, q.Left, q.Top, q.Right, q.Top, q.Left, q.Bottom, q.Right, q.Bottom, drawn.Argb, 60000]);
            }
            long[] wanted = expected[4].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(events.SequenceEqual(wanted), $"Actual PC world reticle[{string.Join(',', input)}] source minimum scale, unsigned multiplication/logical shift, source signed frame offsets, wrapped short corners, depth6, allocation exit and B0 alpha/BGR channel packing; actual[{string.Join(',', events)}], expected[{string.Join(',', wanted)}].");
        }
    }
}
