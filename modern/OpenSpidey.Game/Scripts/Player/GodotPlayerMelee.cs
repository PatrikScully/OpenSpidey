using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

/// <summary>Source player combo/input/frame ownership using actual native actors and original posed part contacts.</summary>
public sealed class GodotPlayerMelee : IPlayerMeleeFrameHost,IPlayerMeleeHost,IPlayerKickHost,IDisposable
{
    private static readonly int[] SlideHooks=[6,5,1,0];
    private readonly GodotPlayer player;
    private readonly GodotPlayerMeleeConfiguration configuration;
    private readonly FixedJointTransform[] pose=new FixedJointTransform[18];
    private FixedJointTransform cachedBasis;
    private FixedVector3 cachedTranslation;
    public PlayerMeleeState State {get;}
    public PlayerKickState Kick {get;}
    public GodotPlayerZipDive ZipDive {get;}
    public PlayerAutoAimState AutoAim {get;}=new();
    public PlayerMeleeController Controller {get;}
    public GodotPlayerInteractions? Interactions {get;}
    public PlayerSenseState Sense => configuration.SenseState;
    public int? HeldObjectFlags => Kick.HeldObject?.ManipulationFlags;
    public uint Timer => configuration.Services.Clock;
    public uint Clock => Timer;
    public int Suit => player.Combat.CurrentSuit;
    public int Difficulty => configuration.Services.Difficulty;
    public bool CameraFlag180 => player.Combat.CameraFlag180;
    public IPlayerMeleeTarget? BaddyHead => configuration.Services.BaddyHead;
    public IReadOnlyList<PlayerKickObject> SourceBaddyOrder => configuration.Services.KickBaddyOrder;
    public IReadOnlyList<PlayerKickObject> SourceEnvironmentalOrder => configuration.Services.KickEnvironmentalOrder;

    public GodotPlayerMelee(GodotPlayer player,GodotPlayerMeleeConfiguration configuration)
    {
        this.player=player ?? throw new ArgumentNullException(nameof(player));
        this.configuration=configuration ?? throw new ArgumentNullException(nameof(configuration));
        if(!ReferenceEquals(configuration.SenseState.Combat,player.Combat.State))
            throw new ArgumentException("Melee, sense targets and player damage must share one combat state.",nameof(configuration));
        if(configuration.Hooks.Hooks.Count!=9 || configuration.PoseLinks.Links.Count!=12)
            throw new ArgumentException("Player melee requires the original nine hooks and twelve source joints.",nameof(configuration));
        State=new(player.Combat.State);Kick=new(State);
        Controller=new(State,configuration.FightData,this,configuration.Random);
        ZipDive=new(player,this,configuration.Services);
        player.Model.InitializeSourceJoints(configuration.PoseLinks);
        CacheTransform();
        Interactions=configuration.CreateInteractions?.Invoke(this);
    }
    public void PrepareTargets(int ticks)
    {
        State.AccumulatedTicks=unchecked((int)player.Controller.SourceTimeTicks);
        State.EffectiveHeading=player.Controller.Orientation.GetEffectiveHeading(player.Controller.OnWall);
        State.RightDirection=player.Controller.Orientation.Right;
        State.AnimationFinished=player.Model.Playback.IsFinished;
        configuration.Services.PrepareKickState(Kick);
        configuration.Services.UpdateAutoAimTarget(this);
        PlayerSenseTargets.Refresh(Sense,configuration.Services);
        PlayerSenseTargets.Rebuild(Sense,configuration.Services);
        player.ActorState.MinorRadius=100;
        State.HitSessionStarted=Sense.Quips.EncounterActive;
        State.HitSessionHealth=unchecked((short)Sense.Quips.StartingHealth);
        State.HitSessionClock=Sense.Quips.EncounterClock;
    }
    public void ReadControls(PlayerMovementInputState movement,PlayerButtonInput buttons)
    {
        // The verified melee rules name source E2D Sideways and E2E Forward.
        // ReadAnalogueInput writes E2D from UpDown and E2E from LeftRight.
        State.Input.PreviousForward=movement.PreviousAxes.Sideways;
        State.Input.PreviousSideways=movement.PreviousAxes.Forward;
        State.Input.Forward=movement.Axes.Sideways;
        State.Input.Sideways=movement.Axes.Forward;
        Read(4,PlayerButton.Target);Read(6,PlayerButton.Zip);Read(7,PlayerButton.Swing);
        Read(16,PlayerButton.Jump);Read(17,PlayerButton.Web);Read(18,PlayerButton.Kick);Read(19,PlayerButton.Punch);
        void Read(int slot,PlayerButton button)
        {
            State.Input.Pressed[slot]=buttons.IsHeld(button)?(byte)1:(byte)0;
            if(buttons.WasTriggered(button))State.Input.Triggered[slot]=State.Input.Mirrors[slot]=1;
        }
    }
    public bool CheckKick()=>PlayerKickSelection.Check(Kick,this);
    public void UpdatePunchCombo()
    {
        State.AnimationFinished=player.Model.Playback.IsFinished;
        Controller.UpdatePunchCombo();
        if(State.Combat.ActionState==(uint)PlayerAction.PunchCombo)
            player.Model.SetSourceAnimationFrame(State.Combat.AnimationFrame);
        // Both source subsystems name the same field354/358/35C encounter snapshot.
        Sense.Quips.EncounterActive=State.HitSessionStarted;
        Sense.Quips.StartingHealth=State.HitSessionHealth;
        Sense.Quips.EncounterClock=State.HitSessionClock;
    }
    public void UpdateZipDive(ushort completedAnimation) => ZipDive.Update(completedAnimation);
    public void UpdateInteraction(ushort completedAnimation)
    {
        (Interactions ?? throw new NotSupportedException("Player pickup/throw/switch actions require a genuine native interaction host.")).Update(completedAnimation);
    }
    public void FinishFrame()
    {
        if((player.ActorState.ItemFlags&4)!=0)ApplyPlayerPose();else CacheTransform();
        AfterPose?.Invoke();
        PlayerHandHooks hands = new(GetPlayerHookPosition(1), GetPlayerHookPosition(0));
        BeforeHeldObjectPlacement?.Invoke(ref hands);
        if(Kick.HeldObject is not null)
            (Interactions ?? throw new NotSupportedException("Held-object placement requires a genuine native interaction host.")).UpdateHeldPose(hands.Left,hands.Right);
        if(State.TrackingActive==0)return;
        for(int hook=0;hook<SlideHooks.Length;hook++)State.SlideHookOrigins[hook]=GetPlayerHookPosition(SlideHooks[hook]);
    }
    public event Action? AfterPose;
    public event PlayerHandHookUpdate? BeforeHeldObjectPlacement;
    public void FinishInput() => PlayerInputFinalizer.ClearEdges(State.Input);
    public void ProcessAnimationSoundWords(int animation,int frame)=>configuration.Services.ProcessAnimationSoundWords(animation,frame);
    private void CacheTransform()
    {cachedBasis=player.Controller.Orientation.Basis;cachedTranslation=State.Combat.Position>>12;}
    public void ApplyPlayerPose()
    {
        player.Model.SetSourceAnimationFrame(State.Combat.AnimationFrame);
        CacheTransform();
        player.ActorState.ItemFlags|=4;
        player.Model.ApplySourceJointPose(true);
    }
    public FixedVector3 GetPlayerHookPosition(int hook)
    {
        if((uint)hook>=configuration.Hooks.Hooks.Count)throw new ArgumentOutOfRangeException(nameof(hook));
        return GetPlayerDynamicHookPosition(configuration.Hooks.Hooks[hook]);
    }
    public FixedVector3 GetPlayerDynamicHookPosition(ActorHook hook)
    {
        for(int part=0;part<pose.Length;part++)pose[part]=player.Model.GetSourcePartPose(part);
        return ActorHookPosition.Evaluate(State.Combat.Position,cachedTranslation,cachedBasis,pose,hook);
    }
    public IPlayerMeleeTarget? SelectTargetBaddy(int range,int minimum,int maximum,int flags)=>
        PlayerTargetSelection.SelectBaddy(State.Combat.Position,configuration.TargetingRotation(),configuration.Services,range,minimum,maximum,flags);
    public PlayerKickHandle StoreTargetHandle(IPlayerMeleeTarget? target)=>configuration.Services.StoreTargetHandle(target);
    public PlayerKickHandle MakeHandle(PlayerKickObject target)=>configuration.Services.MakeKickHandle(target);
    public PlayerKickTrace LineToItem(FixedVector3 start,FixedVector3 end,int mode)=>configuration.Services.TraceKickItem(start,end,mode);
    public PlayerKickSwitchSelection SelectTargetSwitch(int maximumDistance,int minimumFacing,int weight,int facingWeight)
    {
        PlayerKickSwitchSelection selected=PlayerTargetSelection.SelectSwitch(State.Combat.Position,configuration.TargetingRotation(),configuration.Services,
            maximumDistance,minimumFacing,weight,facingWeight);
        AutoAim.SwitchAimPoint=selected.AutoAimPosition;
        return selected;
    }
    public void OrientToNormal(bool useTarget,FixedVector3 target)
    {
        player.Controller.OrientFromMelee(State.HeadingDelta,useTarget,target);
        State.EffectiveHeading=player.Controller.Orientation.GetEffectiveHeading(player.Controller.OnWall);
        State.RightDirection=player.Controller.Orientation.Right;
    }
    public void SelectAndResetAnimationSounds(ushort animation)
    {configuration.Services.SelectAndResetAnimationSounds(animation);player.FrameSounds.Select(animation);}
    public void RunAnimation(ushort animation,int firstFrame,int lastFrame)
    {
        player.Model.RunSourceClip(animation,firstFrame,lastFrame); SyncAnimationFields();
    }
    public void PlaySingleAnimation(ushort animation,int firstFrame,int lastFrame)
    {
        player.Animation.Run(animation,firstFrame,lastFrame); SyncAnimationFields();
    }
    private void SyncAnimationFields()
    {
        State.Combat.AnimationId = (ushort)player.Model.ClipId; State.Combat.AnimationFrame = player.Model.Playback.Frame;
        State.AnimationFinished = player.Model.Playback.IsFinished;
    }
    public void PlaySound(ushort sound,int volume,int pitch)=>player.Combat.PlaySound(sound,volume,pitch);
    public void PlayPositionSound(ushort sound,FixedVector3 position,int pitch)=>player.Combat.PlayPositionSound(sound,position,pitch);
    public bool LineToBaddySphere(FixedVector3 start,FixedVector3 end,FixedVector3 direction,int argument,int scale)=>
        configuration.Services.LineToBaddySphere(start,end,direction,argument,scale);
    public bool TraceWorld(FixedVector3 start,FixedVector3 end,int flags)=>configuration.Services.TraceMeleeWorld(start,end,flags);
    public void BuildTargetTransform(IPlayerMeleeTarget target)=>configuration.Services.BuildTargetTransform(target);
    public bool CollideWithTarget(IPlayerMeleeTarget target,FixedVector3 start,FixedVector3 end,int scale,out ActorHook hook)=>
        configuration.Services.CollideWithTarget(target,start,end,scale,out hook);
    public FixedVector3 GetTargetDynamicHookPosition(IPlayerMeleeTarget target,ActorHook hook)=>configuration.Services.GetTargetDynamicHookPosition(target,hook);
    public bool HitTarget(IPlayerMeleeTarget target,ActorHit hit)=>configuration.Services.HitTarget(target,hit);
    public void PutCameraBehind(int argument)=>player.Combat.PutCameraBehind(argument);
    public void CreateCombatImpactEffect(FixedVector3 position,int kind)=>configuration.Services.CreateCombatImpactEffect(position,kind);
    public bool CheckGroundGone()=>player.Controller.MeleeCheckGroundGone();
    public bool CheckJump()=>player.Controller.MeleeCheckJump();
    public void SwitchToStandMode()=>player.Combat.SwitchToStandMode();
    public void LockTargetTorsoAngle()=>player.Controller.LockMeleeTorsoAngle();
    public void SetTargetTorsoAngle(FixedVector3 target)=>player.Controller.TargetMeleeTorsoAngle(target,configuration.TargetingRotation(),configuration.AirborneTurnFactor);
    public void SetTargetTorsoAngle(short wanted, bool fast)=>player.Controller.TargetMeleeTorsoHeading(wanted,configuration.AirborneTurnFactor,fast);
    public void InitiateCombo(ushort move,int headStart)=>Controller.Initiate(move,headStart);
    public void Dispose()
    {if(configuration.Services is IDisposable services)services.Dispose();}
}
