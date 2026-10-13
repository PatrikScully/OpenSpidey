namespace OpenSpidey.Core.Actors.Thugs;

/// <summary>Recovered thug behavior words; their numeric values remain compatible with mission scripts.</summary>
public enum ThugBehaviorState
{
    Initializing = 0,
    Guarding = 1,
    Patrolling = 2,
    Cautious = 3,
    Chasing = 4,
    MeleeAttacking = 5,
    ShootingHostage = 8,
    ShootingPlayer = 9,
    StoppingFire = 10,
    ThrowingGrenade = 11,
    RecoveringFromHit = 12,
    Backpedaling = 13,
    Yanked = 14,
    YankedAlternate = 15,
    WebThrown = 16,
    FlyingFromHit = 17,
    TrappedInWeb = 18,
    WebAttached = 19,
    Grabbed = 20,
    Acknowledging = 21,
    Falling = 22,
    Searching = 23,
    FollowingLastKnownPosition = 24,
    Confused = 25,
    Dying = 26,
    ImmediateDeath = 27,
    ChoosingBehavior = 28
}
