using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerTerrainMotionTests
{
    public static void Run(Action<bool, string> assert)
    {
        var policy = new PlayerTerrainPolicy(true, new(false, 10, true));
        var frame = new PlayerTerrainFrame(new(1000000, -393216, 2000000), new(8192, 8192, 0), default,
            new(31, 31, 31), 2, 96, 70, false);
        PlayerWorldHit floor = new(new(new(1008192, 0, 2000000), new(0, -4096, 0), 136, 0, false), 1, 2, 3);
        var walking = new ScriptedPlayerWorld(null, null, null, floor);
        PlayerTerrainStep grounded = PlayerTerrainMotion.Step(frame, walking, policy);
        assert(grounded.Movement.Position == new FixedVector3(1008192, -393216, 2000000) &&
            grounded.Movement.Velocity == new FixedVector3(8192, 0, 0) && grounded.CollisionFlags == 2,
            "Ordinary terrain motion sweeps horizontal travel before resolving saved downward velocity.");
        assert(walking.Rays.Count == 4 && grounded.Fall?.Hit == floor && grounded.CeilingHit is null,
            "Downward motion queries ground after the body and shoulder sweeps without a ceiling query.");

        var free = new ScriptedPlayerWorld();
        PlayerTerrainStep falling = PlayerTerrainMotion.Step(frame, free, policy);
        assert(falling.Movement.Position == new FixedVector3(1008192, -385024, 2000000) &&
            falling.Movement.Velocity == frame.Velocity && falling.CollisionFlags == 0,
            "An unobstructed falling step preserves both horizontal and vertical displacement.");
        PlayerWorldHit ceiling = new(new(new(1000000, -400000, 2000000), new(0, 4096, 0), 10, 0, false), 1, 2, 4);
        var rising = new ScriptedPlayerWorld(ceiling);
        PlayerTerrainStep clipped = PlayerTerrainMotion.Step(frame with { Velocity = new(0, -8192, 0) }, rising, policy);
        assert(clipped.Movement.Position.Y == frame.Position.Y && clipped.Movement.Velocity.Y == 0 &&
            clipped.CollisionFlags == 0x100 && clipped.Fall is null,
            "A qualifying ceiling hit restores initial height and cancels upward velocity.");
        assert(rising.Rays.Single() == (new FixedVector3(1000000, -131072, 2000000), new FixedVector3(1000000, -1155071, 2000000)),
            "Ceiling clipping retains original body-clearance and travel offsets after damping.");
        var clearUp = new ScriptedPlayerWorld();
        PlayerTerrainStep upward = PlayerTerrainMotion.Step(frame with { Velocity = new(0, -8192, 0) }, clearUp, policy);
        assert(upward.Movement.Position.Y == -401407 && upward.Movement.Velocity.Y == -8191 && upward.Fall is null,
            "Unobstructed upward movement retains signed damping and skips falling response.");
        var support = new ScriptedPlayerWorld();
        PlayerTerrainMotion.Step(frame with { Velocity = default, PreviousSupportVerticalVelocity = -1 }, support, policy);
        assert(support.Rays.Count == 2, "An upward-moving support requests ceiling clearance even with no player vertical velocity.");
    }
}
