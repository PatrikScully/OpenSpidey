using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Original twenty button slots; mirrors are the player's latches, not a second device.</summary>
public sealed class PlayerMeleeInput
{
    public byte[] Pressed { get; } = new byte[20];
    public byte[] Triggered { get; } = new byte[20];
    public byte[] Mirrors { get; } = new byte[20];
    public sbyte Sideways { get; set; }
    public sbyte Forward { get; set; }
    public sbyte PreviousSideways { get; set; }
    public sbyte PreviousForward { get; set; }
}

public sealed class PlayerComboPart
{
    public bool Active { get; set; }
    public bool WaitingFirst { get; set; }
    public bool UsesLateWindow { get; set; }
    public bool Started { get; set; }
    public int Flags { get; set; }
    public int LastPressTicks { get; set; }
    public int InputCursor { get; set; }
    public IReadOnlyList<byte> Input { get; set; } = Array.Empty<byte>();
}

/// <summary>Melee-only source fields. Shared hit/action/position fields remain in Combat.</summary>
public sealed class PlayerMeleeState(PlayerCombatState combat)
{
    public PlayerCombatState Combat { get; } = combat ?? throw new ArgumentNullException(nameof(combat));
    public PlayerMeleeInput Input { get; } = new();
    public int AccumulatedTicks { get; set; }
    public int HeadingDelta { get; set; }
    public int EffectiveHeading { get; set; }
    public FixedVector3 RightDirection { get; set; }
    public ushort Move { get; set; }
    public int ComboStartTicks { get; set; }
    public uint LastButtonClock { get; set; }
    public ushort DistanceCursor { get; set; }
    public PlayerComboBranch? FollowOn { get; set; }
    public bool PartsEnabled { get; set; }
    public byte TrackingActive { get; set; }
    public ushort TransitionAnimation { get; set; }
    public bool FirstContactFrame { get; set; }
    public bool CollisionStreamActive { get; set; }
    public bool AnimationFinished { get; set; }
    public PlayerComboPart[] Parts { get; } = Enumerable.Range(0, 16).Select(_ => new PlayerComboPart()).ToArray();
    public int PartCount { get; set; }
    public FixedVector3[] SlideHookOrigins { get; } = new FixedVector3[4];
    public FixedVector3[] CurrentContactHooks { get; } = new FixedVector3[16];
    public FixedVector3[] PreviousContactHooks { get; } = new FixedVector3[16];
    public object?[] HitTargets { get; } = new object?[4];
    public int HitTargetCount { get; set; }
    public byte HitSessionStarted { get; set; }
    public short HitSessionHealth { get; set; }
    public uint HitSessionClock { get; set; }
    public int AttackEntryClock { get; set; }
    public PlayerKickHandle ZipDiveHandle { get; set; }
    public byte ZipDiveAirControlSuppression { get; set; }
    public int ZipDiveClock { get; set; }
    public byte WebAttackMode { get; set; }
    public byte WebAttackSubstate { get; set; }
    public byte WebAttackStarted { get; set; }
    public byte WebFiringSide { get; set; }
    public byte AnimationFlags { get; set; }
    public IPlayerMeleeTarget? AimTarget { get; set; }
}

public interface IPlayerMeleeTarget
{
    object Identity { get; }
    FixedVector3 Position { get; }
    ushort Type { get; }
    ushort ItemFlags { get; }
    IPlayerMeleeTarget? Next { get; }
}

public interface IPlayerMeleeHost
{
    uint Timer { get; }
    int Suit { get; }
    int Difficulty { get; }
    bool CameraFlag180 { get; }
    IPlayerMeleeTarget? BaddyHead { get; }
    IPlayerMeleeTarget? SelectTargetBaddy(int range, int minimum, int maximum, int flags);
    PlayerKickHandle StoreTargetHandle(IPlayerMeleeTarget? target);
    void OrientToNormal(bool useTarget, FixedVector3 target);
    void SelectAndResetAnimationSounds(ushort animation);
    void RunAnimation(ushort animation, int firstFrame, int lastFrame);
    void PlaySingleAnimation(ushort animation, int firstFrame, int lastFrame);
    void PlaySound(ushort sound, int volume, int pitch);
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitch);
    void ApplyPlayerPose();
    FixedVector3 GetPlayerHookPosition(int hook);
    bool LineToBaddySphere(FixedVector3 start, FixedVector3 end, FixedVector3 direction, int argument, int scale);
    bool TraceWorld(FixedVector3 start, FixedVector3 end, int flags);
    FixedVector3 GetPlayerDynamicHookPosition(ActorHook hook);
    void BuildTargetTransform(IPlayerMeleeTarget target);
    bool CollideWithTarget(IPlayerMeleeTarget target, FixedVector3 start, FixedVector3 end, int scale, out ActorHook hook);
    FixedVector3 GetTargetDynamicHookPosition(IPlayerMeleeTarget target, ActorHook hook);
    bool HitTarget(IPlayerMeleeTarget target, ActorHit hit);
    void PutCameraBehind(int argument);
    void CreateCombatImpactEffect(FixedVector3 position, int kind);
    bool CheckGroundGone();
    bool CheckJump();
    void SwitchToStandMode();
    void LockTargetTorsoAngle();
    void SetTargetTorsoAngle(FixedVector3 target);
}
