using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.LizMan;

/// <summary>Source CLizMan (type 317, module "lizman") constructor fields at 0x44ABA0.</summary>
public sealed class LizManState(int node, int levelId, int attackRelatedSnapshot, int sourceConstructionOrdinal)
{
    public const short Type = 317;
    public const string ModuleName = "lizman";
    public const uint LightAddress = 0x54FFE8;
    public const uint SetupWord0 = 0x02020201;
    public const uint SetupWord1 = 0;
    public static string ModelNameFor(int levelId) =>
        levelId is 0x503 or 0x504 or 0x601 or 0x602 ? "lizman2" : "lizman";

    public int SourceNode { get; } = node;
    public int LevelId { get; } = levelId;
    public int AttackRelatedSnapshot { get; set; } = attackRelatedSnapshot;
    public int SourceConstructionOrdinal { get; } = sourceConstructionOrdinal;
    public byte ActorPhase => unchecked((byte)SourceConstructionOrdinal);
    public string ModelName => ModelNameFor(LevelId);
    public short SurfaceClearance { get; } = 100;
    public ushort MinorRadius { get; set; } = 0x80;
    public short PushValue { get; } = 0x40;
    public short Field216 { get; } = 0x20;
    public byte Field212 { get; set; }
    public int ShadowScale { get; set; } = 0x30;
    public FixedVector3 ShadowPosition { get; set; }
    public uint BodyFlags { get; set; } = 0x216;
    public uint BehaviorFlags { get; set; }
    public uint CombatFlags { get; set; }
    public int State { get; set; }
    public int Substate { get; set; }
    public int Field338 { get; set; } = 0x1000;
    public int Field340 { get; set; }
    public int Field374 { get; set; }
    public int Field398 { get; set; }
    public int Field3AC { get; set; } = 0x21;
    /// <summary>0x324 level-0x1103 tick counter.</summary>
    public int Field324 { get; set; }
    /// <summary>0x380 fight-probe phase mask (3/15/31 by player distance).</summary>
    public int Field380 { get; set; }
    /// <summary>CBaddy 0x1A8 last seen player position, 0x204/0x208 DistanceToPlayer cache, 0x29C stored body Y.</summary>
    public FixedVector3 LastSeenPlayerPosition { get; set; }
    public int CachedPlayerDistance { get; set; }
    public int DistanceSampleClock { get; set; }
    public int Field29C { get; set; }
    public int Field1F0 { get; set; }
    public int Field1F4 { get; set; }
    public int Field1F8 { get; set; }
    public uint Field2F0 { get; set; }
    public int Field310 { get; set; }
    public int Field230 { get; set; }
    public int Field318 { get; set; }
    public int ShadowGroundY { get; set; }
    public int Field394 { get; set; }
    public byte Field390 { get; set; }
    public byte Field39C { get; set; }
    public byte Field39D { get; set; }
    public short Health { get; set; }
    public short SlidePitchRate { get; set; }
    public short SlideYawRate { get; set; }
    public FixedVector3[]? JumpPositions { get; set; }
    public int JumpPositionIndex { get; set; }
    public bool IsDead => (BodyFlags & 0x40) != 0;

    public void ApplyLevelDefaults()
    {
        if (LevelId == 0x505)
        {
            CombatFlags |= 0x2000000;
            Field212 = 0xF;
            Field398 = 0x32000;
            BehaviorFlags |= 0x8000;
        }
        else
        {
            Field398 = 0x3C000;
            Field212 = 0x1E;
        }
    }
}
