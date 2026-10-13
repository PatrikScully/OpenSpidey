using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class KnottedWebTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(KnottedWebTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.knotted-web.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(v => v.GetInt64()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
            int cursor = (int)input[10]; var events = new List<long>();
            int Random(int bound) { int value = (cursor++ * 7 + 1) % bound; events.AddRange([1, bound, value]); return value; }
            void EmitFire(FixedVector3 position, int scale, Func<int, int> random)
            { events.AddRange([2, 5, position.X, position.Y, position.Z, scale]); random(7); }
            var strand = new KnottedWebStrand(Vector(0), Vector(3), Random);
            strand.SetEndpoints(Vector(0), Vector(3));
            strand.Side = KnottedWebStrand.FacingCamera(Vector(0), Vector(3), Vector(6));
            strand.CacheRestPositions();
            strand.FlickerKnots = (byte)input[11]; strand.WobbleKnots = (byte)input[12];
            strand.TriggerPulse = (byte)input[13]; strand.PulseDecay = (byte)input[14]; strand.FireVariant = (int)input[15];
            assert(strand.SegmentCount == expected.GetProperty("count").GetInt32(),
                $"Knotted web case {caseIndex}: original whole-unit distance/80 count, capped1..40.");
            uint clock = (uint)input[9]; int frame = 0;
            foreach (JsonElement state in expected.GetProperty("states").EnumerateArray())
            {
                if (frame != 0) { clock = unchecked(clock + 2); strand.AdvanceBits(clock, Random, EmitFire); }
                var actual = new List<long>();
                void Append(FixedVector3 value) => actual.AddRange([value.X, value.Y, value.Z]);
                Append(strand.Start); Append(strand.Side); actual.AddRange([strand.TriggerPulse, strand.Pulse, strand.StartColour]);
                Append(strand.InnerStart); actual.Add(strand.InnerStartColour);
                for (int i = 0; i < strand.SegmentCount; i++)
                {
                    WebStrandKnot knot = strand.Knots[i]; Append(strand.Ends[i]); actual.Add(strand.Colours[i]); Append(knot.RestPosition);
                    actual.AddRange([knot.WobbleAmplitude, knot.WobbleSpeed, knot.Phase, knot.SideOffset, knot.Fraction]);
                    Append(knot.Position); actual.Add(knot.CodeRgb);
                }
                for (int i = 0; i < strand.InnerEnds.Count; i++) { Append(strand.InnerEnds[i]); actual.Add(strand.InnerColours[i]); }
                assert(actual.SequenceEqual(state.EnumerateArray().Select(v => v.GetInt64())),
                    $"Knotted web case {caseIndex} frame {frame}: source/PC endpoints, protected knots, phase/pulse order, signed offsets and exact RGB banks.");
                frame++;
            }
            assert(events.SequenceEqual(expected.GetProperty("events").EnumerateArray().Select(v => v.GetInt64())),
                $"Knotted web case {caseIndex}: constructor/flicker/fire callbacks retain original shared RNG order, including effect callback consumption.");
            strand.Dispose(); assert(!strand.Alive, "Knotted web disposal retires owner, protected inner line and all knot sprites together.");
            caseIndex++;
        }
    }
}
