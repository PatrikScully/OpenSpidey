namespace OpenSpidey.Core.World;

[Flags]
public enum CollisionQuery
{
    Player = 0,
    Enemy = 1,
    LineOfSight = 2,
    Camera = 4,
    Trigger = 8
}
