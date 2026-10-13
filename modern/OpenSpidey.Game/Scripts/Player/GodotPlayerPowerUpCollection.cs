using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Player;

/// <summary>Actual player finalizer collection over the scene registry's canonical PowerUp head order.</summary>
public sealed class GodotPlayerPowerUpCollection : IPlayerPowerUpCollectionFrameHost, IPlayerPowerUpCollectionHost
{
    private readonly GodotPlayer player;
    private readonly GodotActorRegistry registry;
    private readonly Action<ushort, int, int> playSound;
    public PlayerPowerUpCollectionState State { get; }
    public IReadOnlyList<IPlayerCollectiblePowerUp> SourcePowerUpOrder => registry.SourceList(ActorUpdateGroup.PowerUp)
        .SourceOrder.OfType<IPlayerCollectiblePowerUp>().ToArray();

    public GodotPlayerPowerUpCollection(GodotPlayer player, GodotActorRegistry registry, PlayerPowerUpCollectionState state,
        Action<ushort, int, int> playSound)
    {
        ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(playSound);
        if (!ReferenceEquals(state, player.PowerUpCollection)) throw new ArgumentException("Collection must use the actual player's fields370/5E0.", nameof(state));
        this.player = player; this.registry = registry; State = state; this.playSound = playSound;
    }
    public void CollectPowerUps() => PlayerPowerUpCollection.Collect(State, player.Controller.Position,
        (uint)player.Controller.Action, this);
    public void PlaySound(ushort sound, int volume, int pitch) => playSound(sound, volume, pitch);
}
