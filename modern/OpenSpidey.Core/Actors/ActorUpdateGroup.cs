namespace OpenSpidey.Core.Actors;

/// <summary>Original WorldSimulation list order after the player and before suspended-actor cleanup.</summary>
public enum ActorUpdateGroup
{
    PowerUp,
    Bullet,
    Miscellaneous,
    Environment,
    Background,
    Enemy,
    Control
}
