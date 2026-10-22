namespace OpenSpidey.Core.Actors.SuperOck;

/// <summary>Source CSuperDocOck (type 309, module "superock") constructor fields.</summary>
public sealed class SuperOckState(int node, int levelId, int sourceConstructionOrdinal)
{
    public const short Type = 309;
    public const string ModuleName = "superock";
    public const uint LightAddress = 0x557928;
    public const string ModelName = "superock";

    public int SourceNode { get; } = node;
    public int LevelId { get; } = levelId;
    public int SourceConstructionOrdinal { get; } = sourceConstructionOrdinal;
    public short Health { get; set; } = 500;
    public ushort MinorRadius { get; set; }
    public uint BodyFlags { get; set; } = 0x206;
    public int AiState { get; set; } = 1;
    public int AiSubstate { get; set; }
    public int Field21E { get; set; } = 100;
    public bool IsDead => (BodyFlags & 0x40) != 0;
}
