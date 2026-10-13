using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class WebSwingAnchorTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(WebSwingAnchorTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.web-swing-anchor.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
            var matrix = new SourceMatrix((short)input[4], (short)input[5], (short)input[6], (short)input[7], (short)input[8],
                (short)input[9], (short)input[10], (short)input[11], (short)input[12], (ushort)input[13], Vector(14));
            int cursor = 0; var events = new List<long>();
            int Random(int bound) { int result = (cursor++ * 7 + 1) % bound; events.AddRange([1, bound, result]); return result; }
            uint clock = (uint)input[20];
            using var swing = new WebSwingAnchor(Vector(0), (int)input[3], matrix, Vector(17), clock, new(500, 300, -700), Random);
            int stateIndex = 0;
            foreach (JsonElement expected in test.GetProperty("expected").GetProperty("states").EnumerateArray())
            {
                if (stateIndex == 1) { swing.FireVariant = (int)input[22]; swing.Angles = swing.Angles with { Z = (short)input[26] }; }
                if ((stateIndex & 1) != 0) { clock = unchecked(clock + (uint)input[21]); swing.Advance(clock, new(500, 300, -700)); }
                else if (stateIndex != 0) swing.SetRenderEnd(Vector(23), new(500, 300, -700));
                var actual = new List<long> { swing.LastUpdateTick, swing.Angles.X, swing.Angles.Y, swing.Angles.Z,
                    swing.Phase, swing.PhaseSpeed, swing.LengthEighth, swing.SampleCount, swing.FireVariant };
                void Append(FixedVector3 vector) => actual.AddRange([vector.X, vector.Y, vector.Z]);
                void Matrix(SourceMatrix value)
                {
                    actual.AddRange([value.M00, value.M01, value.M02, value.M10, value.M11, value.M12,
                        value.M20, value.M21, value.M22, value.Reserved]); Append(value.Translation);
                }
                Append(swing.RenderEndOffset); Append(swing.Anchor); Matrix(swing.BaseMatrix); Matrix(swing.SwingMatrix);
                Append(swing.Forward); Append(swing.Side); Append(swing.Down); Append(swing.CurrentPosition);
                actual.AddRange([swing.IsComplete ? 1 : 0, swing.AnimationFrame((int)input[27]), swing.Strand.SegmentCount,
                    swing.Strand.FlickerKnots, swing.Strand.WobbleKnots, swing.Strand.FireVariant]);
                Append(swing.Strand.Start); Append(swing.Strand.Side);
                for (int i = 0; i < swing.Strand.SegmentCount; i++) { Append(swing.Strand.Ends[i]); Append(swing.Strand.Knots[i].RestPosition); }
                Append(swing.ReleasedEndpoint);
                long[] wanted = expected.EnumerateArray().Select(value => value.GetInt64()).ToArray();
                int mismatch = Enumerable.Range(0, Math.Min(actual.Count, wanted.Length)).FirstOrDefault(i => actual[i] != wanted[i], -1);
                assert(actual.SequenceEqual(wanted), $"Actual PC swing {caseIndex} state {stateIndex}: direct clock, complete MATRIX, phase, axes, source endpoints; first difference {mismatch}.");
                stateIndex++;
            }
            assert(events.SequenceEqual(test.GetProperty("expected").GetProperty("events").EnumerateArray().Select(value => value.GetInt64())),
                $"Actual PC swing {caseIndex}: original knotted constructor random order and no actor-AI random consumption.");
            caseIndex++;
        }
    }
}
