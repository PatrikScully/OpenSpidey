using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerWallAttachmentTests
{
    public static void Run(Action<bool, string> assert)
    {
        var contact = new PlayerGroundContact(new(100 << 12, -200 << 12, 30 << 12), new(-4096, 0, 0), 20, 0, false);
        var hit = new PlayerWorldHit(contact, 1, 2, 3);
        var frame = new PlayerWallAttachmentFrame(new(80 << 12, -200 << 12, 30 << 12), 4, 1, hit, 96, 212);
        var free = new ScriptedPlayerWorld();
        PlayerWallAttachment? attach = PlayerWallAttachmentRules.Resolve(frame, free);
        assert(attach == new PlayerWallAttachment(new(4 << 12, -200 << 12, 30 << 12), contact.Normal, 227),
            "Falling wall attachment preserves original contact clearance and selects the normal attach animation.");
        assert(free.Rays.Single() == (frame.Position, frame.Position + new FixedVector3(0, 180 << 12, 0)),
            "Attachment checks for nearby ground along the recovered 180-unit downward segment.");
        assert(PlayerWallAttachmentRules.Resolve(frame with { AnimationId = 232 }, new ScriptedPlayerWorld())?.AnimationId == 234,
            "The special falling clip selects its distinct wall attachment animation.");
        foreach (PlayerWallAttachmentFrame rejected in new[] { frame with { Action = 1 }, frame with { CollisionFlags = 2 },
            frame with { BodyHit = null }, frame with { BodyHit = hit with { Contact = contact with { SurfaceFlags = 0x40000 } } },
            frame with { BodyHit = hit with { Contact = contact with { Normal = new(0, 3401, 0) } } },
            frame with { BodyHit = hit with { Contact = contact with { Normal = new(0, -2601, 0) } } } })
        {
            var world = new ScriptedPlayerWorld();
            assert(PlayerWallAttachmentRules.Resolve(rejected, world) is null && world.Rays.Count == 0,
                "Ineligible actions, shoulder-only collision, restricted faces and vertical normals skip attachment queries.");
        }
        foreach (int boundary in new[] { -2600, 3400 })
            assert(PlayerWallAttachmentRules.Resolve(frame with { BodyHit = hit with {
                Contact = contact with { Normal = new(-3000, boundary, 0) } } }, new ScriptedPlayerWorld()) is not null,
                "Wall-normal thresholds are inclusive at both recovered limits.");
        assert(PlayerWallAttachmentRules.Resolve(frame, new ScriptedPlayerWorld(hit)) is null,
            "A nearby surface rejects attachment regardless of its surface normal.");
        assert(PlayerWallAttachmentRules.Resolve(frame, new ScriptedPlayerWorld(hit with {
            Contact = contact with { Position = contact.Position with { Y = -1 } } })) is not null,
            "The recovered height sentinel remains distinct from ordinary nearby contact.");
    }
}
