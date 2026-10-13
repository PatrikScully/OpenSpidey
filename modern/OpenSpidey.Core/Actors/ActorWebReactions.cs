namespace OpenSpidey.Core.Actors;

public interface IActorWebReactionHost
{
    bool TryCreateMessage(int code);
    void BurstYankContact();
}

/// <summary>CBaddy::TrapWeb and TugWeb acceptance, message creation and rejected-yank cleanup.</summary>
public static class ActorWebReactions
{
    public static bool TrapWeb(uint behaviorFlags, short health, IActorWebReactionHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if ((behaviorFlags & 0x10000) != 0 || health <= 0) return false;
        host.TryCreateMessage(5);
        return true;
    }

    public static bool TugWeb(uint behaviorFlags, short health, IActorWebReactionHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if ((behaviorFlags & 0x200) != 0 || health <= 0)
        {
            host.BurstYankContact();
            return false;
        }
        host.TryCreateMessage(6);
        return true;
    }
}
