using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class WebImpactSplatTests
{
    public static void Run(Action<bool, string> assert)
    {
        var splat = new WebImpactSplat(default, new(0, 4096, 0), 0);
        assert(splat.Age == 1 && splat.Scale == 16 && splat.Tint == 128 &&
            splat.Corners[0] == new FixedVector3(-(16 << 12), 10 << 12, -(16 << 12)),
            "A splat's constructor grows its first quad and offsets its center ten units from the surface.");
        splat.Step();
        assert(splat.Scale == 24 && splat.Corners[3] == new FixedVector3(24 << 12, 10 << 12, 24 << 12),
            "Splat growth updates both surface-local corner offsets with integer easing.");
        for (int update = splat.Age; update < 30; update++) splat.Step();
        assert(splat.Scale == 31 && splat.Tint == 128 && !splat.Finished,
            "Integer growth settles at thirty-one while tint remains unchanged through age thirty.");
        splat.Step();
        assert(splat.Tint == 123, "The first fade occurs strictly after age thirty.");
        while (!splat.Finished) splat.Step();
        assert(splat.Age == 56 && splat.Tint == 0, "The splat retires when its source tint reaches zero.");
        int age = splat.Age;
        splat.Step();
        assert(splat.Age == age, "Retired splats no longer update their geometry or age.");
        var wall = new WebImpactSplat(new(100 << 12, 0, 0), new(-4096, 0, 0), 0);
        assert(wall.Corners.All(corner => corner.X == 90 << 12),
            "Wall splats retain a quad on the target plane offset by its raw normal.");
    }
}
