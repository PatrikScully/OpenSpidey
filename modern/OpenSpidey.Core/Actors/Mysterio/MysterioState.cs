namespace OpenSpidey.Core.Actors.Mysterio;

/// <summary>Source CMysterio (type 311, module "mysterio") constructor fields.</summary>
public sealed class MysterioState(int node, int levelId, int sourceConstructionOrdinal)
{
    public const short Type = 311;
    public const string ModuleName = "mysterio";
    public const uint LightAddress = 0x54E0F0;
    public const string ModelName = "mysterio";

    public int SourceNode { get; } = node;
    public int LevelId { get; } = levelId;
    public int SourceConstructionOrdinal { get; } = sourceConstructionOrdinal;
    public short Health { get; set; } = 10000;
    public ushort MinorRadius { get; set; }
    public uint BodyFlags { get; set; } = 0x216;
    public uint BehaviorFlags { get; set; } = 0x10200;
    public int AiState { get; set; }
    public int AiSubstate { get; set; }
    public bool IsDead => (BodyFlags & 0x40) != 0;
}
