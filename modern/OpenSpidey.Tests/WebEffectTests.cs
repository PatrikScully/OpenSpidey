using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class WebEffectTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(WebEffectTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.web-effects.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            JsonElement[] expected = test.GetProperty("expected").EnumerateArray().ToArray();
            if (input[0] == 0)
            {
                var ring = new WebImpactRing(new(new(input[1], input[2], input[3]), (byte)input[4], (byte)input[5], (byte)input[6], input[7], input[8]));
                for (int frame = 0; frame < expected.Length; frame++)
                {
                    if (frame != 0) ring.AdvanceBits();
                    long[] actual = [ring.Scale, ring.CodeRgb, ring.ColourDecay, ring.Alive ? 0 : 1];
                    assert(actual.SequenceEqual(expected[frame].EnumerateArray().Select(v => v.GetInt64())),
                        $"Web ring case {caseIndex} frame {frame}: original-PC short growth, strict scale death, RGB fade and post-death retirement.");
                }
                assert(ring.AnimationIndex == 9 && ring.Frame == 1 && ring.PostScale == 0x0A001000 && ring.FrigDeltaZ == 100,
                    "Web impact ring retains actual Impactwb frame1 and source projection metadata.");
            }
            else if (input[0] == 1)
            {
                var splat = new WebSurfaceSplat(new(input[1], input[2], input[3]), new((short)input[4], (short)input[5], (short)input[6]));
                assert(splat.Corners.SelectMany(v => new[] { v.X, v.Y, v.Z }).SequenceEqual(expected[0].EnumerateArray().Select(v => v.GetInt32())),
                    $"Web splat case {caseIndex}: exact source CalcPerps and nonrandom fifty-unit surface corners without a second standoff.");
                for (int frame = 1; frame < expected.Length; frame++)
                {
                    if (frame != 1) splat.AdvanceBits();
                    long[] actual = [splat.Mode, splat.Age, splat.Tint, splat.Alive ? 0 : 1];
                    assert(actual.SequenceEqual(expected[frame].EnumerateArray().Select(v => v.GetInt64())),
                        $"Web splat case {caseIndex} frame {frame - 1}: original-PC transition tick, signed age, delayed fade and cleanup.");
                }
            }
            else
            {
                int cursor = input[16]; var events = new List<int>();
                int Random(int bound) { int result = (cursor++ * 7 + 1) % bound; events.AddRange([bound, result]); return result; }
                FixedVector3 Vector(int offset) => new(input[offset], input[offset + 1], input[offset + 2]);
                var fragment = new WebFragment(new(input[1], Vector(2), Vector(5), Vector(8), Vector(11), input[14], input[15]), Random);
                assert(events.SequenceEqual(expected[0].EnumerateArray().Select(v => v.GetInt32())),
                    $"Web fragment case {caseIndex}: source optional three jitter draws then two color-decay draws in shared RNG order.");
                for (int frame = 1; frame < expected.Length; frame++)
                {
                    if (frame != 1) fragment.AdvanceBits();
                    var actual = new List<long>();
                    foreach (FixedVector3 v in new[] { fragment.Start, fragment.End, fragment.CompanionStart, fragment.CompanionEnd,
                        fragment.StartVelocity, fragment.EndVelocity, fragment.CompanionVelocity }) actual.AddRange([v.X, v.Y, v.Z]);
                    actual.AddRange([fragment.CodeRgb0, fragment.CodeRgb1, fragment.CodeRgb0, fragment.CodeRgb1,
                        fragment.ColourDecay, fragment.CompanionProtected ? 1 : 0, fragment.Alive ? 0 : 1]);
                    assert(actual.SequenceEqual(expected[frame].EnumerateArray().Select(v => v.GetInt64())),
                        $"Web fragment case {caseIndex} frame {frame - 1}: source three endpoints/velocity copies, strict floor clamp before gravity, shared companion color and lifetime.");
                }
            }
            caseIndex++;
        }
    }
}
