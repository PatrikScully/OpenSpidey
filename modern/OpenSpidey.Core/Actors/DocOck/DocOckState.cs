using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.DocOck;

/// <summary>Source CDocOc (type 308, module "docock") constructor fields at 0x44xxxx.</summary>
public sealed class DocOckState(int node, int levelId, int sourceConstructionOrdinal)
{
    public const short Type = 308;
    public const string ModuleName = "docock";
    public const uint LightAddress = 0x549E80;
    public const string ModelName = "docock";

    public int SourceNode { get; } = node;
    public int LevelId { get; } = levelId;
    public int SourceConstructionOrdinal { get; } = sourceConstructionOrdinal;
    public short Health { get; set; } = 600;
    public ushort MinorRadius { get; set; }
    public uint BodyFlags { get; set; } = 0x216;
    public uint CombatFlags { get; set; }
    public int AiState { get; set; } = 1;
    public int AiSubstate { get; set; }
    public int Field324 { get; set; } = 25;
    public int Field4EC { get; set; } = 1;
    public int Field554 { get; set; } = 4;
    public int Field21E { get; set; } = 100;
    public int Field194 { get; set; } = unchecked((int)0xFFFC0000);
    public int Field198 { get; set; } = 0x3FFF;
    public FixedVector3 LinkPosition { get; set; }
    public bool IsDead => (BodyFlags & 0x40) != 0;
}
