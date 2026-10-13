using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

/// <summary>Recovered CThug configuration. Parameter names retain their source offsets where meaning is unproven.</summary>
public sealed class ThugState
{
    public int Type { get; private set; }
    public string? ModelName { get; private set; }
    public short SurfaceClearance { get; set; }
    public uint AnimationWord294 { get; internal set; }
    public uint AnimationWord298 { get; internal set; }
    public byte IdleAnimation => (byte)AnimationWord298;
    public int SourceNode { get; }
    public int LevelId { get; }
    public int AttackRelatedSnapshot { get; }
    public int SourceConstructionOrdinal { get; }
    public bool ShadowEnabled { get; } = true;
    public int ShadowScale { get; set; } = 50;
    public FixedVector3 ShadowPosition { get; set; }
    public int MinorRadius { get; set; }
    public int PushValue { get; } = 64;
    public short Field216 { get; } = 32;
    public byte Field212 { get; set; } = 60;
    public uint BehaviorFlags { get; set; } = 1;
    public uint CombatFlags { get; set; }
    public uint BodyFlags { get; set; } = 0x216;
    public int State { get; set; }
    public int Substate { get; set; }
    public int ActionCounter { get; set; }
    public int PauseUpdates { get; set; }
    public int PathPointIndex { get; set; }
    public int AlertTicks { get; set; }
    public int TurnRate { get; set; }
    public short Health { get; set; }
    public int FightSuppressionTicks { get; set; }
    public int RangedCooldownTicks { get; set; }
    public int FightPollInterval { get; set; }
    public int ClosePlayerTicks { get; set; }
    public int DistanceSampleClock { get; set; }
    public int CachedPlayerDistance { get; set; }
    public FixedVector3 LastSeenPlayerPosition { get; set; }
    public byte AttackOwnershipFlags { get; set; }
    public byte AttackSlotMask { get; set; }
    public byte AttackSlotCursor { get; set; }
    public byte ActorPhase { get; set; }
    public uint ActionFlags { get; set; }
    public int WorkCounter { get; set; }
    public int GrenadeElapsedTicks { get; set; }
    public int GroundReferenceY { get; set; }
    public int ReactionContext { get; set; }
    public int SlidePitchRate { get; set; }
    public int SlideYawRate { get; set; }
    public int Timer334Ticks { get; set; }
    public int GunSoundCooldownTicks { get; set; }
    public int ConversationTicks { get; set; }
    public int CollisionDelayTicks { get; set; }
    public int GunSoundDelayTicks { get; set; }
    public int AimPlayerY { get; set; }
    public int LandingY { get; set; }
    public FixedVector3 PreviousPosition { get; set; }
    public int DeathSoundTicks { get; set; }
    public uint TintColor { get; set; }
    public uint Transparency { get; set; }
    public int YankOscillationTicks { get; set; }
    public int WorkDelayCounter { get; set; }
    public int DialogueClip { get; set; }
    public int DialogueBank { get; set; }
    public int Field370 { get; private set; } = 3500;
    public int Field374 { get; private set; } = 400;
    public int Field378 { get; private set; } = 2047;
    public int Field37C { get; private set; } = 100;
    public int Field380 { get; private set; }
    public int Field384 { get; private set; } = 1500;
    public int Field388 { get; private set; } = 10;
    public int Field38C { get; private set; } = 10;
    public int Field1FC { get; private set; } = 10;
    public int Field394 { get; private set; } = 2000;

    // Defaults are established before ParseScript calls SetThugType. Changing type later does not recompute them.
    public ThugState(int sourceNode, int levelId, int attackRelatedSnapshot = 0, int initialType = 0,
        int sourceConstructionOrdinal = 0)
    {
        SourceNode = sourceNode;
        LevelId = levelId;
        AttackRelatedSnapshot = attackRelatedSnapshot;
        SourceConstructionOrdinal = sourceConstructionOrdinal;
        ActorPhase = unchecked((byte)sourceConstructionOrdinal);
        Type = initialType;
        MinorRadius = initialType != 304 ? 150 : 96;
        Field380 = initialType != 304 ? 400 : 300;
    }

    public void SetThugType(int type, Action<int>? readHooks = null)
    {
        // The source stores mType even on its unknown-type diagnostic branch.
        Type = type;
        switch (type)
        {
            case 304:
                ModelName = "thug";
                SurfaceClearance = 100;
                AnimationWord294 = 0x0D0D0100;
                AnimationWord298 = 3;
                break;
            case 312:
                ModelName = LevelId == 513 ? "henchngt" : "henchman";
                SurfaceClearance = 100;
                AnimationWord294 = 0x06040504;
                AnimationWord298 = 0;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown thug type.");
        }
        readHooks?.Invoke(type);
    }

    public void SetParamByIndex(int index, int value)
    {
        switch (index)
        {
            case 1: Field374 = value; break;
            case 2: Field370 = value; break;
            case 3: Field378 = value; break;
            case 4: Field37C = value; break;
            case 5: Field380 = value; break;
            case 6: Field384 = value; break;
            case 7: Field388 = value; break;
            case 8: Field38C = value; break;
            case 9: Field1FC = value; break;
            case 10: Field394 = value; break;
            case 11: CombatFlags = value != 0 ? CombatFlags | 0x800u : CombatFlags & ~0x800u; break;
            case 12: CombatFlags |= 0x8000; break;
            default: throw new ArgumentOutOfRangeException(nameof(index), index, "Unknown thug parameter index.");
        }
    }
}
