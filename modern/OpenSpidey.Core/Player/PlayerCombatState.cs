using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Player damage/recovery fields; unnamed scratch values retain their source offsets.</summary>
public sealed class PlayerCombatState(short health, int maxHealth)
{
    /// <summary>Shared progress counter used by source switch and web actions (field E20).</summary>
    public int ActionProgress { get; set; }
    public short Health { get; set; } = health;
    public int MaxHealth { get; set; } = maxHealth;
    public uint ActionState { get; set; } = 1;
    public ushort AnimationId { get; set; }
    public int AnimationFrame { get; set; }
    public int ElapsedTicks { get; set; } = 2;
    public ushort CollisionFlags { get; set; }
    public FixedVector3 Position { get; set; }
    public FixedVector3 Velocity { get; set; }
    public FixedVector3 ForwardDirection { get; set; }
    public FixedVector3 UpDirection { get; set; }
    public FixedVector3 CrawlSurfaceNormal { get; set; }
    public bool IsCrawling { get; set; }
    public bool SynthesizedInputActive { get; set; }
    public int FieldE18 { get; set; }
    public short SavedInputAnimationSpeed { get; set; } // Source +0xE12, saved by SetIgnoreInputTimer.
    public int Field36C { get; set; }
    public int Field8DC { get; set; }
    public byte Field8E8 { get; set; }
    public byte Field8E9 { get; set; }
    /// <summary>Source +0x8EB; CBaddy opcode 0x42BA writes (operand != 0) when MechList player is live.</summary>
    public byte Field8EB { get; set; }
    public bool LookaroundActive { get; set; }
    public byte SwingCameraResetLatch { get; set; } // Source +0x201, consumed by camera-facing finalization.
    public byte CameraAngleLock { get; set; } // Source +0x8EC.
    public byte ArenaConstraintActive { get; set; } // Source +0xEF4.
    public byte Field54C { get; set; }
    public byte Field551 { get; set; }
    public byte FieldAD7 { get; set; }
    public byte PendingWebReleaseAnimation { get; set; } // Source +0x54D.
    public byte FlailDropChain { get; set; } // Source +0x54E.
    public byte FieldAE5 { get; set; }
    public byte FieldAE6 { get; set; } // Locks airborne torso aiming; separate from E8C velocity heading.
    public int FallReferenceHeight { get; set; } // E38.
    public int FallReferenceTick { get; set; } // E3C.
    public int FallDamageStartsAt { get; set; } // E40; zero disables landing damage.
    public int FallDamageFullHealthAt { get; set; } // E44.
    public byte FieldAE4 { get; set; }
    public byte FieldE8C { get; set; }
    public short FieldEA6 { get; set; }
    public ushort AirborneTicks { get; set; } // Source +0xE8E.
    public int HeadingTurnTicksRemaining { get; set; }
    public int LoopingSoundHandle { get; set; }
    public bool FirstSmokeTrailActive { get; set; }
    public bool SecondSmokeTrailActive { get; set; }
    public bool HeldObjectPresent { get; set; }
    public bool WebPresent { get; set; }
    public bool DeathEffectPresent { get; set; }
    public bool ArmorActive { get; set; }
    /// <summary>Original player57C suit-5 switch, saved independently from the damage-absorbing armour flag5E9.</summary>
    public byte SuitArmorEnabled { get; set; }
    public int ArmorAmount { get; set; }
    public bool ArmorTexturesActive { get; set; }
    public int ArmorAnimationVariant { get; set; }
    public int SavedArmorAmount { get; set; }
    public byte SavedArmorActive { get; set; }
    public uint LastHitClock { get; set; }
    public uint LastHitAction { get; set; }
    public uint PainSoundClock { get; set; }
    public int Field528 { get; set; }
    public int Field52C { get; set; }
    public int Field534 { get; set; }
    public int FightMusicClock { get; set; } // Source530, advanced by the render music owner.
    public uint FightMusicVoiceHandle { get; set; } // Source538; independent from player5E4 looping effects.
    /// <summary>Shared CSwitch::Flick counter stored at player field568.</summary>
    public int SwitchCounter { get; set; }
    public int FloorCameraState { get; set; } = -1; // Source +0x540 constructor value; cleared only by floor setup in mode3.
    public int FightMusicTime { get; set; } = 300; // Source +0xC60, initialized by CPlayer's constructor.
    public byte FightMusicEnabled { get; set; } = 1; // C5C: constructor level gate.
    public int FightMusicGain { get; set; } = 4096; // C64: source Q12 gain, advanced before player input.
    public byte FightMusicFadeDown { get; set; } // C68.
    public byte FightMusicFadeUp { get; set; } // C69.
    public byte Electrified { get; set; }
    public int ElectrificationTicks { get; set; }
    public int Field5AC { get; set; }
    public int Field5B0 { get; set; }
    public uint Field5B4 { get; set; }
    public int DeathTimer { get; set; }
}

/// <summary>Explicit source world, animation, audio and effect boundaries used by player hit/death/recovery.</summary>
public interface IPlayerHitHost
{
    int CurrentSuit { get; }
    int SymbioteRelated { get; }
    uint Timer { get; }
    bool CameraFlag180 { get; }
    void StopSound(int handle);
    void PutCameraBehind(int argument);
    void FadeSmokeTrail(int sourceSlot);
    void SmashHeldObject();
    void SetArmorTextures(bool enabled);
    void SaveArmor(int amount, byte active);
    void PlaySound(ushort sound, int volume, int pitch);
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitchOffset);
    void StopMyXa();
    void ExitLookaroundMode();
    void SwitchWebToBlob();
    void Electrify();
    bool LineOfSight(FixedVector3 from, FixedVector3 to, int flags, int argument);
    void PlaySingleAnimation(ushort clip, int from, int to);
    void RunAnimation(ushort clip, int from, int to);
    void OrientToNormal(bool useTarget, FixedVector3 target);
    void ResetDeathAnimationSounds(ushort clip);
    void DeleteDeathEffect();
    void SetCameraField12C(int value);
    void SwitchToStandMode();
    void SetLevelStatus(int value);
}
