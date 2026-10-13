using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerCrawlMotionTests
{
    private static readonly FixedVector3 WallNormal = new(4096, 0, 0);
    private static readonly PlayerCrawlFrame Wall = new(new(96 << 12, 0, 0), default, default,
        new(1, 1, 1), 2, 96, WallNormal, WallNormal, new(0, 0, 4096), new(0, 4096, 0), true);

    public static void Run(Action<bool, string> assert)
    {
        var wallWorld = new ScriptedPlayerWorld(Hit(default, WallNormal));
        PlayerCrawlStep resting = Step(Wall, wallWorld);
        assert(resting.Movement.Position == Wall.Position && resting.Movement.Velocity == default &&
            resting.CollisionFlags == 2 && resting.GroundContactGraceUpdates == 4,
            "An idle wall crawl keeps the recovered 96-unit clearance without gravity.");
        assert(wallWorld.Rays.Single() == (new FixedVector3(136 << 12, 0, 0), new FixedVector3(-44 << 12, 0, 0)),
            "Crawl support probes follow surface up, rather than world vertical.");
        assert(resting.ShadowPosition == default(FixedVector3) && resting.SurfaceNormal == WallNormal,
            "Successful crawl contact exposes shadow position and the unmodified raw support normal.");
        PlayerWorldHit previousForward = Hit(default, new(0, 0, -4096));
        PlayerCrawlStep retained = Step(Wall with { PreviousBodyHit = previousForward }, new(Hit(default, WallNormal)));
        assert(retained.BodyHit == previousForward,
            "Idle crawling preserves the previous forward contact because it does not run a new travel probe.");

        var ceiling = Wall with { Position = new(0, 96 << 12, 0), SurfaceNormal = new(0, 4096, 0),
            Up = new(0, 4096, 0), OnWall = false };
        PlayerCrawlStep ceilingStep = Step(ceiling, new(Hit(default, ceiling.SurfaceNormal, 0x1800000, true)));
        assert(ceilingStep.Movement.Position == ceiling.Position && ceilingStep.HardSurface &&
            ceilingStep.UseAlternateBrightness == true && ceilingStep.NotifySupport,
            "Ceiling support preserves clearance and forwards hard-surface, brightness and platform effects.");
        assert(!ceilingStep.CeilingControlsChanged && resting.UseAlternateBrightness == false,
            "A stable ceiling does not request a control transition; ordinary support restores brightness.");
        PlayerCrawlStep transition = Step(Wall, new(Hit(default, new(0, 4096, 0))));
        assert(transition.CeilingControlsChanged && transition.SurfaceNormal.Y == 4096 &&
            transition.Movement.Position == new FixedVector3(0, 96 << 12, 0),
            "Entering a positive-vertical surface requests a control transition and adjusts clearance.");

        PlayerCrawlStep subpixel = Step(Wall with { Position = new((96 << 12) + 8191, 0, 0) },
            new(Hit(default, WallNormal)));
        PlayerCrawlStep correction = Step(Wall with { Position = new((96 << 12) + 12288, 0, 0) },
            new(Hit(default, WallNormal)));
        assert(subpixel.Movement.Position.X == (96 << 12) + 8191 && correction.Movement.Position == Wall.Position,
            "Idle support correction uses quantized squared distance with the original greater-than-eight threshold.");

        PlayerCrawlFrame moving = Wall with { Velocity = new(0, 0, 65536) };
        FixedVector3 hookPosition = default;
        var unobstructed = new ScriptedPlayerWorld(null, null, null, null, Hit(new(0, 0, 32768), WallNormal));
        PlayerCrawlStep travel = PlayerCrawlMotion.Step(moving, unobstructed, position => hookPosition = position);
        assert(hookPosition == Wall.Position + new FixedVector3(0, 0, 32768) &&
            travel.Movement.Position == hookPosition && travel.Movement.Velocity.Z == 32768,
            "The body hook is evaluated after velocity damping and movement, before crawl probes.");
        assert(unobstructed.Rays.Count == 5 && unobstructed.Rays[0] ==
            (hookPosition + new FixedVector3(0, 0, 24 << 12), hookPosition - new FixedVector3(0, 0, 96 << 12)),
            "Unobstructed crawl runs forward, both sides, corner and surface probes in order.");

        var aligned = new ScriptedPlayerWorld(Hit(default, WallNormal), Hit(default, WallNormal));
        PlayerCrawlStep rejected = Step(moving, aligned);
        assert(aligned.Rays.Count == 2 && rejected.BodyHit is null && rejected.CollisionFlags == 2,
            "A forward hit on the same surface skips side probes and is discarded.");
        var obstacle = new ScriptedPlayerWorld(Hit(default, new(0, 0, -4096), distance: 20), Hit(default, WallNormal));
        PlayerCrawlStep blocked = Step(moving, obstacle);
        assert(blocked.CollisionFlags == 3 && blocked.BodyHit is not null && blocked.ForwardClearance == 68 &&
            blocked.Movement.Position == Wall.Position,
            "An inside corner blocks travel and reports the source forward clearance.");
        PlayerCrawlStep distant = Step(moving, new(Hit(default, new(0, 0, -4096), distance: 80), Hit(default, WallNormal)));
        assert(distant.ForwardClearance == 0, "Forward hits at distance 80 stop requesting extra clearance.");
        PlayerCrawlStep restricted = Step(moving, new(Hit(default, new(0, 0, -4096), 0x40000), Hit(default, WallNormal)));
        assert(restricted.BodyHit is null && restricted.CollisionFlags == 3,
            "Non-crawlable forward faces retain blocking collision while clearing transition targets.");
        PlayerCrawlStep underside = Step(moving, new(Hit(default, new(0, -4096, 0), 0x40000), Hit(default, WallNormal)));
        assert(underside.BodyHit is not null, "A wall crawler retains a non-crawlable upward-facing corner target.");

        var cornerWorld = new ScriptedPlayerWorld(null, Hit(default, new(0, -4096, 0)),
            Hit(default, new(0, 0, -4096), distance: 32), Hit(default, WallNormal));
        PlayerCrawlStep corner = Step(moving, cornerWorld);
        assert(cornerWorld.Rays.Count == 4 && corner.ExteriorCorner && corner.CornerDistance == 24 &&
            corner.BodyHit is not null && corner.Movement.Position == Wall.Position,
            "A side collision skips the opposite side probe and an exterior corner retains its transition distance.");
        PlayerCrawlStep closeCorner = Step(moving, new(null, null, null,
            Hit(default, new(0, 0, -4096), distance: 16), Hit(default, WallNormal)));
        assert(closeCorner.ExteriorCorner && closeCorner.CornerDistance == 0,
            "A corner within sixteen units needs no additional approach distance.");

        PlayerCrawlStep nonCrawl = Step(moving, new(Hit(default, WallNormal), Hit(default, WallNormal, 0x840000)));
        assert(nonCrawl.Movement.Position == Wall.Position && nonCrawl.SurfaceNormal == WallNormal &&
            nonCrawl.GroundContactGraceUpdates == 4 && nonCrawl.ShadowPosition is null && nonCrawl.UseAlternateBrightness == true,
            "A non-crawlable support restores previous position but still updates contact grace and brightness.");
        PlayerCrawlStep fence = Step(moving, new(Hit(default, WallNormal), Hit(default, WallNormal, 0x8040000)));
        assert(fence.Fence && fence.Movement.Position.Z == 32768 && fence.CollisionFlags == 2,
            "A non-crawlable fence reports its special effect without rolling back travel.");

        var recoveryWorld = new ScriptedPlayerWorld(null, Hit(new(0, 0, 100), new(0, 0, -4096)), Hit(default, WallNormal));
        PlayerCrawlStep recovery = Step(Wall with { GroundContactGraceUpdates = 2 }, recoveryWorld);
        assert(recoveryWorld.Rays.Count == 3 && recovery.ExteriorCorner && recovery.RecoveryCornerHit is not null &&
            recovery.CollisionFlags == 2 && recovery.GroundContactGraceUpdates == 2 && recovery.ShadowPosition is null,
            "Missing support restores position and stops after recovery, even when the final ground probe hits.");
        assert(recovery.BodyHit is null && recovery.SurfaceNormal == WallNormal && recovery.UseAlternateBrightness is null,
            "Recovery exposes its partial corner update separately without fabricating a complete forward contact.");
        var emptyWorld = new ScriptedPlayerWorld();
        PlayerCrawlStep unsupported = Step(Wall, emptyWorld);
        assert(unsupported.CollisionFlags == 0 && unsupported.Movement.Position == Wall.Position && emptyWorld.Rays.Count == 3,
            "Failed recovery clears the support collision and retains the previous position.");
        PlayerCrawlStep forbiddenRecovery = Step(Wall, new(null, Hit(default, WallNormal, 0x40000), null));
        assert(!forbiddenRecovery.ExteriorCorner && forbiddenRecovery.RecoveryCornerHit is null,
            "A non-crawlable recovery face cannot become an exterior transition target.");
    }

    private static PlayerCrawlStep Step(PlayerCrawlFrame frame, ScriptedPlayerWorld world) =>
        PlayerCrawlMotion.Step(frame, world, position => position);

    private static PlayerWorldHit Hit(FixedVector3 position, FixedVector3 normal, uint flags = 0,
        bool support = false, int distance = 40) => new(new(position, normal, distance, flags, support), 1, 2, 3);
}
