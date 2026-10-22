namespace OpenSpidey.Core.Gameplay;

/// <summary>Process globals shared by level-script water monitors and scene rendering (original 0x5FCDA8 and player mFlags bit 8).</summary>
public sealed class CampaignSceneWaterState
{
    /// <summary>Cached pool surface height in Q12, written while the player is inside an authored water volume.</summary>
    public int SceneWaterLevelY { get; set; }
    /// <summary>Original G_MECHLIST_PLAYER mFlags bit 8, set by l5/l6 level-script monitors each frame.</summary>
    public bool PlayerInWaterVolume { get; set; }
}
