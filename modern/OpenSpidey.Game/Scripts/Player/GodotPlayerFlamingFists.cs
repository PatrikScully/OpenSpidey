using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.Player;

/// <summary>Source flaming-fist presentation over the shared real CEmber Flat owner, without another RNG stream or particle phase.</summary>
public sealed class GodotPlayerFlamingFists
{
    private readonly GodotPlayer player;
    private readonly GodotFireEmbers embers;
    private readonly Func<int, int> random;

    public GodotPlayerFlamingFists(GodotPlayer player, GodotFireEmbers embers, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(embers); ArgumentNullException.ThrowIfNull(random);
        if (player.Melee is null || player.Inventory is null)
            throw new InvalidOperationException("Flaming fists require the actual posed player and shared web inventory.");
        this.player = player; this.embers = embers; this.random = random;
    }

    public void FinishFrame(in PlayerHandHooks hands) => PlayerFlamingFists.Emit(player.Inventory!.State.FireWebbing,
        player.Combat.State.Field5AC, player.Model.Playback.ClipId, hands.Left, hands.Right, random,
        (position, scale) => embers.Emit(position, scale, random));
}
