using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerGroundSolverTests
{
    public static void Run(Action<bool, string> assert)
    {
        var frame = new PlayerGroundFrame(new(100 << 12, -96 << 12, 200 << 12), default,
            new(1, 0, 3), 0x40, 96, false, new(1, 4, 1));
        var contact = new PlayerGroundContact(new(100 << 12, 0, 200 << 12), new(0, -4096, 0), 136, 0, false);
        var hit = new PlayerWorldHit(contact, 7, 8, 9);
        var direct = new ScriptedPlayerWorld(hit);
        PlayerFallResult landing = PlayerGroundSolver.ResolveFall(frame, 8193, 3, direct, new(true, 10, true));
        assert(direct.Rays.Count == 1 && direct.Rays[0] == (new FixedVector3(409600, -557056, 819200),
            new FixedVector3(409600, 28673, 819200)), "The initial fall ray preserves original offsets and elapsed-tick displacement.");
        assert(landing.Hit == hit && landing.Ground is not null && landing.Movement.Position == frame.Position &&
            landing.Movement.Velocity == new FixedVector3(1, 0, 3), "The first ground hit resolves immediately without wider probes.");

        var miss = new ScriptedPlayerWorld();
        PlayerFallResult falling = PlayerGroundSolver.ResolveFall(frame, 8193, 3, miss, new(false, 10, true));
        assert(miss.Rays.Count == 1 && falling.Hit is null && falling.Ground is null &&
            falling.Movement.Position.Y == -380927 && falling.Movement.Velocity.Y == 8193,
            "A ground miss applies fall and restores saved vertical velocity without synthesizing a contact.");

        var wide = new ScriptedPlayerWorld();
        PlayerGroundSolver.ResolveFall(frame, 8193, 3, wide, new(true, 10, true));
        (int X, int Z)[] expected = [(100, 200), (110, 200), (90, 200), (100, 210), (100, 190),
            (105, 205), (105, 195), (95, 205), (95, 195), (100, 200)];
        assert(wide.Rays.Count == expected.Length, "The wide fallback performs eight offset rays and the final center repeat.");
        assert(wide.Rays.Select(ray => (ray.Start.X >> 12, ray.Start.Z >> 12)).SequenceEqual(expected) &&
            wide.Rays.All(ray => ray.Start.Y == -557056 && ray.End.Y == 28673),
            "Wide probes preserve the recovered coordinate order and vertical range.");
        var nearby = new ScriptedPlayerWorld(null, null, hit);
        PlayerFallResult edge = PlayerGroundSolver.ResolveFall(frame, 8193, 3, nearby, new(true, 10, true));
        assert(nearby.Rays.Count == 3 && edge.Hit == hit && edge.Ground is not null,
            "Wide probing stops on its first hit and routes that hit through ground response.");
    }
}
