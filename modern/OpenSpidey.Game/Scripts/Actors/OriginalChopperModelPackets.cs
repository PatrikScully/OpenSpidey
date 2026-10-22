using OpenSpidey.Assets.Models;

namespace OpenSpidey.Game.Actors;

/// <summary>Static hook packet at SpideyPC 0x548F88 (M3dUtils_ReadHooksPacket for CChopper).</summary>
public static class OriginalChopperModelPackets
{
    public static ActorHookPacket Hooks { get; } = new(0, Array.AsReadOnly(new ActorHook[]
    {
        new(0, 0, 0, 3), new(0, 253, -1440, 4), new(0, 10, 0, 0), new(0, -10, 0, 0),
        new(0, 0, 0, 16), new(0, -16, 0, 0), new(0, 22, 0, 0), new(0, -22, 0, 0),
        new(0, 28, 0, 0), new(0, -28, 0, 0)
    }));
}
