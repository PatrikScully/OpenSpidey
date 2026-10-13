using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

/// <summary>Required source data and native session ownership for live player melee.</summary>
public sealed class GodotPlayerMeleeConfiguration
{
    public required PlayerFightData FightData {get;init;}
    public required ActorHookPacket Hooks {get;init;}
    public required ActorPoseLinkPacket PoseLinks {get;init;}
    public required IGodotPlayerMeleeServices Services {get;init;}
    public required PlayerSenseState SenseState {get;init;}
    public required Func<FixedJointTransform> TargetingRotation {get;init;}
    public required Func<int,int> Random {get;init;}
    public Func<GodotPlayerMelee,GodotPlayerInteractions>? CreateInteractions {get;init;}
    public int AirborneTurnFactor {get;init;}=2;
}

/// <summary>
/// Real source actor/control/environment lists, animated contacts and effects. Unsupported activated
/// interactions must fail explicitly; sphere tests here are slide blockers, never melee damage contacts.
/// </summary>
public interface IGodotPlayerMeleeServices : IPlayerTargetSelectionHost,IPlayerSenseHost
{
    int Difficulty {get;}
    IPlayerMeleeTarget? BaddyHead {get;}
    IReadOnlyList<PlayerKickObject> KickBaddyOrder {get;}
    IReadOnlyList<PlayerKickObject> KickEnvironmentalOrder {get;}
    PlayerKickHandle MakeKickHandle(PlayerKickObject target);
    PlayerKickTrace TraceKickItem(FixedVector3 start,FixedVector3 end,int mode);
    PlayerKickHandle StoreTargetHandle(IPlayerMeleeTarget? target);
    PlayerZipDiveTarget? RecoverZipDiveTarget(ref PlayerKickHandle handle);
    bool GrabZipDiveTarget(PlayerZipDiveTarget target, FixedVector3 destination);
    void ClearZipDiveGrabFlag(PlayerZipDiveTarget target);
    void HitZipDiveTarget(PlayerZipDiveTarget target, ActorHit hit);
    FixedVector3 GetZipDiveTargetHook(PlayerZipDiveTarget target, ActorHook hook);
    void PrepareKickState(PlayerKickState state);
    void UpdateAutoAimTarget(GodotPlayerMelee melee);
    void SelectAndResetAnimationSounds(ushort animation);
    void ProcessAnimationSoundWords(int animation,int frame);
    bool LineToBaddySphere(FixedVector3 start,FixedVector3 end,FixedVector3 direction,int argument,int scale);
    bool TraceMeleeWorld(FixedVector3 start,FixedVector3 end,int flags);
    void BuildTargetTransform(IPlayerMeleeTarget target);
    bool CollideWithTarget(IPlayerMeleeTarget target,FixedVector3 start,FixedVector3 end,int scale,out ActorHook hook);
    FixedVector3 GetTargetDynamicHookPosition(IPlayerMeleeTarget target,ActorHook hook);
    bool HitTarget(IPlayerMeleeTarget target,ActorHit hit);
    void CreateCombatImpactEffect(FixedVector3 position,int kind);
}
