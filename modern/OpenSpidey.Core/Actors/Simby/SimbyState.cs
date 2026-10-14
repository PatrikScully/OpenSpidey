using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Simby;

/// <summary>Source324 CSimby constructor fields (0x4A7870) and module "simby".</summary>
public sealed class SimbyState(int node, int levelId, int sourceConstructionOrdinal)
{
    public const short Type = 324;
    public const short DropletTriggerType = 411;
    public const short PunchObType = 412;
    public const int DarkModelLevel = 0x803;
    public const int SpecialBehaviorLevel = 0x702;
    public const uint PosePacketAddress = 0x5554A0;
    public const string ModuleName = "simby";
    public const string BrightModelName = "symbi_02";
    public const string DarkModelName = "sym_dark";
    /// <summary>Source gSimbySetup at the PC export (0x5521C0 / 0x5521C4).</summary>
    public static readonly int[] Setup = [84215815, 261];

    public int SourceNode { get; } = node;
    public int LevelId { get; } = levelId;
    public int SourceConstructionOrdinal { get; } = sourceConstructionOrdinal;
    public string ModelName => LevelId == DarkModelLevel ? DarkModelName : BrightModelName;
    public short SurfaceClearance { get; set; } = 100;
    public ushort MinorRadius { get; set; } = 140;
    public uint BodyFlags { get; set; } = 0x216;
    public uint BehaviorFlags { get; set; }
    public uint CombatFlags { get; set; } = 0x201;
    public short Health { get; set; } = 800;
    public short PushValue { get; set; } = 64;
    public int GeneralParameter { get; set; } = 32;
    public int WaitUpdates { get; set; }
    /// <summary>CBaddy field_31C.bothFlags (AI state).</summary>
    public int Stage { get; set; }
    /// <summary>CBaddy dumbAssPad / field_320 (AI substate).</summary>
    public int Substate { get; set; }
    public int SetupLow { get; set; } = Setup[0];
    public int SetupHigh { get; set; } = Setup[1];
    public int AttackRelatedSnapshot { get; set; }
    /// <summary>field_34C grunt/alert noise timer (Rnd(300) at construct).</summary>
    public int AlertNoiseTimer { get; set; }
    /// <summary>field_348 MoveFreely/AI attack alert (separate from field_34C).</summary>
    public int AlertModeTimer { get; set; }
    public int Field3B8 { get; set; } = 0xDAC;
    public int Field3BC { get; set; } = 0x190;
    public int Field3C0 { get; set; } = 0x555;
    /// <summary>field_3C4 DetermineFightState accumulator.</summary>
    public int FightProbeAccumulator { get; set; }
    /// <summary>field_3C8 DetermineFightState distance band threshold.</summary>
    public int FightProbeThreshold { get; set; }
    public int Field30C { get; set; } = 0x64;
    public int Field310 { get; set; }
    public uint Field3A0 { get; set; }
    public int AttackMask { get; set; }
    /// <summary>field_324 used by MoveFreely as a long-range wait counter.</summary>
    public int Field324 { get; set; }
    /// <summary>field_354 DropDown case3 previous Y sample.</summary>
    public int DropSampleY { get; set; }
    /// <summary>field_1F0 waypoint budget used by Initialize / FollowWaypoints.</summary>
    public int Field1F0 { get; set; }
    /// <summary>field_1F8 RunTimer slot used by FollowWaypoints / MoveFreely(666).</summary>
    public int Field1F8 { get; set; }
    /// <summary>field_230 MoveFreely case3/4 turn wait.</summary>
    public int Field230 { get; set; }
    /// <summary>field_398 aim Y used by MoveFreely LABEL_31.</summary>
    public int AimYaw { get; set; }
    public FixedVector3 UnitDirection { get; set; }
    public FixedVector3 TargetPosition { get; set; }
    public FixedVector3 ShadowPosition { get; set; }
    public int ShadowGroundY { get; set; }
    /// <summary>field_3D8 CSimbySlimeBase pointer presence (non-zero when live).</summary>
    public int SlimeHandle { get; set; }
    /// <summary>Padding after field_3F0 used as slime respawn countdown.</summary>
    public int SlimeRespawnTimer { get; set; }
    /// <summary>field_3B4 MoveFreely ceiling-probe latch.</summary>
    public int CeilingProbeLatch { get; set; }
    public short FlashFrames { get; set; }
    public ushort FlashDeltaR { get; set; }
    public ushort FlashDeltaG { get; set; }
    public short FlashDeltaB { get; set; }
    public int FlashRestore { get; set; }
    public int Rgb { get; set; }
    /// <summary>CBaddy field_2F0 route flags (0x4227 sets bit2).</summary>
    public ushort RouteFlags { get; set; }
    /// <summary>CBaddy field_212/213 height-query timing from opcode 0x4227.</summary>
    public byte HeightQueryInterval { get; set; }
    public byte HeightQueryElapsed { get; set; }
    public bool IsDead => (BodyFlags & 0x40) != 0;
}

/// <summary>Source412 CPunchOb constructor fields (0x4A73B0), created by module slot4.</summary>
public sealed class PunchObState(int node, int sourceConstructionOrdinal)
{
    public const short Type = SimbyState.PunchObType;
    public const string ModelName = "sym_gen";
    public int SourceNode { get; } = node;
    public int SourceConstructionOrdinal { get; } = sourceConstructionOrdinal;
    public ushort MinorRadius { get; set; } = 100;
    public uint BodyFlags { get; set; } = 0x216;
    public uint BehaviorFlags { get; set; }
    public uint CombatFlags { get; set; } = 2;
    public short Health { get; set; }
    public int Stage { get; set; }
    public int Substate { get; set; }
    public int Elapsed { get; set; }
    public byte DeathPulseSent { get; set; }
    public bool IsDead => (BodyFlags & 0x40) != 0;
}

/// <summary>Shared live instance counter at source 0x682C5C and reset flag 0x682C64.</summary>
public sealed class SimbyWorldState
{
    public int Count { get; set; }
    public int CountResetFlag { get; set; }
    public int AttackData { get; set; }
}
