using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerWallMovementTests
{
    public static void Run(Action<bool, string> assert)
    {
        var orientation = new PlayerOrientation(0);
        orientation.Orient(new(4096, 0, 0), 0, new(0, 4096, 0));
        assert(orientation.Up == new FixedVector3(4096, 0, 0) &&
            orientation.Right == new FixedVector3(0, 0, -4096) && orientation.Forward == new FixedVector3(0, 4096, 0),
            "Wall axes expose the surface normal, right axis and forward axis with the recovered signs.");
        assert(orientation.GetEffectiveHeading(true) == 0 && orientation.GetEffectiveHeading(false) == orientation.Heading,
            "Effective wall heading measures local orientation against world vertical.");
        FixedJointTransform basis = orientation.Basis;
        var frame = new PlayerFlatMovementFrame(new(65, 129, 193), new(-127, 0), 0, 0, 3072,
            16, 16, 50, 1, Crawling: true);
        PlayerFlatMovementResult forward = PlayerWallMovement.Resolve(frame, basis);
        assert(forward.Velocity == new FixedVector3(64, -106496, 192) && forward.AnimationSpeed == 65536,
            "Wall movement changes local forward speed while preserving quantized normal and sideways velocity.");
        assert(PlayerWallMovement.Resolve(frame with { CameraHeading = 1024 }, basis) == forward,
            "Wall input does not depend on the following camera's yaw.");
        PlayerFlatMovementResult reverse = PlayerWallMovement.Resolve(frame with { InputHeading = 2048 }, basis);
        assert(reverse == forward, "Opposite wall input uses absolute heading alignment while body turning supplies direction.");
        PlayerFlatMovementResult perpendicular = PlayerWallMovement.Resolve(frame with { InputHeading = 1024 }, basis);
        assert(perpendicular.Velocity == new FixedVector3(64, -704, 192),
            $"Perpendicular input retains only the recovered trigonometric table's small rounding drift: {perpendicular.Velocity}.");
        PlayerFlatMovementResult half = PlayerWallMovement.Resolve(frame with { Axes = new(-32, 0) }, basis);
        assert(half.Velocity.Y == -53248 && half.AnimationSpeed == 32768,
            "Half magnitude scales crawl movement and the three wall-walk animation clocks.");
        PlayerFlatMovementResult small = PlayerWallMovement.Resolve(frame with { Axes = new(-1, 0) }, basis);
        assert(small.AnimationSpeed == 32768 && small.Velocity.Y == -1664,
            "Small crawl input retains movement below the minimum animation playback rate.");
        assert(PlayerWallMovement.Resolve(frame with { AnimationId = 19, AnimationSpeed = 12345 }, basis).AnimationSpeed == 12345,
            "Other animation clocks are not rescaled by wall walking.");
        assert(PlayerWallMovement.Resolve(frame with { Axes = default }, basis).Velocity == new FixedVector3(64, 0, 0),
            "Centered input clears local tangential movement while retaining surface-normal motion.");
        foreach (PlayerFlatMovementFrame blocked in new[] { frame with { Action = 1 }, frame with { Lookaround = true },
            frame with { MovementEnabled = false }, frame with { Axes = default, Action = 4 } })
            assert(PlayerWallMovement.Resolve(blocked, basis) == new PlayerFlatMovementResult(blocked.Velocity, blocked.AnimationSpeed),
                "Suppressed finalization preserves the original world velocity without a quantizing round trip.");
        assert(PlayerWallMovement.Resolve(frame with { SuppressMovement = true }, basis) == forward,
            "The source's free-plane suppression flag does not suppress the separate wall branch.");
        assert(PlayerWallMovement.Resolve(frame with { Crawling = false }, basis).Velocity.Y == -163840 &&
            PlayerWallMovement.Resolve(frame with { Crawling = false, HoldingObject = true }, basis).Velocity.Y == -98304 &&
            PlayerWallMovement.Resolve(frame with { Crawling = false, HoldingObject = true, SmallHeldObject = true }, basis).Velocity.Y == -40960,
            "The wall branch retains ordinary and carried-object scales when those policies are selected.");
        var identity = new FixedJointTransform(4096, 0, 0, 0, 4096, 0, 0, 0, 4096, 99, 88, 77);
        assert(FixedRotation.Transform(identity, new(524288, -524289, 7)) == new FixedVector3(-524288, 524287, 7),
            "Q12 transforms preserve native 32-bit overflow and ignore translation fields.");
        assert(FixedRotation.TransformTranspose(basis, new(64, -106496, 192)) == new FixedVector3(-192, -64, -106496),
            "The transpose moves world velocity back into the exact wall basis.");
        VerifyHooks(assert, identity with { X = 0, Y = 0, Z = 0 }, basis);
    }

    private static void VerifyHooks(Action<bool, string> assert, FixedJointTransform identity, FixedJointTransform wall)
    {
        var position = new FixedVector3(100 << 12, -100 << 12, 50 << 12);
        FixedJointTransform translatedPart = identity with { X = 16, Y = 32, Z = -16 };
        assert(PlayerBodyHook.Evaluate(position, position, identity, translatedPart) ==
            position + new FixedVector3(4096, 8192, -4096),
            "Body hooks contribute one sixteenth of the cached part offset to the current root position.");
        assert(PlayerBodyHook.Evaluate(position + new FixedVector3(65536, 0, 0), position, identity, identity) ==
            position + new FixedVector3(61440, 0, 0),
            "A root movement is smoothed against the previously built transform instead of replacing its translation.");
        var fractional = new FixedVector3(100, -100, 5000);
        assert(PlayerBodyHook.Evaluate(fractional, fractional, identity, identity) == new FixedVector3(93, -350, 4943),
            "Previous root translation is quantized before hook smoothing, including signed floor rounding.");
        assert(PlayerBodyHook.Evaluate(position, position, wall, translatedPart) ==
            position + new FixedVector3(-8192, -4096, -4096),
            "Hooks rotate cached part translations into a wall-oriented body basis.");
        FixedJointTransform largePart = identity with { X = 32760 };
        assert(PlayerBodyHook.Evaluate(default, default, identity, largePart, offsetX: 20).X == -8385536,
            "Part-local hook translation wraps to a signed halfword before the world transform.");
        assert(PlayerBodyHook.Evaluate(position, position, wall, translatedPart, mirrored: true) ==
            position + new FixedVector3(-8192, -4096, 4096),
            "Mirrored hooks flip only the body right column without mutating the caller's basis.");
    }
}
