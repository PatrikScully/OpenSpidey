namespace OpenSpidey.Game.World;

public static class CollisionLayers
{
    public const uint PlayerTerrain = 1;
    public const uint CameraTerrain = 2;
    public const uint LineOfSightTerrain = 4;
    public const uint EnemyTerrain = 8;
    public const uint TriggerZones = 16;
    public const uint Actors = 32;
}
