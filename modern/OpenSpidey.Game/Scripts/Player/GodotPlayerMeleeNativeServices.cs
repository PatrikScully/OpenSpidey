using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

/// <summary>Concrete source actor contacts, selectors, original reticle, probes, sound words and player impact effects.</summary>
public sealed class GodotPlayerMeleeNativeServices : IGodotPlayerMeleeServices,IPlayerAutoAimHost,IDisposable
{
    private readonly GodotPlayer player;
    private readonly GodotPlayerInventory inventory;
    private readonly GodotPlayerMeleeDirectory directory;
    private readonly GodotActorWorldQuery world;
    private readonly GodotThugResources thugResources;
    private readonly PsxAssetLibrary items;
    private readonly GodotPlayerCombatImpacts impacts;
    private readonly IGodotPlayerMeleeSessionHost session;
    private readonly Func<uint> clock;
    private readonly Func<FixedJointTransform> targeting;
    private readonly Func<FixedVector3> referencePosition;
    private readonly Func<FixedJointTransform> referenceRotation;
    private readonly Dictionary<ushort,uint[]> soundWords=[];
    private uint[]? selectedSounds;
    private int soundCursor;
    private GodotPlayerTargetReticle? reticle;
    public GodotPlayerMeleeNativeServices(GodotPlayer player,GodotPlayerMeleeDirectory directory,GodotActorWorldQuery world,
        GodotThugResources thugResources,PsxAssetLibrary items,GodotPlayerCombatImpacts impacts,OriginalPlayerData original,
        IGodotPlayerMeleeSessionHost session,Func<uint> clock,Func<FixedJointTransform> targetingRotation,
        Func<FixedVector3> referencePositionWholeUnits,Func<FixedJointTransform> referenceRotation)
    {
        this.player=player;inventory=player.Inventory ?? throw new InvalidOperationException("Native melee requires the player's actual supply/fire-webbing state owner.");
        this.directory=directory;this.world=world;this.thugResources=thugResources;this.items=items;
        this.impacts=impacts;this.session=session;this.clock=clock;targeting=targetingRotation;
        referencePosition=referencePositionWholeUnits;this.referenceRotation=referenceRotation;
        player.ConfigureHandVariants(original.FistVariants);
        foreach(var entry in original.AnimationSounds)
            soundWords.Add(entry.Key,[..entry.Value.Select(word=>unchecked((uint)word)),uint.MaxValue]);
    }
    public int AnimationSoundCursor=>soundCursor;
    public IReadOnlyList<uint>? SelectedAnimationSoundWords=>selectedSounds is null?null:Array.AsReadOnly(selectedSounds);
    public FixedVector3? ReticlePosition=>HasReticle?reticle!.SourcePosition:null;
    public uint Clock=>clock();
    public int Difficulty=>session.Difficulty;
    public FixedVector3 ReferencePositionWholeUnits=>referencePosition();
    public FixedJointTransform ReferenceRotation=>referenceRotation();
    public IEnumerable<PlayerTargetCandidate> SourceBaddyOrder=>directory.SourceBodies.Select(body=>body.Candidate);
    public IEnumerable<PlayerSwitchCandidate> SourceControlOrder=>directory.SourceControls.Select(actor=>actor.TargetCandidate);
    public IPlayerMeleeTarget? BaddyHead=>directory.BaddyHead;
    public IReadOnlyList<PlayerKickObject> KickBaddyOrder=>directory.SourceBodies.Select(body=>new PlayerKickObject(body.Identity,body.Type,body.Position,0)).ToArray();
    public IReadOnlyList<PlayerKickObject> KickEnvironmentalOrder=>session.SourceEnvironmentalOrder;
    public PlayerKickHandle MakeBaddyHandle(PlayerTargetCandidate target)=>directory.MakeHandle(target.Target.Identity);
    public PlayerKickHandle MakeKickHandle(PlayerKickObject target)=>directory.Contains(target.Identity)?directory.MakeHandle(target.Identity):session.MakeEnvironmentalHandle(target);
    public PlayerKickHandle StoreTargetHandle(IPlayerMeleeTarget? target)=>directory.MakeHandle(target?.Identity);
    public PlayerZipDiveTarget? RecoverZipDiveTarget(ref PlayerKickHandle handle)
    {
        GodotScriptActor? actor = directory.Recover(ref handle);
        if (actor is null) return null;
        GodotPlayerMeleeDirectory.Body body = directory.RequireBody(actor);
        short health = actor switch
        {
            GodotThugActor thug => unchecked((short)thug.State.Health),
            GodotCopActor cop => cop.Health,
            GodotHostageActor hostage => hostage.State.Health,
            _ => throw new NotSupportedException("ZipDive requires an original baddy adapter.")
        };
        return new(actor, body.Position, body.Type, health);
    }
    public bool GrabZipDiveTarget(PlayerZipDiveTarget target, FixedVector3 destination) => directory.RequireBody(target.Identity).Native switch
    {
        GodotThugActor thug => thug.Grab(destination),
        GodotCopActor cop => cop.Grab(destination),
        GodotHostageActor => false, // Original CBaddy::Grab returns0; hostage supplies no override.
        _ => throw new NotSupportedException("ZipDive target has no proven original Grab slot.")
    };
    public void ClearZipDiveGrabFlag(PlayerZipDiveTarget target)
    {
        GodotScriptActor actor = directory.RequireBody(target.Identity).Native;
        if (actor is GodotThugActor thug) thug.Actor.BehaviorFlags &= ~0x40u;
        else if (actor is GodotCopActor cop) cop.Actor.BehaviorFlags &= ~0x40u;
        else if (actor is GodotHostageActor hostage) hostage.Actor.BehaviorFlags &= ~0x40u;
        else throw new NotSupportedException("ZipDive target has no original behavior flags.");
    }
    public void HitZipDiveTarget(PlayerZipDiveTarget target, ActorHit hit) => HitTarget(directory.RequireBody(target.Identity), hit);
    public FixedVector3 GetZipDiveTargetHook(PlayerZipDiveTarget target, ActorHook hook) => GetTargetDynamicHookPosition(directory.RequireBody(target.Identity), hook);
    public PlayerTargetCandidate? RecoverBaddy(ref PlayerKickHandle handle)
    {var actor=directory.Recover(ref handle);return actor is GodotThugActor or GodotHostageActor?directory.RequireBody(actor).Candidate:null;}
    public PlayerKickHandle MakeSwitchHandle(PlayerSwitchCandidate? item)=>directory.MakeHandle(item?.Identity);
    public PlayerSwitchAimPoint? GetSwitchAutoAimPoint(PlayerSwitchCandidate item)=>
        item.Identity is GodotSwitchActor actor && directory.Contains(actor)?actor.AutoAimPoint:
        throw new InvalidOperationException("Switch selector received a control outside the source directory.");
    public bool LineOfSight(FixedVector3 start,FixedVector3 end,int firstSourceArgument,int secondSourceArgument)
    {
        if(firstSourceArgument!=0 || secondSourceArgument!=0)throw new NotSupportedException("Source player selector uses Utils_LineOfSight0,0.");
        FixedVector3 hit=default;return world.LineOfSight(start,end,0,ref hit);
    }
    public PlayerSwitchTrace LineToItem(FixedVector3 start,FixedVector3 end,int mode)
    {
        if(mode!=1)throw new NotSupportedException("Source switch selector uses the environmental zoned query mode1.");
        GodotActorWorldHit? hit=world.Trace(start,end,includeEnvironmentalActors:true);
        return hit is null?default:new(session.ResolveWorldContact(hit),hit.Triangle.PackedMetadata);
    }
    public PlayerKickTrace TraceKickItem(FixedVector3 start,FixedVector3 end,int mode)
    {
        if(mode!=1)throw new NotSupportedException("Source kick selection uses the environmental zoned query mode1.");
        GodotActorWorldHit? hit=world.Trace(start,end,includeEnvironmentalActors:true);
        if(hit is null)return default;
        FixedVector3 delta=(hit.Position-start)>>12;
        int distance=FixedVectorMath.OriginalSquareRoot(unchecked(delta.X*delta.X+delta.Y*delta.Y+delta.Z*delta.Z));
        return new(session.ResolveWorldContact(hit),distance);
    }
    public void PrepareKickState(PlayerKickState state)
    {state.SurfaceTransition=session.SurfaceTransition;state.HeldObject=session.HeldObject;}
    public void UpdateAutoAimTarget(GodotPlayerMelee melee)
    {
        PlayerAutoAimSelection.Update(melee.AutoAim,this);
        melee.State.AimTarget=melee.AutoAim.Target;
        melee.Kick.TorsoTarget=melee.AutoAim.Target is { } target?new(target.Identity,target.Type,target.Position,0):null;
    }
    public byte SurfaceTransition=>session.SurfaceTransition;
    public int ElapsedTicks=>player.Combat.State.ElapsedTicks;
    public bool HasReticle=>reticle is not null && GodotObject.IsInstanceValid(reticle);
    public short ReticleYaw
    {get=>RequireReticle().SourceYaw;set{GodotPlayerTargetReticle actor=RequireReticle();actor.SourceYaw=value;actor.Synchronize();}}
    public IPlayerMeleeTarget? SelectTargetBaddy(int distance,int cone,int distanceWeight,int coneWeight)=>
        PlayerTargetSelection.SelectBaddy(player.Combat.State.Position,targeting(),this,distance,cone,distanceWeight,coneWeight);
    public ushort GetTargetHeight(IPlayerMeleeTarget target)=>directory.RequireBody(target.Identity).ReticleHeight;
    public void CreateReticle()
    {
        if(HasReticle)throw new InvalidOperationException("Source reticle is already owned.");
        reticle=GodotPlayerTargetReticle.Create(items);
        // It is a miscellaneous world-space body, not a child in the player's moving local frame.
        player.GetParent().AddChild(reticle);
    }
    public void DeleteReticle()
    {if(reticle is { } actor && GodotObject.IsInstanceValid(actor))actor.Free();reticle=null;}
    public void PositionReticle(FixedVector3 position)
    {GodotPlayerTargetReticle actor=RequireReticle();actor.SourcePosition=position;actor.Synchronize();}
    private GodotPlayerTargetReticle RequireReticle()=>HasReticle?reticle!:throw new InvalidOperationException("Source player reticle is not allocated.");
    public void SelectAndResetAnimationSounds(ushort animation)
    {
        if(animation>=300)throw new ArgumentOutOfRangeException(nameof(animation));
        selectedSounds=soundWords.GetValueOrDefault(animation);soundCursor=0;
        if(selectedSounds is not null)
            for(int index=0;selectedSounds[index]!=uint.MaxValue;index++)selectedSounds[index]&=0xFFFF;
    }
    public void ProcessAnimationSoundWords(int animation,int frame)
    {
        if(selectedSounds is null)return;
        uint word=selectedSounds[soundCursor];
        if(word==uint.MaxValue){selectedSounds=null;return;}
        if(frame<unchecked((int)word))return;
        selectedSounds[soundCursor++]=word|0xFFFF0000;
    }
    public bool LineToBaddySphere(FixedVector3 start,FixedVector3 end,FixedVector3 direction,int argument,int scale)
    {
        if(argument!=0 || scale!=2048)throw new NotSupportedException("Source combo slide uses LineToSphere with null exclude and radiusScale2048.");
        ActorLaserBody[] bodies=directory.SourceBodies.Select(body=>new ActorLaserBody(body.Identity,body.Position,body.Radius,body.BodyFlags)).ToArray();
        return ActorLaserCollision.CheckBodies(start,end,bodies,radiusScale:scale,ignoreRadius:session.IgnoreSphereRadius) is not null;
    }
    public bool TraceMeleeWorld(FixedVector3 start,FixedVector3 end,int flags)
    {
        if(flags!=1)throw new NotSupportedException("Source combo slide probes use M3dZone_LineToItem1.");
        return world.Trace(start,end,includeEnvironmentalActors:true) is not null;
    }
    public void BuildTargetTransform(IPlayerMeleeTarget target)
    {
        GodotScriptActor actor=directory.RequireBody(target.Identity).Native;
        if(actor is GodotThugActor thug)thug.BuildTransform();else if(actor is GodotHostageActor hostage)hostage.BuildTransform();
    }
    public bool CollideWithTarget(IPlayerMeleeTarget target,FixedVector3 start,FixedVector3 end,int scale,out ActorHook hook)
    {
        GodotScriptActor actor=directory.RequireBody(target.Identity).Native;
        if(actor is GodotHostageActor hostage)return hostage.CollideWithPartBoxes(start,end,scale,out hook);
        if(actor is not GodotThugActor thug)throw new NotSupportedException("Melee target has no original posed-part adapter.");
        IReadOnlyList<ActorPartBox> boxes=thugResources.CollisionBoxes(thug.State.ModelName!);
        var pose=new FixedJointTransform[boxes.Count];
        for(int part=0;part<pose.Length;part++)pose[part]=thug.Model.GetSourceAnimationPartPose(part);
        var angles=thug.Actor.Body.Angles;
        var basis=FixedJointTransform.FromJoint(new(angles.X,angles.Y,angles.Z,0,0,0));
        return ActorPartCollision.TryCollide(thug.Position,basis,pose,boxes,0,start,end,scale,out hook);
    }
    public FixedVector3 GetTargetDynamicHookPosition(IPlayerMeleeTarget target,ActorHook hook)=>directory.RequireBody(target.Identity).Native switch
    {GodotThugActor thug=>thug.GetDynamicHookPosition(hook),GodotHostageActor hostage=>hostage.GetDynamicHookPosition(hook),_=>throw new NotSupportedException("Melee target has no source hook pose.")};
    public bool HitTarget(IPlayerMeleeTarget target,ActorHit hit)=>directory.RequireBody(target.Identity).Native switch
    {GodotThugActor thug=>thug.Hit(hit),GodotHostageActor hostage=>hostage.Hit(hit),_=>throw new NotSupportedException("Melee target has no source Hit implementation.")};
    public void CreateCombatImpactEffect(FixedVector3 position,int kind)=>impacts.Emit(new(
        inventory.State.FireWebbing,player.Combat.State.Field5AC,checked((ushort)player.Model.ClipId)),position,kind);
    public void Dispose()=>DeleteReticle();
}
