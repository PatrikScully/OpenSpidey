using OpenSpidey.Core.Mathematics;

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
    public int Field364 { get; set; }
    public int ScriptElapsedTicks { get; set; }
    public int Field39C { get; set; }
    public byte Field33C { get; set; }
    public byte Field33D { get; set; } = 1;
    public byte Field34D { get; set; }
    public byte Field34C { get; set; }
    public byte Field354 { get; set; }
    public int Field3B0 { get; set; }
    public int Field3B4 { get; set; }
    public int Field3B8 { get; set; }
    public int SwitchNodeCount { get; set; }
    public int[] SwitchNodeIndices { get; } = new int[4];
    public object?[] SwitchTargets { get; } = new object?[5];
    public int Field460 { get; set; }
    public int Field464 { get; set; }
    public int Field45C { get; set; }
    public int ChaseBarFrame { get; set; }
    public int ChaseBarAccumulator { get; set; }
    public bool ResolveSwitchNodesPending { get; set; }
    /// <summary>CBaddy 0x222 current walk node and 0x224 chosen switch slot.</summary>
    public short WaypointNode { get; set; }
    public short Field224 { get; set; }
    /// <summary>0x240 travel target placed on the ground below the linked node.</summary>
    public FixedVector3 TargetPosition { get; set; }
    /// <summary>0x3E0 gTimerRelated stamp taken when a node state spots the player.</summary>
    public int Field3E0 { get; set; }
    /// <summary>0x438..0x448 last Redbook entries per speech table (no immediate repeats).</summary>
    public int[] LastSpeech { get; } = new int[5];
    /// <summary>0x324 footstep bits, 0x330 goo timer, 0x338 l6a4 water level, 0x388 countdown.</summary>
    public int Field324 { get; set; }
    public int Field330 { get; set; }
    public int Field338 { get; set; }
    public int Field388 { get; set; }
    /// <summary>Teleport fade: 0x3AC outline level, 0x454 body grey, 0x458 fade-in and 0x45C fade-out clocks.</summary>
    public int Field3AC { get; set; }
    public int Field454 { get; set; } = 0xFF;
    public int Field458 { get; set; }
    /// <summary>CItem 0x24 packed grey written by the fade.</summary>
    public uint ItemColor { get; set; } = 0xFFFFFF;
    public bool OutlineEnabled { get; set; }
    public bool OutlineSemiTransparent { get; set; }
    public (byte R, byte G, byte B) OutlineColor { get; set; }
    public bool IsDead => (BodyFlags & 0x40) != 0;

    /// <summary>0x226/0x228 state and substate resumed after a jump finishes.</summary>
    public short ReturnAiState { get; set; }
    public short ReturnSubstate { get; set; }
    /// <summary>0x318 hit kind of the last accepted hit.</summary>
    public int HitKind { get; set; }
    /// <summary>0x368 damage accumulated since the last stagger.</summary>
    public int AccumulatedDamage { get; set; }
    /// <summary>0x36C/0x370 state and substate interrupted by TakeHit.</summary>
    public int InterruptedAiState { get; set; }
    public int InterruptedSubstate { get; set; }
    /// <summary>0x374 gTimerRelated stamp of the last stagger-or-fight decision in CVenom::Hit.</summary>
    public int LastHitTime { get; set; }
    /// <summary>0x378 remaining slide ticks and 0x37C slide velocity per tick.</summary>
    public int SlideTicks { get; set; }
    public FixedVector3 SlideVelocity { get; set; }
    /// <summary>0x38C/0x390/0x394 jump arc (count lives in the array length), written by CalculateJumpPositionArray.</summary>
    public FixedVector3[]? JumpPath { get; set; }
    public int JumpCount { get; set; }
    public int JumpIndex { get; set; }
    /// <summary>0x3A0 jump landing point and 0x3E8 first/0x400 second arm hook scratch.</summary>
    public FixedVector3 JumpTarget { get; set; }
    public FixedVector3 HandHookPosition { get; set; }
    /// <summary>0x3F4 wrap side flag, 0x3F8 choke offset, 0x3FC arm stretch timer and 0x400 stretch vector.</summary>
    public byte Field3F4 { get; set; }
    public int Field3F8 { get; set; }
    public int StretchTimer { get; set; }
    public FixedVector3 StretchVector { get; set; }
    /// <summary>0x40C and 0x418 attack hook positions, 0x42C web reach and 0x430 ground probe.</summary>
    public FixedVector3 AttackHookPosition { get; set; }
    public FixedVector3 ArmHookPosition { get; set; }
    public int WebReach { get; set; }
    public int GroundProbe { get; set; }
    /// <summary>0x44C/0x450 last Redbook entries of the combat speech tables.</summary>
    public int[] LastCombatSpeech { get; } = new int[2];
    /// <summary>0x1F8 trap-web wait counter.</summary>
    public int TrapCounter { get; set; }
    /// <summary>0x334 CVenomElectrified existence.</summary>
    public bool ElectrifiedPresent { get; set; }
    /// <summary>0x428 CWeb (the Venom web strand) existence.</summary>
    public bool WebHeld { get; set; }
}
