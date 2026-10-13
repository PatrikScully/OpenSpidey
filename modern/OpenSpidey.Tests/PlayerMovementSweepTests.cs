using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerMovementSweepTests
{
    public static void Run(Action<bool, string> assert)
    {
        assert(FixedVectorMath.OriginalSquareRoot(-32768) == 32768 && FixedVectorMath.OriginalSquareRoot(-1) == 0 &&
            FixedVectorMath.OriginalSquareRoot(int.MaxValue) == 46340, "Sweep lengths preserve original square-root boundary behavior.");
        assert(FixedVectorMath.Normalize(default) == new FixedVector3(0, 4096, 0) &&
            FixedVectorMath.Normalize(new(4096, 0, 4096)) == new FixedVector3(2896, 0, 2896),
            "Body normals preserve original Q12 float normalization and its zero-vector fallback.");
        var empty = new ScriptedPlayerWorld();
        PlayerSweepResult vertical = PlayerMovementSweep.Resolve(default, new(0, -4096, 0), 70, 2, false, empty);
        assert(empty.Rays.Count == 0 && vertical.LastHit is null && vertical.Attempts == 0,
            "Vertical-only motion does not use an uninitialized horizontal hit or issue sweeps.");

        var clear = new ScriptedPlayerWorld();
        PlayerSweepResult clearResult = PlayerMovementSweep.Resolve(default, new(4096, 0, 0), 70, 2, false, clear);
        assert(clear.Rays.SequenceEqual(new[] {
            (new FixedVector3(-80640, -262144, 0), new FixedVector3(322560, -262144, 0)),
            (new FixedVector3(-143360, 65536, -35840), new FixedVector3(286720, 65536, -143360)),
            (new FixedVector3(-143360, 65536, 35840), new FixedVector3(286720, 65536, 143360)) }),
            "Body and both shoulder probes preserve their original origins, reach and order.");
        assert(clearResult.Displacement == new FixedVector3(4096, 0, 0) && clearResult.CollisionFlags == 0 && clearResult.LastHit is null,
            "Unobstructed sweeps preserve motion and return no collision.");

        PlayerWorldHit wall = Hit(new(2048, 0, 0), 1);
        var main = new ScriptedPlayerWorld(wall);
        PlayerSweepResult stopped = PlayerMovementSweep.Resolve(default, new(-4096, 0, 0), 70, 2, false, main);
        assert(main.Rays.Count == 1 && stopped.Displacement == default && stopped.BodyHit == wall && stopped.LastHit == wall,
            "Main body hits normalize the horizontal normal and retain the body contact.");

        var sliding = new ScriptedPlayerWorld(wall, null, null, null);
        PlayerSweepResult slide = PlayerMovementSweep.Resolve(default, new(-4096, 0, 8192), 70, 2, false, sliding);
        assert(slide.Displacement == new FixedVector3(0, 0, 2048) && slide.LastHit is null && slide.BodyHit == wall &&
            slide.Attempts == 2 && sliding.Rays.Count == 4, "A clear second sweep permits tangential motion and retains the earlier body hit.");

        var shoulder = new ScriptedPlayerWorld(null, wall, null, null, null);
        PlayerSweepResult shoulderSlide = PlayerMovementSweep.Resolve(default, new(-4096, 0, 8192), 70, 2, false, shoulder);
        assert(shoulderSlide.Displacement == new FixedVector3(-768, 0, 2048) && shoulderSlide.BodyHit is null,
            "Shoulder contacts use raw Q12 normals and do not populate the main body contact.");
        var carried = new ScriptedPlayerWorld(wall, null, null, null);
        PlayerSweepResult carriedResult = PlayerMovementSweep.Resolve(default, new(4096, 0, 0), 70, 2, true, carried);
        assert(carried.Rays.Count == 4 && carried.Rays[0] == (new FixedVector3(0, -786432, 0), new FixedVector3(322560, -786432, 0)) &&
            carriedResult.CollisionFlags == 0x40 && carriedResult.LastHit is null,
            "Carried-object probes run before body probes and preserve their independent collision bit.");
        var carriedWall = new ScriptedPlayerWorld(wall, wall);
        PlayerSweepResult bodyResult = PlayerMovementSweep.Resolve(default, new(-4096, 0, 0), 70, 2, true, carriedWall, 0x80);
        assert(bodyResult.CollisionFlags == 0x81, "A body hit clears the carried-object bit while preserving other collision flags.");
        var repeated = new ScriptedPlayerWorld(wall, wall, wall);
        PlayerSweepResult bounded = PlayerMovementSweep.Resolve(default, new(4096, 0, 0), 70, 2, false, repeated);
        assert(bounded.Attempts == 2 && repeated.Rays.Count == 2 && bounded.LastHit == wall,
            "Repeated hits stop after the recovered two-attempt limit.");
        var rising = new ScriptedPlayerWorld();
        PlayerMovementSweep.Resolve(default, new(4096, -4096, 0), 70, 2, false, rising);
        assert(rising.Rays[1].Start.Y == 65536 && rising.Rays[2].Start.Y == 65536 && rising.Rays[1].End.Y != 65536,
            "Shoulder ray origins remain horizontal during upward motion.");
    }

    private static PlayerWorldHit Hit(FixedVector3 normal, int face) => new(new(default, normal, 1, 0, false), 0, 0, face);
}
