using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class SmokeTrailTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(SmokeTrailTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.smoke-trail.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            FixedVector3 initial = new(input[6], input[7], input[8]);
            var trail = new SmokeTrail(initial, input[0], input[1], input[2], input[3], (byte)input[4]);
            foreach (SmokeTrailSegment segment in trail.Segments) segment.AnimationSpeed = (short)input[5];
            Verify(0);
            for (int tick = 0; tick < 36; tick++)
            {
                if (tick % 3 != 2) trail.SetPosition(initial + new FixedVector3(tick * 12345, -tick * 23456, tick * 34567));
                if (tick == input[9]) trail.FadeAway = 1;
                foreach (SmokeTrailSegment segment in trail.Segments) segment.Move();
                trail.Move(); Verify(tick + 1);
            }
            trail.Dispose();
            assert(trail.IsDisposed, "Smoke owner disposal remains distinct from its fade-to-dead bit.");
            void Verify(int snapshot)
            {
                object[] actual = [trail.Cursor, trail.FadeAway, trail.IsDead ? 1 : 0, input[0] + 1, 1,
                    Enumerable.Range(0, input[0]).ToArray(), trail.Points.Select(point => new[] { point.X, point.Y, point.Z }).ToArray(),
                    trail.Segments.Select(segment => new long[] { segment.Start.X, segment.Start.Y, segment.Start.Z,
                        segment.End.X, segment.End.Y, segment.End.Z, segment.Flags, segment.CodeBgr, segment.Scale,
                        segment.AnimationSpeed, segment.FrameFraction, segment.Frame, segment.FrameCount, segment.FadeRate, 1, 0 }).ToArray()];
                string text = JsonSerializer.Serialize(actual), wanted = test.GetProperty("expected")[snapshot].GetRawText();
                assert(text == wanted, $"Actual PC smoke[{string.Join(',', input)}] snapshot{snapshot}: ring-buffer endpoints, source child order, protected lifetime, code/scale, signed animation wrap and complete fade state; actual{text}, expected{wanted}.");
            }
        }
        VerifyProjection(assert);
    }

    private static void VerifyProjection(Action<bool, string> assert)
    {
        using Stream stream = typeof(SmokeTrailTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.linked-ribbon-projection.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[][] input = test.GetProperty("input").EnumerateArray().Select(row => row.EnumerateArray().Select(value => value.GetInt32()).ToArray()).ToArray();
            var segments = input.Select((row, index) => new LinkedRibbonView(index, (byte)row[0], new(row[1], row[2], row[3]),
                new(row[4], row[5], row[6]), (ushort)row[7], (byte)row[8]));
            IReadOnlyList<LinkedRibbonQuad> quads = LinkedRibbonProjection.Project(segments, 5, 8048);
            int[][] actual = quads.Select(quad => new[] { (int)quad.Identity, quad.First.X, quad.First.Y, quad.Second.X, quad.Second.Y,
                quad.Third.X, quad.Third.Y, quad.Fourth.X, quad.Fourth.Y }).ToArray();
            string text = JsonSerializer.Serialize(actual), wanted = test.GetProperty("expected").GetRawText();
            assert(text == wanted, $"Actual PC connected ribbon rendering{test.GetProperty("input").GetRawText()}: frameWidth, signed16scale/unsigneddivision, exact winding/cache/history/failure rules and packed signed16corners; actual{text}, expected{wanted}.");
        }
    }
}
