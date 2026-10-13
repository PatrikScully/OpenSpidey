using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class ThugCombatRingTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugCombatRingTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-combat-ring.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            if (input[0] == 2)
            {
                ThugPingPixel[]? corners = ThugPingProjection.Project(new(-16, -16, 32, 32, 0xC11FEECC), (ushort)input[2], (short)input[1], (int)input[3], (int)input[4], (int)input[5], 0x0A001000);
                assert(corners is not null && corners.SelectMany(pixel => new[] { pixel.X, pixel.Y }).SequenceEqual(expected.EnumerateArray().SelectMany(pixel => pixel.EnumerateArray().Select(value => value.GetInt32()))),
                    "Combat ring preserves original HITSPRIT rotated packed corners and unequal X4096/Y2560 post-scale.");
            }
            else
            {
                uint randomState = (uint)input[12];
                int calls = 0;
                int Random(int range)
                {
                    calls++;
                    assert(range == 4096, "Combat ring angle requests the exact full source heading range.");
                    randomState = unchecked(randomState * 1664525 + 1013904223);
                    return (int)(randomState % (uint)range);
                }
                FixedVector3 position = new((int)input[2], (int)input[3], (int)input[4]);
                ThugCombatImpactRing[] rings = input[0] == 0 ? ThugCombatImpactRing.Create((int)input[1], position, (uint)input[11], Random) :
                    [new(position, (byte)input[5], (byte)input[6], (byte)input[7], (int)input[8], (int)input[9], (int)input[10], (uint)input[11], Random)];
                assert(rings.Length == expected.GetArrayLength() && calls == rings.Length, "CThug creates two source color-bank rings in constructor/random order, ignoring flags.");
                for (int ring = 0; ring < rings.Length; ring++)
                {
                    int frame = 0;
                    foreach (JsonElement reference in expected[ring].EnumerateArray())
                    {
                        if (frame != 0) rings[ring].Advance((uint)input[13 + frame]);
                        var state = rings[ring];
                        long[] actual = [state.Position.X, state.Position.Y, state.Position.Z, state.Angle, state.Scale, state.MaximumScale,
                            state.GrowthPerTick, state.LastClock, state.PostScale, state.CodeRgb, state.FrigDeltaZ, state.AnimationIndex, state.Frame, state.Alive ? 0 : 1];
                        assert(actual.SequenceEqual(reference.EnumerateArray().Select(value => value.GetInt64())),
                            $"Combat ring{index}:{ring} update{frame} preserves original PC wrapping clock, signed-short growth, clamp-after-narrowing, delayed equality death and retained clock.");
                        frame++;
                    }
                }
            }
            index++;
        }
        var boundary = new ThugCombatImpactRing(default, 18, 18, 108, 384, 1792, 144, 0, _ => 0);
        boundary.Advance(10);
        assert(boundary.Alive && boundary.Scale == 1792 && boundary.LastClock == 10, "Reaching maximum scale remains visible for that source update.");
        boundary.Advance(12);
        assert(!boundary.Alive && boundary.LastClock == 10, "Next Move kills an equal-scale ring before reading or storing the new source clock.");
    }
}
