namespace OpenSpidey.Core.Actors.Turret;

/// <summary>Source CTurret (type 325, module "turret") constructor fields at 0x4E4780.</summary>
public sealed class TurretState(int node, int levelId, int sourceConstructionOrdinal)
{
    public const short Type = 325;
    public const string ModuleName = "turret";
    public const uint HooksPacketAddress = 0x55984C;

    public int SourceNode { get; } = node;
    public int LevelId { get; } = levelId;
    public int SourceConstructionOrdinal { get; } = sourceConstructionOrdinal;
    public short Health { get; set; } = 10;
    public ushort MinorRadius { get; set; } = 0x80;
    public uint BodyFlags { get; set; } = 0x216;
    public uint CombatFlags { get; set; }
    public int ScriptCursorOffset { get; set; }
    public int Field20C { get; set; } = 1;
}
