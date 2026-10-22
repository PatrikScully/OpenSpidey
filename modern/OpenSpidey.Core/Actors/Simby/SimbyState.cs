using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Actors.Procedures;
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
    private ActorProcedureState? combatOwner;
    private uint combatFlags = 0x201;
    /// <summary>CBaddy field_2A8; once bound it is the procedure state's flags read by Neutralize and PathCheck.</summary>
    public uint CombatFlags
    {
        get => combatOwner?.BehaviorFlags ?? combatFlags;
        set
        {
            if (combatOwner is null) combatFlags = value;
            else combatOwner.BehaviorFlags = value;
        }
    }
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
    /// <summary>field_354 (StretchStart.Y): DropDown settle sample and DieWhileFalling ground height.</summary>
    public int DropSampleY { get => StretchStart.Y; set => StretchStart = StretchStart with { Y = value }; }
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
    /// <summary>field_204 / field_208 CBaddy::DistanceToPlayer cache.</summary>
    public int CachedPlayerDistance { get; set; }
    public int DistanceClock { get; set; }
    /// <summary>field_374 unit vector toward the claimed attack slot.</summary>
    public FixedVector3 AttackUnit { get; set; }
    /// <summary>field_380 GetToTarget(2) climb probe point.</summary>
    public FixedVector3 ClimbTarget { get; set; }
    /// <summary>field_39C attack slot heading (1365 * slot).</summary>
    public int AttackAngle { get; set; }
    /// <summary>field_3A8 SetUpJumpPath arc height.</summary>
    public int JumpHeight { get; set; }
    /// <summary>field_3AC one-based RegisterTargetPos slot (0 = none).</summary>
    public int TargetSlot { get; set; }
    /// <summary>field_3CC stretch/jump step count and field_3D0 4096 / steps.</summary>
    public int JumpSteps { get; set; }
    public int JumpStep { get; set; }
    /// <summary>field_188 SLink corrections for the 16 links of the 0x5554A0 pose packet.</summary>
    public RawJointPose[] Joints { get; } = new RawJointPose[16];
    /// <summary>field_3B0 DoStretch reach scale (455 or 204).</summary>
    public int StretchScale { get; set; }
    /// <summary>field_3DC SetUpHandPos midpoint between hooks 14 and 11.</summary>
    public FixedVector3 HandPosition { get; set; }
    /// <summary>field_334 slink scale timer (30 frames).</summary>
    public int SlinkScaleTimer { get; set; }
    /// <summary>field_338 / field_33C sym_base handles and field_340 slink ribbon handle (0 = none).</summary>
    public int SymBase { get; set; }
    public int ClimbSymBase { get; set; }
    public int Ribbon { get; set; }
    /// <summary>field_350 slink/climb start position.</summary>
    public FixedVector3 StretchStart { get; set; }
    /// <summary>field_3F8 slink delta (target - start) >> 12 and field_410 its length.</summary>
    public FixedVector3 SlinkDelta { get; set; }
    public int SlinkLength { get; set; }
    /// <summary>field_418 ribbon segment fills: seven 585-unit slink spans, ten climb spans.</summary>
    public int[] SlinkSegments { get; } = new int[10];
    /// <summary>field_444 per-tick segment fill and field_448 retracted total.</summary>
    public int SlinkRate { get; set; }
    public int SlinkProgress { get; set; }
    /// <summary>CItem scale shorts at 0x28/0x2A/0x2C (used while ItemFlags bit 0x200 is set).</summary>
    public short Scale { get; set; } = 4096;
    /// <summary>field_3D4 4096 / frame count of the current clip.</summary>
    public int ClipScaleRate { get; set; }
    /// <summary>field_3A4 ceiling height found by StillTouchingCeiling (0x4AB490).</summary>
    public int CeilingY { get; set; }
    /// <summary>field_318 attack kind of the last accepted hit (SHitInfo field_4 when flag 2 is set).</summary>
    public int LastHitKind { get; set; }
    /// <summary>field_194 CBaddy collision word: AI sets bits 0..2 while field_3A0 bit 2 is up, else bit 0x8000.</summary>
    public uint Field194 { get; set; }
    /// <summary>field_45C: the player's AI function saved while the Simby holds him (GettingGrabbed 10..13).</summary>
    public bool PlayerAiSaved { get; set; }
    /// <summary>field_38C last hand position the player was pulled toward.</summary>
    public FixedVector3 GrabHandPosition { get; set; }
    public bool IsDead => (BodyFlags & 0x40) != 0;

    public void BindCombatFlags(ActorProcedureState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        owner.BehaviorFlags = CombatFlags;
        combatOwner = owner;
    }
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
    /// <summary>gSimbyAttackData (0x682C60): three attack-slot bits.</summary>
    public int AttackData { get; set; }
    /// <summary>Target slot mask at 0x682C68 for the eight positions at 0x6A7B10.</summary>
    public uint TargetSlotMask { get; set; }
    public FixedVector3[] TargetSlots { get; } = new FixedVector3[8];
    /// <summary>Slot index -1 (0x6A7B04) written after "Not enough slots.  Fire Matt."; its mask bit is 31.</summary>
    public FixedVector3 OverflowTargetSlot { get; set; }

    /// <summary>~CSimby (0x4A7AE0) target-slot release.</summary>
    public void ReleaseTargetSlot(SimbyState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        TargetSlotMask &= ~(1u << ((state.TargetSlot - 1) & 31));
    }
}
