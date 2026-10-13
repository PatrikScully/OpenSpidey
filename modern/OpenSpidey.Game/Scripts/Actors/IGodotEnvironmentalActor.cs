using OpenSpidey.Core.Player;
using OpenSpidey.Core.Actors;

namespace OpenSpidey.Game.Actors;

/// <summary>A live member of the original EnvironmentalObjectList, separate from static ENV geometry.</summary>
public interface IGodotEnvironmentalActor
{
    PlayerKickObject KickObject { get; }
    ActorBodyMotion SupportMotion { get; }
}
