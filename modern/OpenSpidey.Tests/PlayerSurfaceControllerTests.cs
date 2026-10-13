using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerSurfaceControllerTests
{
    private static readonly PlayerInputSample Forward = new(true, new(-127, 0), false, false, false, false, false, false);

    public static void Run(Action<bool, string> assert)
    {
        var wall = new Plane(0, new(4096, 0, 0));
        var animation = new Animation();
        var player = Create(new(160 << 12, -500 << 12, 0), wall, animation);
        for (int update = 0; update < 8 && !player.IsCrawling; update++) player.Step(Forward, 1024);
        assert(player.IsCrawling && player.OnWall && !player.OnCeiling && player.Action == PlayerAction.Idle &&
            player.Position.X == 96 << 12 && player.Velocity == default && animation.Playback.ClipId == 227,
            "A falling body collision starts the recovered wall attachment and clears movement.");
        for (int update = 0; update < 24; update++) player.Step(default, 0);
        assert(player.IsCrawling && player.IsGrounded && player.LastCrawlStep?.GroundHit is not null &&
            animation.Playback.ClipId == 19 && player.Velocity == default,
            $"Wall attachment follows onto crawl idle and reacquires real support without gravity: crawling={player.IsCrawling}, grounded={player.IsGrounded}, clip={animation.Playback.ClipId}, velocity={player.Velocity}, action={player.Action}.");
        FixedVector3 before = player.Position;
        player.Step(Forward, 2048);
        assert(player.Action == PlayerAction.GroundMovement && animation.Playback.ClipId == 57 && player.Velocity.Y < 0,
            "Aligned wall input starts the soft crawl clip and travels along the body basis.");
        for (int update = 0; update < 24; update++) player.Step(Forward, 2048);
        assert(player.Position.Y < before.Y && player.Position.X == before.X && player.IsGrounded &&
            animation.Playback.ClipId == 50 && player.SpeedRamp == 16,
            "Wall walking reaches the crawl cycle, preserves clearance and ignores camera yaw.");
        player.Step(default, 0);
        assert(player.Action == PlayerAction.Idle && animation.Playback.ClipId == 55,
            "Releasing wall-walk input chooses its source stop clip.");
        for (int update = 0; update < 24; update++) player.Step(default, 0);
        assert(animation.Playback.ClipId == 19 && player.Velocity == default && player.IsGrounded,
            "The crawl stop follows back to a stable wall idle.");
        wall.Enabled = false;
        for (int update = 0; update < 4; update++) player.Step(default, 0);
        assert(!player.IsCrawling && player.Action == PlayerAction.Falling && player.Orientation.Up == new FixedVector3(0, -4096, 0),
            "Expired surface grace detaches the crawler and restores the free-fall body orientation.");
        player.Step(default, 0);
        assert(player.Velocity.Y > 0, "Detached actors return to downward gravity.");

        var hardWall = new Plane(0, new(4096, 0, 0)) { Flags = 0x1000000 };
        var hardAnimation = new Animation();
        var hard = Create(new(96 << 12, -500 << 12, 0), hardWall, hardAnimation);
        hard.AttachToSurface(new(hard.Position, new(4096, 0, 0), 227));
        for (int update = 0; update < 24; update++) hard.Step(default, 0);
        hard.Step(Forward, 0);
        assert(hard.HardSurface && hardAnimation.Playback.ClipId == 58,
            "Hard native surface metadata selects the alternate crawl start.");
        for (int update = 0; update < 24; update++) hard.Step(Forward, 0);
        assert(hardAnimation.Playback.ClipId == 52, "Hard crawl start follows onto its own walk cycle.");
        hard.Step(default, 0);
        assert(hardAnimation.Playback.ClipId == 56, "Hard crawl walking selects the alternate stop clip.");

        var ceilingWorld = new Plane(1, new(0, 4096, 0));
        var ceilingAnimation = new Animation();
        var ceiling = Create(new(0, 96 << 12, 0), ceilingWorld, ceilingAnimation);
        FixedVector3 initialForward = ceiling.Orientation.Forward;
        ceiling.AttachToSurface(new(ceiling.Position, new(0, 4096, 0), 227));
        for (int update = 0; update < 24; update++) ceiling.Step(default, 2048);
        assert(ceiling.OnCeiling && !ceiling.OnWall && ceiling.IsGrounded &&
            ceiling.Position.Y == 96 << 12 && ceiling.Orientation.Forward == initialForward,
            "Ceiling attachment reverses up while retaining the previous body forward axis and clearance.");
        FixedVector3 ceilingBefore = ceiling.Position;
        for (int update = 0; update < 25; update++) ceiling.Step(Forward, 2048);
        assert(ceiling.Position.Z > ceilingBefore.Z && ceiling.Position.Y == ceilingBefore.Y && ceiling.IsGrounded &&
            ceilingAnimation.Playback.ClipId == 50,
            "Ceiling crawling uses camera-relative horizontal velocity at the recovered crawl scale.");
        var partialAnimation = new Animation();
        var partial = Create(new(96 << 12, -500 << 12, 0), new Plane(0, new(4096, 0, 0)), partialAnimation);
        partial.AttachToSurface(new(partial.Position, new(4096, 0, 0), 227));
        for (int update = 0; update < 24; update++) partial.Step(default, 0);
        partial.Step(Forward, 0);
        partial.Step(Forward, 0);
        partial.Step(default, 0);
        assert(partialAnimation.Playback.ClipId == 57 && partialAnimation.Playback.Direction == -1 && partial.Action == PlayerAction.Idle,
            "Releasing input during crawl start plays its current frame back toward frame zero.");
    }

    private static PlayerTerrainController Create(FixedVector3 position, Plane world, Animation animation) =>
        new(position, 0, world, animation, new(false, new(false, 0, false)), _ => 0) { BodyHook = at => at };

    private sealed class Animation : IPlayerAnimationDriver
    {
        public AnimationPlaybackState Playback { get; private set; } = AnimationPlaybackState.Initial;
        public int Speed { get => Playback.Speed; set => Playback = Playback with { Speed = value }; }
        public void Advance(int ticks) => Playback = ActorAnimationPlayback.Advance(Playback, ticks).State;
        public void Run(int clip, int from = 0, int to = -1) => Playback = ActorAnimationPlayback.Run(Playback, (ushort)clip, 22, from, to);
    }

    private sealed class Plane(int axis, FixedVector3 normal) : IPlayerWorldQuery
    {
        public bool Enabled { get; set; } = true;
        public uint Flags { get; set; }
        public PlayerWorldHit? Trace(FixedVector3 start, FixedVector3 end)
        {
            int from = axis == 0 ? start.X : start.Y, to = axis == 0 ? end.X : end.Y;
            if (!Enabled || from <= 0 || to > 0) return null;
            double fraction = -(double)from / ((double)to - from);
            var position = new FixedVector3((int)(start.X + ((double)end.X - start.X) * fraction),
                (int)(start.Y + ((double)end.Y - start.Y) * fraction), (int)(start.Z + ((double)end.Z - start.Z) * fraction));
            int distance = (int)(Math.Sqrt(Math.Pow(((double)end.X - start.X) / 4096, 2) +
                Math.Pow(((double)end.Y - start.Y) / 4096, 2) + Math.Pow(((double)end.Z - start.Z) / 4096, 2)) * fraction);
            return new(new(position, normal, distance, Flags, false), 0, 0, 0);
        }
    }
}
