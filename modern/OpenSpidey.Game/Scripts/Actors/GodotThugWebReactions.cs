using OpenSpidey.Core.Actors;

namespace OpenSpidey.Game.Actors;

/// <summary>Binds the shared baddy web checks to the thug's genuine message queue and yank-contact owner.</summary>
public static class GodotThugWebReactions
{
    public static bool TrapWeb(GodotThugActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return ActorWebReactions.TrapWeb(actor.Actor.BehaviorFlags, actor.State.Health, new Host(actor));
    }

    public static bool TugWeb(GodotThugActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return ActorWebReactions.TugWeb(actor.Actor.BehaviorFlags, actor.State.Health, new Host(actor));
    }

    private sealed class Host(GodotThugActor actor) : IActorWebReactionHost
    {
        public bool TryCreateMessage(int code)
        {
            actor.ReceiveMessage(code, null);
            return true;
        }

        public void BurstYankContact() => actor.BurstTugWeb();
    }
}
