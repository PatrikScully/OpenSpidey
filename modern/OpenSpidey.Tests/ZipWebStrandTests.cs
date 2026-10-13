using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class ZipWebStrandTests
{
    public static void Run(Action<bool, string> assert)
    {
        var hook = new FixedVector3(240 << 12, 0, 0);
        var strand = new ZipWebStrand(default, hook, _ => 0);
        strand.Step(hook, new(0, 0, 100 << 12), 2);
        assert(strand.Outer.Count == 4 && strand.Inner.Count == 7 && strand.Knots.Count == 3 &&
            strand.Outer[0] == default && strand.Outer[^1] == hook,
            "The zip strand uses one segment per eighty units and a separate two-part inner line.");
        assert(strand.Knots[0] == new FixedVector3(20 << 12, 0, 0) &&
            strand.Inner[1] == new FixedVector3(10 << 12, 36720, 0) && strand.Inner[2] == strand.Knots[0],
            "Knot placement and camera-facing side offsets retain original integer division and signed offsets.");
        strand.Step(hook, hook, 2);
        assert(strand.Inner[1] == new FixedVector3(10 << 12, 0, 0),
            "A camera aligned with the strand produces the recovered zero-side fallback.");
        var shortStrand = new ZipWebStrand(default, new(1, 0, 0), _ => 0);
        var longStrand = new ZipWebStrand(default, new(10000 << 12, 0, 0), _ => 0);
        assert(shortStrand.Knots.Count == 1 && longStrand.Knots.Count == 40,
            "Short and long strands clamp segment counts to the recovered one-to-forty range.");
        strand.Release();
        strand.Step(new(999 << 12, 0, 0), new(0, 0, 100 << 12), 2);
        assert(strand.Released && !strand.Finished && strand.Outer[0].X is > 0 and < (240 << 12) && strand.Outer[^1] == default,
            "Released web contraction follows the cached hook toward the anchor rather than the moving actor.");
        for (int update = 0; update < 7; update++) strand.Step(default, default, 2);
        assert(!strand.Finished, "A released strand remains present through sixteen elapsed ticks.");
        strand.Step(default, default, 2);
        assert(strand.Finished, "The original greater-than-sixteen boundary retires the contracted strand.");
    }
}
