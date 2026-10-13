using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Combat;

namespace OpenSpidey.Core.Actors;

public interface IPlatformDamageHost
{
    void Die();
    int Random(int range);
    void PlayBreakSound(int sound);
    void Shatter(bool checkSeen, bool markProcessed);
}

/// <summary>Original CPlatform::Hit damage widths and ordered death, sound and shatter calls.</summary>
public static class PlatformDamage
{
    public static bool Hit(ActorScriptVariables variables, ActorHit hit, IPlatformDamageHost host)
    {
        variables.ByteParameter = unchecked((byte)(variables.ByteParameter + 1));
        if (variables.Attributes[1] == 0 || variables.Health <= 0) return true;

        variables.Health = unchecked((short)(variables.Health - hit.Damage));
        if (variables.Health > 0) return true;

        host.Die();
        host.PlayBreakSound(host.Random(2) + 1);
        host.Shatter(checkSeen: false, markProcessed: true);
        variables.ItemFlags |= 1;
        return true;
    }
}
