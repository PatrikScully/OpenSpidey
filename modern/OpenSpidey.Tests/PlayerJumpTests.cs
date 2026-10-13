using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerJumpTests
{
    private static readonly PlayerButtonInput Held = new(PlayerButton.Jump, PlayerButton.None);
    private static readonly PlayerButtonInput Pressed = new(PlayerButton.Jump, PlayerButton.Jump);

    public static void Run(Action<bool, string> assert)
    {
        var full = PlayerJumpImpulseRules.Advance(new(-245760, 0x70000, 0xA8000), 0, 0, 2, 2);
        assert(full.VerticalVelocity == -245760 && full.Impulse.Phase == 0x60000 && full.Impulse.Hold == 0x98000,
            "Jump finalization applies its source impulse and decays both timers by one Q16 frame.");
        var tail = PlayerJumpImpulseRules.Advance(new(-245760, 32768, 0), 0, 0, 2, 1);
        assert(tail.VerticalVelocity == -122880 && tail.Impulse.Phase == 0,
            "A fractional impulse tail retains source whole-unit quantization.");
        assert(PlayerJumpImpulseRules.Advance(new(-245760, 0, 65536), 100, 0x100, 2, 2).VerticalVelocity == 100,
            "Ceiling collision clears the held impulse before velocity override.");
        var driver = new Animation();
        var player = Create(new World(), driver);
        player.Step(default, 2048);
        player.Step(default, 2048, buttons: Pressed);
        assert(player.Action == PlayerAction.JumpWindup && driver.Playback.ClipId == 210 && driver.Playback.Frame == 4,
            "Standing jump starts the source windup at frame four.");
        for (int update = 0; update < 30 && player.Action == PlayerAction.JumpWindup; update++) player.Step(default, 2048, buttons: Held);
        assert(player.Action == PlayerAction.Jumping && driver.Playback.ClipId == 211 && player.Velocity.Y == -245760,
            "Finished standing windup starts the airborne clip and vertical impulse.");
        int initialY = player.Position.Y;
        for (int update = 0; update < 8; update++) player.Step(default, 2048, buttons: Held);
        assert(player.Position.Y < initialY && !player.IsGrounded, "Held jumping rises above actual support.");
        for (int update = 0; update < 120 && (player.Action != PlayerAction.Idle || !player.IsGrounded); update++) player.Step(default, 2048);
        assert(player.Action == PlayerAction.Idle && player.IsGrounded && player.Position.Y == -(96 << 12),
            "Released jump returns through falling and landing recovery to the original floor clearance.");
        var ceilingDriver = new Animation();
        var ceiling = Create(new World(-200 << 12), ceilingDriver);
        ceiling.Step(default, 2048);
        ceiling.Step(default, 2048, buttons: Pressed);
        for (int update = 0; update < 80 && !ceiling.IsCrawling; update++) ceiling.Step(default, 2048, buttons: Held);
        assert(ceiling.OnCeiling && ceiling.Position.Y == -(104 << 12) && ceilingDriver.Playback.ClipId == 227,
            "Holding jump at a valid ceiling collision starts source ceiling attachment.");
        for (int update = 0; update < 24; update++) ceiling.Step(default, 2048);
        ceiling.Step(default, 2048, buttons: Pressed);
        assert(!ceiling.IsCrawling && ceiling.Action == PlayerAction.Falling && ceiling.Orientation.Basis.M11 == 4096,
            "Jump from a ceiling releases support and restores a complete floor-oriented basis.");
        var movingDriver = new Animation();
        var moving = Create(new World(), movingDriver);
        var forward = new PlayerInputSample(true, new(-127, 0), false, false, false, false, false, false);
        moving.Step(default, 2048);
        moving.Step(forward, 2048);
        moving.Step(forward, 2048, buttons: Pressed);
        assert(moving.Action == PlayerAction.JumpWindup && movingDriver.Playback.ClipId == 223 && movingDriver.Playback.Frame == 5,
            "Ground movement uses the running jump windup from source frame five.");
        var backward = forward with { Analog = new(127, 0) };
        var rollingDriver = new Animation();
        var rolling = Create(new World(), rollingDriver);
        rolling.Step(default, 2048);
        rolling.Step(backward, 2048);
        assert(rolling.Action == PlayerAction.Rolling && rollingDriver.Playback.ClipId == 31,
            "Opposite ground input starts the source roll before jump interruption.");
        rolling.Step(backward, 2048, buttons: Pressed);
        assert(rolling.Action == PlayerAction.JumpWindup && rollingDriver.Playback.ClipId == 210,
            "A jump interrupts an active roll after its body orientation changes.");
        var completedDriver = new Animation();
        var completedRoll = Create(new World(), completedDriver);
        completedRoll.Step(default, 2048);
        completedRoll.Step(backward, 2048);
        completedDriver.Finish();
        completedRoll.Step(backward, 2048, buttons: Pressed);
        assert(completedRoll.Action == PlayerAction.JumpWindup && completedDriver.Playback.ClipId == 210,
            "A roll follow-on still consumes jump instead of returning early.");
    }

    private static PlayerTerrainController Create(World world, Animation animation) =>
        new(new(0, -(96 << 12), 0), 0, world, animation, new(false, new(false, 0, false)), _ => 0) { BodyHook = at => at };
    private sealed class Animation : IPlayerAnimationDriver
    {
        public AnimationPlaybackState Playback { get; private set; } = AnimationPlaybackState.Initial;
        public int Speed { get => Playback.Speed; set => Playback = Playback with { Speed = value }; }
        public void Advance(int ticks) => Playback = ActorAnimationPlayback.Advance(Playback, ticks).State;
        public void Finish() => Playback = Playback with { Frame = Playback.TargetFrame, Finished = 1 };
        public void Run(int clip, int from = 0, int to = -1) => Playback = ActorAnimationPlayback.Run(Playback, (ushort)clip, 22, from, to);
    }
    private sealed class World(int ceiling = int.MinValue) : IPlayerWorldQuery
    {
        public PlayerWorldHit? Trace(FixedVector3 start, FixedVector3 end)
        {
            int plane;
            FixedVector3 normal;
            if (start.Y < 0 && end.Y >= 0) { plane = 0; normal = new(0, -4096, 0); }
            else if (start.Y > ceiling && end.Y <= ceiling) { plane = ceiling; normal = new(0, 4096, 0); }
            else return null;
            double amount = ((double)plane - start.Y) / ((double)end.Y - start.Y);
            var position = new FixedVector3((int)(start.X + ((double)end.X - start.X) * amount), plane,
                (int)(start.Z + ((double)end.Z - start.Z) * amount));
            return new(new(position, normal, FixedAim.Distance(start, position), 0, false), 0, 0, 0);
        }
    }
}
