namespace OpenSpidey.Core.Actors.Venom;

/// <summary>Source CVenom (type 313, module "venom") constructor fields at 0x4E9600.</summary>
public sealed class VenomState(int node, int levelId, int difficultyLevel, int sourceConstructionOrdinal)
{
    public const short Type = 313;
    public const string ModuleName = "venom";
    public const uint LightAddress = 0x54FED0;

    public static string ModelNameFor(int levelId) =>
        levelId is 0x9904 or 0x601 or 0x602 or 0x501 or 0x503 or 0x504 or 0x505 or 0x506 ? "venom2" : "venom";

    public int SourceNode { get; } = node;
    public int LevelId { get; } = levelId;
    public int DifficultyLevel { get; } = difficultyLevel;
    public int SourceConstructionOrdinal { get; } = sourceConstructionOrdinal;
    public byte ActorPhase => unchecked((byte)SourceConstructionOrdinal);
    public string ModelName => ModelNameFor(LevelId);
    public short SurfaceClearance { get; } = 100;
    public ushort MinorRadius { get; set; } = 0xC0;
    public uint BodyFlags { get; set; } = 0x216;
    public uint Field218 { get; set; }
    public short Health { get; set; }
    public int AiState { get; set; } = 1;
    public int Substate { get; set; }
    public int Field358 { get; set; }
    public int Field39C { get; set; }
    public byte Field33C { get; set; }
    public byte Field33D { get; set; } = 1;
    public byte Field34D { get; set; }
    public byte Field354 { get; set; }
    public int Field3B0 { get; set; }
    public int Field3B4 { get; set; }
    public int Field460 { get; set; }
    public int Field464 { get; set; }
    public int Field45C { get; set; }
    public int ChaseBarFrame { get; set; }
    public int ChaseBarAccumulator { get; set; }
    public bool ResolveSwitchNodesPending { get; set; }
    public bool IsDead => (BodyFlags & 0x40) != 0;
}
