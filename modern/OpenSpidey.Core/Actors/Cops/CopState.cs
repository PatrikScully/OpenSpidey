using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Cops;

/// <summary>Recovered CCop (0x394 bytes). Constructor 0x428750 runs before Trig_CreateObject's slot17 SetCopType.</summary>
public sealed class CopState
{
    public const string ModuleName = "cop";
    /// <summary>CBaddy::CheckStateFlags table at 0x549220.</summary>
    public static readonly (short State, ushort Flags)[] StateFlagEntries =
    [
        (2, 64), (23, 64), (24, 64), (1, 64), (26, 42), (27, 42), (22, 58), (18, 32),
        (14, 38), (15, 38), (12, 32), (17, 38), (19, 34), (20, 34), (13, 36), (25, 64), (0, 50)
    ];
    // SetAnimMode words at 0x551E88 (police) and 0x551EA8 (SWAT).
    private static readonly (uint Movement, uint Idle)[] PoliceModes =
        [(0x02010E0D, 3), (0x1F1F0807, 0x11F), (0x08080806, 0x205), (0x05050807, 0x305)];
    private static readonly (uint Movement, uint Idle)[] SwatModes =
        [(0x02010E0D, 3), (0x1F1F0807, 0x105), (0x1F1F0807, 0x205), (0x05050807, 0x305)];

    public CopState(int sourceNode, int levelId, int attackRelatedSnapshot, int sourceConstructionOrdinal)
    {
        SourceNode = sourceNode;
        LevelId = levelId;
        AttackRelatedSnapshot = attackRelatedSnapshot;
        SourceConstructionOrdinal = sourceConstructionOrdinal;
        ActorPhase = unchecked((byte)sourceConstructionOrdinal);
    }

    /// <summary>CItem 0x38; zero while the constructor runs, so 0xDC and 0x370 take their SWAT values.</summary>
    public int Type { get; private set; }
    public string? ModelName { get; private set; }
    public bool IsPolice => Type == 306;
    public int SourceNode { get; }
    public int LevelId { get; }
    /// <summary>0x340, the attack clock 0x5FCCF4 at construction.</summary>
    public int AttackRelatedSnapshot { get; }
    public int SourceConstructionOrdinal { get; }
    /// <summary>0x21D.</summary>
    public byte ActorPhase { get; set; }
    /// <summary>0x21E.</summary>
    public short SurfaceClearance { get; set; }
    public uint AnimationWord294 { get; set; }
    public uint AnimationWord298 { get; set; }
    public byte IdleAnimation => (byte)AnimationWord298;
    public int AnimationMode => (byte)(AnimationWord298 >> 8);
    public int ShadowScale { get; set; } = 50;
    /// <summary>0xB8.</summary>
    public FixedVector3 ShadowPosition { get; set; }
    /// <summary>0xDC.</summary>
    public int MinorRadius { get; set; } = 150;
    public int PushValue { get; } = 64;
    public short Field216 { get; } = 32;
    public byte Field212 { get; set; } = 60;
    /// <summary>CBody 0x46.</summary>
    public uint BodyFlags { get; set; } = 0x216;
    /// <summary>CBaddy 0x218.</summary>
    public uint CombatFlags { get; set; }
    /// <summary>0xE2.</summary>
    public short Health { get; set; }
    /// <summary>0x31C / 0x320.</summary>
    public int State { get; set; }
    public int Substate { get; set; }
    /// <summary>0x1F8.</summary>
    public int ActionCounter { get; set; }
    /// <summary>0x230.</summary>
    public int PauseUpdates { get; set; }
    /// <summary>0x1F0, the stored path point count.</summary>
    public int PathPointIndex { get; set; }
    /// <summary>0x324; the cop resets it to 300 where CThug uses 600.</summary>
    public int AlertTicks { get; set; }
    /// <summary>0x328, run by DoAISwitchLogic beside 0x324.</summary>
    public int Timer328Ticks { get; set; }
    /// <summary>0x32C.</summary>
    public int ClosePlayerTicks { get; set; }
    /// <summary>0x1A4.</summary>
    public int FightSuppressionTicks { get; set; }
    /// <summary>0x310.</summary>
    public int TurnRate { get; set; }
    /// <summary>0x318.</summary>
    public int HitKind { get; set; }
    /// <summary>0x35C.</summary>
    public int FightPollInterval { get; set; }
    /// <summary>0x208 / 0x204, CBaddy::DistanceToPlayer's cache.</summary>
    public int DistanceSampleClock { get; set; }
    public int CachedPlayerDistance { get; set; }
    /// <summary>0x1A8[0].</summary>
    public FixedVector3 LastSeenPlayerPosition { get; set; }
    /// <summary>0x29C / 0x2A0.</summary>
    public int GroundReferenceY { get; set; }
    public int GroundHeight { get; set; }
    /// <summary>0x38C, cleared after messages each frame.</summary>
    public uint ActionFlags { get; set; }
    /// <summary>0x390 / 0x391 / 0x392.</summary>
    public byte AttackOwnershipFlags { get; set; }
    public byte AttackSlotMask { get; set; }
    public byte AttackSlotCursor { get; set; }
    /// <summary>0x360 far, 0x364 vertical, 0x368 cone and 0x36C near sight-cone arguments.</summary>
    public int SightFar { get; set; } = 3500;
    public int SightVertical { get; set; } = 400;
    public int SightCone { get; set; } = 2047;
    public int SightNear { get; set; } = 100;
    /// <summary>0x370.</summary>
    public int Field370 { get; set; } = 400;
    /// <summary>0x374.</summary>
    public int Field374 { get; set; } = 10;
    /// <summary>0x1FC, a word.</summary>
    public int Damage { get; set; } = 10;
    /// <summary>0x37C, the fight-claim distance used with 0x218 bit 0x800.</summary>
    public int FightClaimDistance { get; set; } = 2000;
    /// <summary>0x388, player Y latched when look-at completes during ShootPlayer.</summary>
    public int AimPlayerY { get; set; }
    /// <summary>0x380 CGPolyLine handle present while the laser sight is drawn.</summary>
    public bool HasLaser { get; set; }
    /// <summary>0x354, chase work counter used by ChasePlayer.</summary>
    public int WorkCounter { get; set; }
    /// <summary>0x2FC, the position before the latest flight or yank physics step.</summary>
    public FixedVector3 PreviousPosition { get; set; }
    /// <summary>0x308, the CAIProc_Fall landing height written by ShouldFall.</summary>
    public int LandingY { get; set; }
    /// <summary>0x338, the dying groan countdown.</summary>
    public int DeathSoundTicks { get; set; }
    /// <summary>0x330 / 0x334, per-tick pitch and yaw spin while flying dead (0x218 bit 0x2000).</summary>
    public int SlidePitchRate { get; set; }
    public int SlideYawRate { get; set; }
    /// <summary>CItem 0x30 and 0x24, written by CBaddy::Die's fade.</summary>
    public int Transparency { get; set; }
    public uint TintColor { get; set; }
    /// <summary>0x358, the trapped cop's struggle delay.</summary>
    public int WorkDelayCounter { get; set; }
    /// <summary>0x33C, the state-14 yank bob counter (rises to 40, then falls).</summary>
    public int YankOscillationTicks { get; set; }
    /// <summary>0x211, the CBaddy::SendDeathPulse latch.</summary>
    public bool DeathPulseSent { get; set; }

    /// <summary>0x4288D0. The unknown-type diagnostic branch still stores mType.</summary>
    public void SetCopType(int type)
    {
        Type = type;
        (uint movement, uint idle) = type switch
        {
            306 => PoliceModes[0],
            320 => SwatModes[0],
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown cop type!")
        };
        ModelName = type == 306 ? "police" : "swat";
        SurfaceClearance = 100;
        AnimationWord294 = movement;
        AnimationWord298 = idle;
    }

    /// <summary>0x4294E0; indices 6 and 7 fall to the diagnostic.</summary>
    public void SetParamByIndex(int index, int value)
    {
        switch (index)
        {
            case 1: SightVertical = value; break;
            case 2: SightFar = value; break;
            case 3: SightCone = value; break;
            case 4: SightNear = value; break;
            case 5: Field370 = value; break;
            case 8: Field374 = value; break;
            case 9: Damage = unchecked((short)value); break;
            case 10: FightClaimDistance = value; break;
            case 11: CombatFlags = value != 0 ? CombatFlags | 0x800u : CombatFlags & ~0x800u; break;
            default: throw new ArgumentOutOfRangeException(nameof(index), index, "Unknown index in C_SET_PARAMETER_BY_INDEX");
        }
    }

    /// <summary>0x428EE0. Returns whether a transition clip exists; the words change even when it does not.</summary>
    public bool SetAnimMode(int mode, bool playAnimation, Action<ushort> runAnimation)
    {
        int oldMode = AnimationMode;
        if (oldMode == mode) return false;
        if ((uint)mode >= 4) throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown anim mode.");
        int transition = IsPolice
            ? (mode, oldMode) switch { (0, 1) => 13, (0, 2) => 6, (2, 1) => 4, (2, 3) => 7, (3, 2) => 12, _ => -1 }
            : mode == 0 && oldMode is 1 or 2 ? 13 : -1;
        (AnimationWord294, AnimationWord298) = (IsPolice ? PoliceModes : SwatModes)[mode];
        if (transition == -1) return false;
        if (playAnimation) runAnimation((ushort)transition);
        return true;
    }

    public static ThugStateFlags CreateStateFlags() => new(StateFlagEntries);
    /// <summary>CBaddy ctor sets field_2A8 bit0 before SetCopType.</summary>
    public const uint InitialBehaviorFlags = 1;

    /// <summary>Test harness preset for sight-cone defaults before SetParamByIndex cases.</summary>
    public void SetParamByIndexForTest(int vertical, int far, int cone, int near, int field370, int field374, int damage, int claim)
    {
        SightVertical = vertical; SightFar = far; SightCone = cone; SightNear = near;
        Field370 = field370; Field374 = field374; Damage = damage; FightClaimDistance = claim;
    }
}
