using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.Player;

/// <summary>Real hand-hook CSmokeTrail owners sharing the same source NonRendered/ribbon phase lists as the foot trails.</summary>
public sealed class GodotPlayerHandTrails : IPlayerHandTrailHost
{
    private readonly GodotPlayer player;
    private readonly GodotSmokeTrails renderer;
    public PlayerHandTrailState State { get; }
    internal GodotPlayer Player => player;
    public GodotPlayerHandTrails(GodotPlayer player, GodotSmokeTrails renderer, uint colour)
    {
        ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(renderer);
        this.player = player; this.renderer = renderer; State = new() { Colour = colour };
        if (player.Melee is null) throw new InvalidOperationException("Source hand trails require the genuine posed player hooks.");
    }
    public void Update() => PlayerHandTrails.Update(State, this, player.Controller.Position,
        player.Model.Playback.ClipId, unchecked((short)player.Model.Playback.Frame));
    public bool Present(int slot) => State.Current[slot] is not null;
    public void Fade(int slot)
    {
        if (State.Current[slot] is { } owner) Require(owner).FadeAway = 1;
        State.Current[slot] = null;
    }
    public FixedVector3 HookPosition(int hook) => player.Melee!.GetPlayerHookPosition(hook);
    public object CreateTrail(FixedVector3 position, int count, int red, int green, int blue) => renderer.Emit(position, count, red, green, blue);
    public void DeleteTrail(object trail) => renderer.Remove(Require(trail));
    public void SetTrailPosition(object trail, FixedVector3 position) => Require(trail).SetPosition(position);
    private static SmokeTrail Require(object trail) => trail as SmokeTrail ?? throw new InvalidOperationException("Hand trail callback received another source particle family.");
}
