using OpenSpidey.Assets.Models;

namespace OpenSpidey.Game.Actors;

/// <summary>Source static pose/hook packets, verified against private PC data addresses by reference fixtures.</summary>
public static class OriginalThugModelPackets
{
    private const string ThugLinks = "000010000000ffff00000000000000000700000000000000000000000800070000000000000000000200070000000000000000000100020000000000000000000300010000000000000000000400030000000000000000000600070000000000000000000500060000000000000000000900050000000000000000000a00000000000000000000000b000a0000000000000000000c000b0000000000000000000d00000000000000000000000e000d0000000000000000000f000e000000000000000000";
    private const string HenchmanLinks = "00000f000000ffff00000000000000000c00000000000000000000000d000c0000000000000000000b000c0000000000000000000a000b0000000000000000000e000a00000000000000000008000c000000000000000000070008000000000000000000090007000000000000000000040000000000000000000000050004000000000000000000060005000000000000000000010000000000000000000000020001000000000000000000030002000000000000000000";

    public static ActorPoseLinkPacket PoseLinks(int actorType) => actorType switch
    {
        304 => ActorPoseLinkPacket.Read(Convert.FromHexString(ThugLinks), 16),
        312 => ActorPoseLinkPacket.Read(Convert.FromHexString(HenchmanLinks), 15),
        _ => throw new NotSupportedException($"Actor type {actorType} has no original thug pose packet.")
    };

    public static ActorHookPacket Hooks(int actorType) => actorType switch
    {
        304 => new(0, Array.AsReadOnly(new ActorHook[] { new(0, 200, 0, 9), new(0, -150, -240, 3) })),
        312 => new(0, Array.AsReadOnly(new ActorHook[] { new(-100, 0, -350, 7), new(0, 0, -380, 9),
            new(100, 0, -350, 10), new(0, 0, -380, 14) })),
        _ => throw new NotSupportedException($"Actor type {actorType} has no original thug hook packet.")
    };
}
