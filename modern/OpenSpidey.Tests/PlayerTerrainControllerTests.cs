using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerTerrainControllerTests
{
    private static readonly PlayerInputSample Forward = new(true, new(-127, 0), false, false, false, false, false, false);

    public static void Run(Action<bool, string> assert)
    {
        var driver = new TestAnimation();
        var player = Create(new Floor(), driver);
        player.Step(default, 2048);
        assert(player.IsGrounded && player.Position.Y == -(96 << 12) && player.Action == PlayerAction.Idle,
            "The first update drops the source actor to its actual support with original clearance.");
        player.Step(Forward, 2048);
        assert(player.Action == PlayerAction.GroundMovement && driver.Playback.ClipId == 1 &&
            player.Velocity.Z > 0 && player.SpeedRamp == 2, "Forward movement begins with its source start clip and speed ramp.");
        FixedVector3 before = player.Position;
        for (int index = 0; index < 30; index++) player.Step(Forward, 2048);
        assert(player.Position.Z > before.Z && player.Position.Y == before.Y && player.IsGrounded &&
            driver.Playback.ClipId == 21 && player.SpeedRamp == 16, "Walking follows onto clip 21 and stays at floor clearance.");
        player.Step(default, 2048);
        assert(player.Action == PlayerAction.Idle && driver.Playback.ClipId is 11 or 12 or 13,
            "Releasing input picks the foot-phase stop clip before returning to idle.");
        for (int index = 0; index < 30; index++) player.Step(default, 2048);
        assert(driver.Playback.ClipId == 0 && player.Velocity == default && player.SpeedRamp == 0,
            "Stop follow-ons return to idle and physics damping removes residual movement.");
        FixedVector3 stopped = player.Position;
        for (int index = 0; index < 10; index++) player.Step(default, 2048);
        assert(player.Position == stopped, "Idle has no drift after damping completes.");

        var edgeDriver = new TestAnimation();
        var edge = Create(new Floor(400 << 12), edgeDriver);
        for (int index = 0; index < 50; index++) edge.Step(Forward, 2048);
        assert(edge.Action == PlayerAction.Falling && edge.Position.Y > -(96 << 12) && edge.Velocity.Y > 0,
            "Walking beyond the real floor edge enters the falling action and accelerates downward.");
        assert(edgeDriver.Playback.ClipId is 212 or 226 or 228,
            "A fall follows the original drop and falling clip chain.");

        var landingDriver = new TestAnimation();
        var landingWorld = new Floor { Enabled = false };
        var landing = Create(landingWorld, landingDriver);
        landing.Step(default, 2048);
        assert(landing.Action == PlayerAction.Falling, "Missing initial ground does not create a fake supporting floor.");
        landingWorld.Enabled = true;
        for (int index = 0; index < 30 && landing.Action == PlayerAction.Falling; index++)
            landing.Step(default, 2048);
        assert(landing.Action == PlayerAction.WallAttached && landingDriver.Playback.ClipId == 213,
            "A falling actor enters the source landing-recovery action and clip.");
        for (int index = 0; index < 30; index++) landing.Step(default, 2048);
        assert(landing.IsGrounded && landing.Action == PlayerAction.Idle && landingDriver.Playback.ClipId == 0,
            "Completed landing recovery returns to a grounded idle.");

        var rollingDriver = new TestAnimation();
        var rolling = Create(new Floor(), rollingDriver);
        rolling.Step(default, 2048);
        rolling.Step(Forward, 0);
        assert(rolling.Action == PlayerAction.Rolling && rollingDriver.Playback.ClipId == 31,
            "Opposite movement from idle uses the original reverse start clip.");
        rolling.Step(Forward, 0);
        assert(rolling.Action == PlayerAction.GroundMovement && rolling.Orientation.Forward.Z > 4000 &&
            Math.Abs(rolling.Orientation.Forward.X) < 64 && rolling.Velocity.Z < 0,
            $"The rolling action reverses the body before resuming movement: {rolling.Action}, heading={rolling.Orientation.Heading}, velocity={rolling.Velocity}.");

        VerifyOrientation(assert);
        var unfinished = AnimationPlaybackState.Initial with { Finished = 0, ClipId = 1 };
        assert(PlayerAnimationFollowOns.Resolve(unfinished, _ => 0) is null,
            "Unfinished clips do not execute their follow-on.");
        assert(PlayerAnimationFollowOns.Resolve(unfinished with { Finished = 1 }, _ => 0) == 21 &&
            PlayerAnimationFollowOns.Resolve(unfinished with { ClipId = 194, Finished = 1 }, _ => 0) == 194,
            "Follow-ons use the stored clip key, including entries moved beyond the source sort range.");
        assert(PlayerAnimationFollowOns.Resolve(unfinished with { ClipId = 50, Finished = 1 }, _ => 0) == 85 &&
            PlayerAnimationFollowOns.Resolve(unfinished with { ClipId = 50, Finished = 1 }, _ => 1) == 51,
            "The wall-walk random branch is injected and preserves both original choices.");
        assert(PlayerAnimationFollowOns.Resolve(unfinished with { ClipId = 58, Finished = 1, Direction = -1 }, _ => 0) == 19,
            "Reversed crawl start finishes at its standing clip.");
        var scriptedDriver = new TestAnimation();
        var scripted = Create(new Floor(), scriptedDriver);
        scripted.Step(Forward, 2048, scriptedControls: () =>
        {
            assert(scripted.Position.Y == -(96 << 12) && scriptedDriver.Playback.Frame != 0,
                "The script callback runs after ground physics and animation advance.");
            scripted.EnterScriptedDelay();
            return new(default, default);
        });
        assert(scripted.Action == PlayerAction.ScriptedDelay && scripted.Velocity == default,
            "The delayed-stand script action preserves physics and blocks locomotion dispatch.");
        scripted.Stand();
        scripted.TeleportFromScript(new(300 << 12, -(150 << 12), 20 << 12));
        assert(scripted.Action == PlayerAction.Idle && scripted.Position == new FixedVector3(300 << 12, -(96 << 12), 20 << 12) &&
            !scripted.IsCrawling && scripted.Velocity == default,
            "Script teleport uses the source's 256-unit floor probe and clears surface locomotion.");
        scripted.TeleportFromScript(new(0, -(400 << 12), 0));
        assert(scripted.Action == PlayerAction.Falling && scripted.Position.Y == -(400 << 12),
            "A script teleport beyond the short ground probe enters falling without snapping to a distant floor.");
        var locked = Create(new Floor(), new TestAnimation());
        locked.Step(default, 2048);
        locked.SetIgnoreInputTimer(4);
        var physicalJump = new PlayerButtonInput(PlayerButton.Jump, PlayerButton.Jump);
        locked.Step(Forward, 2048, buttons: physicalJump);
        assert(locked.Action == PlayerAction.Idle && locked.InputSuppression.RemainingTicks == 2 && locked.Velocity == default,
            "An active input lock clears physical action buttons and movement without suspending player physics.");
        locked.Step(Forward, 2048, buttons: physicalJump);
        assert(locked.Action == PlayerAction.GroundMovement && locked.InputSuppression.RemainingTicks == 0,
            "Exact-zero expiry accepts analog movement but still swallows jump on the expiring update.");
        locked.Step(default, 2048, buttons: physicalJump);
        assert(locked.Action == PlayerAction.JumpWindup, "Physical jump becomes available on the next unlocked update.");
        var scriptLocked = Create(new Floor(), new TestAnimation());
        scriptLocked.Step(default, 2048);
        scriptLocked.SetIgnoreInputTimer(8);
        scriptLocked.Step(default, 2048, scriptedControls: () => new(Forward, default));
        assert(scriptLocked.Action == PlayerAction.GroundMovement && scriptLocked.InputSuppression.RemainingTicks == 8,
            "A live scripted input phase drives locomotion while preserving the physical-input timer.");
    }

    private static PlayerTerrainController Create(Floor world, TestAnimation driver) =>
        new(new(0, -(200 << 12), 0), 0, world, driver, new(false, new(false, 0, false)), _ => 0);

    private static void VerifyOrientation(Action<bool, string> assert)
    {
        var orientation = new PlayerOrientation(0);
        assert(orientation.Heading == 2049 && orientation.Basis.M02 == -17 && orientation.Basis.M22 == -4095,
            "The player constructor preserves the recovered rotation conversion, including its imperfect half-turn.");
        assert(!orientation.Orient(new(0, -4096, 0), 0) && orientation.Basis.M11 == 4096,
            "Ground orientation builds a supported body basis.");
        FixedVector3 previousForward = orientation.Forward;
        assert(!orientation.Orient(new(0, 4096, 0), 0, previousForward) && orientation.Basis.M11 == -4096 &&
            orientation.Forward == previousForward, "Ceiling orientation preserves forward continuity while reversing up.");
        assert(!orientation.Orient(new(0, -4096, 0), 0) && orientation.Basis.M11 == 4096,
            "Returning to the floor preserves a complete basis.");
        assert(orientation.Orient(new(0, -4096, 0), 0, new(0, 4096, 0)) &&
            orientation.Forward == new FixedVector3(0, 0, -4096), "A parallel orientation target falls back to complete cached axes.");
        orientation.Orient(new(0, 4096, 0), 0, new(0, 4096, 0));
        assert(!orientation.Orient(new(0, 4096, 0), 0) && orientation.Basis.M11 == -4096,
            "A degenerate ceiling target invalidates the normal cache so the next update rebuilds the supported basis.");
    }

    private sealed class TestAnimation : IPlayerAnimationDriver
    {
        public AnimationPlaybackState Playback { get; private set; } = AnimationPlaybackState.Initial;
        public int Speed { get => Playback.Speed; set => Playback = Playback with { Speed = value }; }
        public void Advance(int ticks) => Playback = ActorAnimationPlayback.Advance(Playback, ticks).State;
        public void Run(int clip, int from = 0, int to = -1) =>
            Playback = ActorAnimationPlayback.Run(Playback, (ushort)clip, 22, from, to);
    }

    private sealed class Floor(int endZ = int.MaxValue) : IPlayerWorldQuery
    {
        public bool Enabled { get; set; } = true;
        public PlayerWorldHit? Trace(FixedVector3 start, FixedVector3 end)
        {
            if (!Enabled || start.Y >= 0 || end.Y < 0) return null;
            double fraction = -(double)start.Y / ((double)end.Y - start.Y);
            var position = new FixedVector3((int)(start.X + (end.X - start.X) * fraction), 0,
                (int)(start.Z + (end.Z - start.Z) * fraction));
            if (position.Z >= endZ) return null;
            return new(new(position, new(0, -4096, 0), 136, 0, false), 0, 0, 0);
        }
    }
}
