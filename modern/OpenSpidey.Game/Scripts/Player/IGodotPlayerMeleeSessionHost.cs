using OpenSpidey.Core.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

/// <summary>Actual session manipulation/environment ownership, alongside the directory's baddy/control actors.</summary>
public interface IGodotPlayerMeleeSessionHost
{
    int Difficulty {get;}
    byte SurfaceTransition {get;}
    bool IgnoreSphereRadius {get;}
    PlayerKickObject? HeldObject {get;}
    IReadOnlyList<PlayerKickObject> SourceEnvironmentalOrder {get;}
    PlayerKickHandle MakeEnvironmentalHandle(PlayerKickObject target);
    object ResolveWorldContact(GodotActorWorldHit hit);
}
