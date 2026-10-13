using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerZipWebTests
{
    private static readonly PlayerButtonInput Zip = new(PlayerButton.Zip, PlayerButton.Zip);

    public static void Run(Action<bool, string> assert)
    {
        var frame = new PlayerZipFrame(new(0, -(96 << 12), 0), new(0, -4096, 0), new(0, -4096, 0), 1, 96);
        PlayerWorldHit anchor = Hit(new(0, -(1000 << 12), 0), new(0, 4096, 0), 904);
        var query = new ScriptedPlayerWorld(anchor);
        assert(PlayerZipWebRules.FindStraightTarget(frame, query) == anchor && query.Rays.Single() ==
            (frame.Position, frame.Position + new FixedVector3(0, -(3072 << 12), 0)),
            "Straight zip selection follows surface up with the recovered reach and source hit identity.");
        foreach (PlayerZipFrame disabled in new[] { frame with { SuppressInput = true }, frame with { HoldingObject = true },
            frame with { Locked = true } })
        {
            var skipped = new ScriptedPlayerWorld(anchor);
            assert(PlayerZipWebRules.FindStraightTarget(disabled, skipped) is null && skipped.Rays.Count == 0,
                "Suppressed, carried-object and locked zip input skips target queries.");
        }
        assert(!PlayerZipWebRules.IsAvailable(frame, anchor with { Contact = anchor.Contact with { Distance = 16 } }, 3072) &&
            !PlayerZipWebRules.IsAvailable(frame, anchor with { Contact = anchor.Contact with { Distance = 3072 } }, 3072),
            "Zip distance bounds are exclusive.");
        assert(PlayerZipWebRules.IsAvailable(frame with { Action = 4 }, anchor with { Contact = anchor.Contact with { Distance = 9 } }, 3072) &&
            !PlayerZipWebRules.IsAvailable(frame with { Action = 4 }, anchor with { Contact = anchor.Contact with { Distance = 8 } }, 3072),
            "The exact falling action uses the recovered shorter minimum range.");
        assert(!PlayerZipWebRules.IsAvailable(frame, anchor with { Contact = anchor.Contact with { SurfaceFlags = 0x40000 } }, 3072),
            "Non-crawlable faces cannot be zip anchors.");
        var clearanceFrame = frame with { Position = default };
        assert(!PlayerZipWebRules.IsAvailable(clearanceFrame, Hit(new(0, 32 << 12, 0), new(0, -4096, 0), 100), 3072) &&
            PlayerZipWebRules.IsAvailable(clearanceFrame, Hit(new(0, 31 << 12, 0), new(0, -4096, 0), 100), 3072),
            "Anchor clearance uses the recovered signed short projection and strict 64-unit threshold.");
        assert(PlayerZipWebRules.HasArrived(new(0, -(873 << 12), 0), anchor) &&
            !PlayerZipWebRules.HasArrived(new(0, -(872 << 12), 0), anchor),
            "Zip arrival accepts a distance below 128 whole units.");
        assert(PlayerZipWebRules.HasArrived(new(0, -(2000 << 12), 0), anchor),
            "Crossing the anchor plane triggers arrival even when distance exceeds the threshold.");
        PlayerZipLanding landing = PlayerZipWebRules.Land(frame.Position, anchor, frame.SurfaceNormal, new(0, 0, -4096), 96);
        assert(landing.Position.Y == -(904 << 12) && landing.OrientationTarget == new FixedVector3(0, 0, -4096),
            "Ceiling arrival preserves horizontal body forward and snaps to original clearance.");
        var tidy = new ScriptedPlayerWorld(Hit(default, new(4096, 0, 0), 1));
        FixedVector3 tidied = PlayerZipWebRules.TidyLanding(default, new(4096, 0, 0), new(0, 0, 4096), 32, tidy);
        assert(tidied == new FixedVector3(32 << 12, 0, 0) && tidy.Rays.Count == 8 &&
            tidy.Rays[0] == (new FixedVector3(32 << 12, 0, 0), new FixedVector3(-(32 << 12), 0, 0)),
            "Landing clearance runs eight surface-local probes and updates the subsequent probe origin after a hit.");

        var world = new Room();
        var animation = new Animation();
        var player = new PlayerTerrainController(frame.Position, 0, world, animation, new(false, new(false, 0, false)))
            { BodyHook = at => at };
        player.Step(default, 2048);
        player.Step(default, 2048, buttons: Zip);
        assert(player.Action == PlayerAction.ZipWebPull && animation.Playback.ClipId == 250 && !player.ZipWebVisible,
            "Ground idle starts the recovered standing web-shot animation before firing the strand.");
        for (int update = 0; update < 30 && animation.Playback.ClipId == 250; update++) player.Step(default, 2048);
        assert(animation.Playback.ClipId == 270 && player.ZipWebVisible,
            "Completed standing fire starts the pull animation and exposes the web effect.");
        for (int update = 0; update < 30 && animation.Playback.Frame < 13; update++) player.Step(default, 2048);
        int queriesBefore = world.Queries;
        FixedVector3 pullBefore = player.Position;
        player.Step(default, 2048);
        assert(world.Queries == queriesBefore && player.Position.Y < pullBefore.Y && !player.ZipWebVisible,
            "The pull window advances source collision-free motion after frame 13 and releases the fired web effect.");
        for (int update = 0; update < 120 && player.Action != PlayerAction.Idle; update++) player.Step(default, 2048);
        assert(player.Action == PlayerAction.Idle && player.OnCeiling && player.IsCrawling && player.IsGrounded &&
            player.Position.Y == -(904 << 12) && animation.Playback.ClipId == 19 && player.Orientation.Basis.M11 == -4096,
            $"Straight zip finishes on the ceiling at crawl idle with complete reversed up: {player.Action}, clip={animation.Playback.ClipId}, position={player.Position}.");
        player.Step(default, 2048, buttons: Zip);
        assert(player.Action == PlayerAction.ZipWebPull && animation.Playback.ClipId == 260,
            "Ceiling idle uses the crouched web-shot animation when zipping back toward the floor.");
        for (int update = 0; update < 180 && player.Action != PlayerAction.Idle; update++) player.Step(default, 2048);
        assert(player.Action == PlayerAction.Idle && !player.IsCrawling && player.IsGrounded &&
            player.Position.Y == -(96 << 12) && animation.Playback.ClipId == 20 && player.Orientation.Basis.M11 == 4096,
            "Returning to the floor rebuilds the complete floor basis and preserves the standing recovery clip.");
        for (int update = 0; update < 24; update++) player.Step(default, 2048);
        assert(animation.Playback.ClipId == 0 && player.Velocity == default, "Floor recovery follows back to ordinary idle.");
        player.Step(default, 2048, buttons: Zip);
        for (int update = 0; update < 60 && !(animation.Playback.ClipId == 270 && animation.Playback.Frame >= 13); update++)
            player.Step(default, 2048);
        player.Step(default, 2048, buttons: new(PlayerButton.Jump, PlayerButton.Jump));
        assert(player.Action == PlayerAction.Falling && player.Velocity == default && !player.IsCrawling,
            "Jump during active pull cancels into the original falling state.");
        player.Step(default, 2048);
        assert(animation.Playback.ClipId == 276, "Cancelled zip starts its release animation on the following fall update.");

        var movingWorld = new Room();
        var movingAnimation = new Animation();
        var moving = new PlayerTerrainController(frame.Position, 0, movingWorld, movingAnimation, new(false, new(false, 0, false)))
            { BodyHook = at => at };
        var forward = new PlayerInputSample(true, new(-127, 0), false, false, false, false, false, false);
        moving.Step(default, 2048);
        moving.Step(forward, 2048);
        moving.Step(forward, 2048, buttons: Zip);
        assert(moving.Action == PlayerAction.ZipWebPull && movingAnimation.Playback.ClipId == 270 &&
            movingAnimation.Playback.Frame == 13 && moving.ZipWebVisible,
            "Zipping from ground movement uses the source airborne fire path, starting the pull at frame 13.");
        var fallingWorld = new Room();
        var fallingAnimation = new Animation();
        var falling = new PlayerTerrainController(frame.Position, 0, fallingWorld, fallingAnimation, new(false, new(false, 0, false)))
            { BodyHook = at => at };
        falling.Step(default, 2048);
        fallingWorld.FloorEnabled = false;
        for (int update = 0; update < 4; update++) falling.Step(default, 2048);
        falling.Step(default, 2048, buttons: Zip);
        assert(falling.Action == PlayerAction.ZipWebPull && fallingAnimation.Playback.ClipId == 270 &&
            fallingAnimation.Playback.Frame == 13 && falling.ZipWebVisible,
            "An airborne falling actor can start the same immediate zip pull without a standing windup.");
    }

    private static PlayerWorldHit Hit(FixedVector3 position, FixedVector3 normal, int distance) =>
        new(new(position, normal, distance, 0, false), 0, 0, 0);

    private sealed class Animation : IPlayerAnimationDriver
    {
        public AnimationPlaybackState Playback { get; private set; } = AnimationPlaybackState.Initial;
        public int Speed { get => Playback.Speed; set => Playback = Playback with { Speed = value }; }
        public void Advance(int ticks) => Playback = ActorAnimationPlayback.Advance(Playback, ticks).State;
        public void Run(int clip, int from = 0, int to = -1) => Playback = ActorAnimationPlayback.Run(Playback, (ushort)clip, 22, from, to);
    }

    private sealed class Room : IPlayerWorldQuery
    {
        public int Queries { get; private set; }
        public bool FloorEnabled { get; set; } = true;
        public PlayerWorldHit? Trace(FixedVector3 start, FixedVector3 end)
        {
            Queries++;
            int plane;
            FixedVector3 normal;
            if (FloorEnabled && start.Y < 0 && end.Y >= 0) { plane = 0; normal = new(0, -4096, 0); }
            else if (start.Y > -(1000 << 12) && end.Y <= -(1000 << 12)) { plane = -(1000 << 12); normal = new(0, 4096, 0); }
            else return null;
            double fraction = ((double)plane - start.Y) / ((double)end.Y - start.Y);
            var position = new FixedVector3((int)(start.X + ((double)end.X - start.X) * fraction), plane,
                (int)(start.Z + ((double)end.Z - start.Z) * fraction));
            return Hit(position, normal, FixedAim.Distance(start, position));
        }
    }
}
