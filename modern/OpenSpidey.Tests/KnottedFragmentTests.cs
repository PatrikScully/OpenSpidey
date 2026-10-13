using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class KnottedFragmentTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(KnottedFragmentTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.knotted-fragment.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
            var events = new List<long>(); int cursor = (int)input[18];
            int Random(int bound) { int value = (cursor++ * 7 + 1) % bound; events.AddRange([1, bound, value]); return value; }
            int Ground(FixedVector3 middle)
            {
                events.AddRange([3, middle.X, middle.Y, middle.Z, middle.X, unchecked(middle.Y + 0x1388000), middle.Z, 1]);
                return input[15] != 0 ? (int)input[16] : unchecked(middle.Y + 0x1388000);
            }
            void Fire(FixedVector3 position, int size, Func<int, int> random) => throw new InvalidOperationException("Broken plain webs have no fire variant.");
            var fragment = new KnottedWebFragment(new(Vector(0), Vector(3), Vector(6), Vector(9)), Random, Ground);
            JsonElement[] expected = test.GetProperty("expected").EnumerateArray().ToArray();
            uint clock = (uint)input[17];
            for (int frame = 0; frame < expected.Length - 1; frame++)
            {
                if (frame != 0) { clock = unchecked(clock + 2); fragment.AdvanceBits(clock, Vector(12), Random, Fire); }
                var actual = new List<long>();
                void Append(FixedVector3 value) => actual.AddRange([value.X, value.Y, value.Z]);
                Append(fragment.Start); Append(fragment.End); Append(fragment.StartVelocity); Append(fragment.EndVelocity);
                actual.AddRange([fragment.GroundY, fragment.RestDistance, fragment.Age, fragment.Alive ? 0 : 1, fragment.Strand.SegmentCount]);
                KnottedWebStrand strand = fragment.Strand;
                Append(strand.Start); Append(strand.Side); actual.AddRange([strand.TriggerPulse, strand.Pulse, strand.StartColour]);
                Append(strand.InnerStart); actual.Add(strand.InnerStartColour);
                for (int index = 0; index < strand.SegmentCount; index++)
                {
                    WebStrandKnot knot = strand.Knots[index]; Append(strand.Ends[index]); actual.Add(strand.Colours[index]); Append(knot.RestPosition);
                    actual.AddRange([knot.WobbleAmplitude, knot.WobbleSpeed, knot.Phase, knot.SideOffset, knot.Fraction]);
                    Append(knot.Position); actual.Add(knot.CodeRgb);
                }
                for (int index = 0; index < strand.InnerEnds.Count; index++) { Append(strand.InnerEnds[index]); actual.Add(strand.InnerColours[index]); }
                assert(actual.SequenceEqual(expected[frame].EnumerateArray().Select(value => value.GetInt64())),
                    "Actual PC broken knotted web preserves both endpoint gravities, floor damping, from-minus-to distance rounding, rest contraction, pulse2 and age31 death.");
            }
            assert(events.SequenceEqual(expected[^1].EnumerateArray().Select(value => value.GetInt64())),
                "Actual PC broken web constructor preserves all knot RNG calls before midpoint ground ray, with source environmental query mode1.");
            assert(!fragment.Strand.IsDisposed, "Source dead fragment leaves protected child bits attached until the shared GPolyLine destructor phase.");
            fragment.Dispose(); assert(fragment.Strand.IsDisposed, "Source fragment destructor retires the protected inner line and all knots.");
        }
        var coincident = new KnottedWebFragment(new(default, default, default, default), bound => 1, point => 100000);
        coincident.AdvanceBits(2, default, bound => 1, (position, size, random) => throw new InvalidOperationException());
        assert(!coincident.Alive && !coincident.Strand.IsDisposed, "Coincident fragment safely marks dead instead of the original distance division crash, preserving cleanup phase ownership.");
        coincident.Dispose();
    }
}
