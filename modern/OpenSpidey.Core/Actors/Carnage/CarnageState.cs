namespace OpenSpidey.Core.Actors.Carnage;

/// <summary>Source CCarnage (type 314, module "carnage") constructor fields.</summary>
public sealed class CarnageState(int node, int levelId, int sourceConstructionOrdinal)
{
    public const short Type = 314;
    public const string ModuleName = "carnage";
    public const uint LightAddress = 0x548980;
    public const string ModelName = "carnage";

    public int SourceNode { get; } = node;
    public int LevelId { get; } = levelId;
    public int SourceConstructionOrdinal { get; } = sourceConstructionOrdinal;
    public short Health { get; set; } = 3000;
    public ushort MinorRadius { get; set; } = 160;
    public uint BodyFlags { get; set; } = 0x216;
    public int AiState { get; set; } = 1;
    public int AiSubstate { get; set; }
    public int Field21E { get; set; } = 100;
    public ushort LinkedNode { get; set; }
    public bool IsDead => (BodyFlags & 0x40) != 0;
}
