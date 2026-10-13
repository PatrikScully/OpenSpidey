using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public interface IPlayerCollectiblePowerUp
{
    ushort Type { get; }
    ushort BodyFlags { get; }
    ushort Radius { get; }
    FixedVector3 Position { get; }
    byte FailedCollectionFlash { get; set; }
    byte TakeEffect();
}

public sealed class PlayerPowerUpCollectionState
{
    public byte PickupJingleReady { get; set; } // Source player370; the first empty sweep arms it.
    public uint LastHealthPickupClock { get; set; } // Source player5E0; web/cartridge timing remains separate.
}

public interface IPlayerPowerUpCollectionHost
{
    IReadOnlyList<IPlayerCollectiblePowerUp> SourcePowerUpOrder { get; }
    void PlaySound(ushort sound, int volume, int pitch);
}

public interface IPlayerPowerUpCollectionFrameHost
{
    void CollectPowerUps();
}

/// <summary>Source4B7A74: stops at the first live nearby body, including an unavailable node pulse or a rejected effect.</summary>
public static class PlayerPowerUpCollection
{
    public static IPlayerCollectiblePowerUp? Collect(PlayerPowerUpCollectionState state, FixedVector3 playerPosition,
        uint playerAction, IPlayerPowerUpCollectionHost host)
    {
        foreach (IPlayerCollectiblePowerUp pickup in host.SourcePowerUpOrder)
        {
            if ((pickup.BodyFlags & 0x40) != 0 || unchecked((int)ActorLifecycle.Distance(playerPosition, pickup.Position)) >= pickup.Radius) continue;
            if (pickup.Type == 11)
            {
                if ((playerAction & 0x11) != 0) pickup.TakeEffect();
            }
            else if (pickup.TakeEffect() == 0)
            {
                pickup.FailedCollectionFlash = 1;
                if (state.PickupJingleReady != 0 && (pickup.Type == 8 || pickup.Type is > 13 and <= 16))
                {
                    host.PlaySound(0x28, 0x2000, 0);
                    state.PickupJingleReady = 0;
                }
            }
            return pickup;
        }
        state.PickupJingleReady = 1;
        return null;
    }
}
