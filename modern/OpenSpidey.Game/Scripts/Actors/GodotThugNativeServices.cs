using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Concrete source actor effects, native sound/speech, live actor handles and recoil world queries.</summary>
public sealed class GodotThugNativeServices : IGodotThugNativeEffects
{
    private readonly GodotThugVisualEffects visuals;
    private readonly SoundChannelController sounds;
    private readonly Func<SoundListener> listener;
    private readonly GodotSpeechPlayer speech;
    private readonly Func<GodotPlayer?> sourcePlayer;
    private readonly Func<IReadOnlyList<ActorCollisionBody>> sourceBaddyBodies;
    private readonly IGodotThugInteractionHost interactions;
    private readonly GodotActorSessionDirectory directory;
    private readonly ActorProcedureState playerActor = new(new());
    public GodotThugNativeServices(GodotThugVisualEffects visuals, SoundChannelController sounds,
        Func<SoundListener> listener, GodotSpeechPlayer speech, GodotPlayer player,
        Func<IReadOnlyList<ActorCollisionBody>> sourceBaddyBodies, IGodotThugInteractionHost interactions,
        GodotActorSessionDirectory directory)
        : this(visuals, sounds, listener, speech, () => player, sourceBaddyBodies, interactions, directory)
    {
        ArgumentNullException.ThrowIfNull(player);
    }
    public GodotThugNativeServices(GodotThugVisualEffects visuals, SoundChannelController sounds,
        Func<SoundListener> listener, GodotSpeechPlayer speech, Func<GodotPlayer?> sourcePlayer,
        Func<IReadOnlyList<ActorCollisionBody>> sourceBaddyBodies, IGodotThugInteractionHost interactions,
        GodotActorSessionDirectory directory)
    {
        this.visuals = visuals ?? throw new ArgumentNullException(nameof(visuals));
        this.sounds = sounds ?? throw new ArgumentNullException(nameof(sounds));
        this.listener = listener ?? throw new ArgumentNullException(nameof(listener));
        this.speech = speech ?? throw new ArgumentNullException(nameof(speech));
        this.sourcePlayer = sourcePlayer ?? throw new ArgumentNullException(nameof(sourcePlayer));
        this.sourceBaddyBodies = sourceBaddyBodies ?? throw new ArgumentNullException(nameof(sourceBaddyBodies));
        this.interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        this.directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }
    public int DomeCount => interactions.DomeCount;
    public bool IgnoreSphereRadius => interactions.IgnoreSphereRadius;
    public int PlayerWebComboValue => interactions.PlayerWebComboValue;
    public void PlayPositionSound(uint sound, FixedVector3 position, int pitchOffset) => sounds.PlayAt(sound,position,listener());
    // Original PC playSFX does not apply the declaration's pitch_offset argument.
    public void PlayGlobalSound(ushort sound,int volume,int pitchOffset) => sounds.Play(sound,unchecked((short)volume));
    public void CreateCombatImpact(GodotThugActor actor,FixedVector3 position,int flags) => visuals.CreateCombatImpact(actor,position,flags);
    public void CreateShadow(GodotThugActor actor)
    {
        visuals.CreateShadow(actor);
        directory.RegisterConstructingActor(actor);
    }
    public void KillShadow(GodotThugActor actor) => visuals.KillShadow(actor);
    public void UpdateAppearanceAndShadow(GodotThugActor actor) => visuals.UpdateAppearanceAndShadow(actor);
    public void ClearLaser(GodotThugActor actor) => visuals.ClearBarrelLine(actor);
    public void DeleteBarrelLine(GodotThugActor actor) => visuals.ClearBarrelLine(actor);
    public void SetLaser(GodotThugActor actor,FixedVector3 start,FixedVector3 end) => visuals.SetBarrelLine(actor,start,end);
    public void DrawTracer(GodotThugActor actor,ThugShotLine line,object? attachment,byte red,byte green,byte blue)
    {
        object? native = attachment is ActorProcedureState state ? NativeOwner(state) : attachment;
        if (attachment is not null && native is null) throw new NotSupportedException("Tracer attachment requires its captured live native actor handle.");
        visuals.DrawTracer(actor,line,native,red,green,blue);
    }
    public void DrawBarrelFlash(GodotThugActor actor,ThugShotLine line,byte red,byte green,byte blue) => visuals.DrawBarrelFlash(actor,line,red,green,blue);
    public void DrawGlowFlash(FixedVector3 position,IReadOnlyList<int> arguments) => visuals.DrawGlowFlash(position,arguments);
    public void SpawnGrenade(GodotThugActor actor,ThugGrenadeRequest request) => interactions.SpawnGrenade(actor,request);
    public void NotifyKill(int actorType) => interactions.NotifyKill(actorType);
    public void PlayDialogue(GodotThugActor actor,int bank,int clip,FixedVector3 position,int attenuation) => speech.Play(bank,clip,attenuation);
    public bool GrabUpdate(GodotThugActor actor,ref FixedVector3 position,ref short yaw) => interactions.GrabUpdate(actor,ref position,ref yaw);
    public IThugTrapWeb? RecoverWeb(GodotThugActor actor,bool tugWeb) => interactions.RecoverWeb(actor,tugWeb);
    public void SetPlayerWebCombo(int duration,int amount) => interactions.SetPlayerWebCombo(duration,amount);
    public IThugInjuryBody? StruckGameObject(GodotThugActor actor,int first,int second) =>
        ActorRecoilCollision.StruckGameObject(actor,actor.State.PreviousPosition,actor.Position,first != 0,second != 0,new RecoilHost(this,actor));
    public void ElasticCollision(GodotThugActor actor,IThugInjuryBody target,FixedVector3 velocity,int duration)
    {
        if (target is not ActorCollisionBody body) throw new NotSupportedException("Elastic collision requires the source collision body's live snapshot and identity.");
        ActorRecoilCollision.ElasticCollision(Snapshot(actor),body,velocity,duration,new RecoilHost(this,actor));
    }
    public IThugInjuryBody? StruckGameObject(GodotCopActor actor,int first,int second) =>
        ActorRecoilCollision.StruckGameObject(actor,actor.State.PreviousPosition,actor.Actor.Body.Position,first != 0,second != 0,new WorldRecoilHost(this,actor.World));
    public void ElasticCollision(GodotCopActor actor,IThugInjuryBody target,FixedVector3 velocity,int duration)
    {
        if (target is not ActorCollisionBody body) throw new NotSupportedException("Elastic collision requires the source collision body's live snapshot and identity.");
        ActorRecoilCollision.ElasticCollision(new(actor,actor.State.Type,actor.Actor.Body.Position,unchecked((ushort)actor.State.MinorRadius),
            unchecked((ushort)actor.State.BodyFlags),actor.State.Health),body,velocity,duration,new WorldRecoilHost(this,actor.World));
    }
    public void CheckFallBack(GodotThugActor actor) => ThugFallBackBehavior.Check(actor.Actor,actor.Procedures,new FallBackHost(actor));
    public void ApplyHitToBody(IThugInjuryBody body, ActorHit hit)
    {
        object identity = body is ActorCollisionBody snapshot ? snapshot.Identity : body;
        directory.ApplyHit(identity, hit);
    }
    public object StoreActorHandle(GodotThugActor actor) => directory.StoreHandle(actor);
    public ActorProcedureState? RecoverActor(object? handle)
    {
        GodotPlayer? player = sourcePlayer();
        if ((player is not null && ReferenceEquals(handle, player)) || ReferenceEquals(handle, playerActor))
        { if (!Alive(player)) return null; RefreshPlayerBody(player!); return playerActor; }
        return directory.RecoverActor(handle);
    }
    public ActorBodyMotion? RecoverBody(ActorProcedureState actor)
    {
        if (ReferenceEquals(actor, playerActor))
        {
            GodotPlayer? player = sourcePlayer();
            if (!Alive(player)) return null;
            RefreshPlayerBody(player!); return actor.Body;
        }
        return directory.RecoverBody(actor);
    }
    public void SendMessage(GodotThugActor sender, object targetHandle, int code) => directory.SendMessage(sender, targetHandle, code);
    public void SelectHostageCamera(FixedVector3 actorPosition,FixedVector3 hostagePosition) => interactions.SelectHostageCamera(actorPosition,hostagePosition);
    public void SendSignal(GodotThugActor actor) => interactions.SendSignal(actor);
    public bool ExecuteScriptCommand(GodotThugActor actor,ushort opcode,ActorScriptCursor operands,ActorScriptEnvironment environment) =>
        interactions.ExecuteScriptCommand(actor,opcode,operands,environment);
    private object? NativeOwner(ActorProcedureState state)
    {
        if (ReferenceEquals(state, playerActor)) return sourcePlayer() is { } player && Alive(player) ? player : null;
        return directory.RecoverNativeActor(state);
    }
    private static bool Alive(Node? node) => node is not null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion();
    private void RefreshPlayerBody(GodotPlayer player)
    {
        playerActor.Body.Position = player.Controller.Position;
        playerActor.Body.Velocity = player.Controller.Velocity;
        playerActor.Body.Angles = new(0,player.Controller.Orientation.Heading,0);
    }
    private static ActorCollisionBody PlayerSnapshot(GodotPlayer player) => new(player,50,player.Controller.Position,player.ActorState.MinorRadius,
        player.ActorState.BodyFlags,player.Combat.State.Health);
    private static ActorCollisionBody Snapshot(GodotThugActor actor) => new(actor,actor.Type,actor.Position,
        unchecked((ushort)actor.State.MinorRadius),actor.State.BodyFlags,actor.State.Health);
    private sealed class RecoilHost(GodotThugNativeServices services,GodotThugActor actor) : IActorRecoilCollisionHost
    {
        public IReadOnlyList<ActorCollisionBody> SourcePlayerBodies => services.sourcePlayer() is { } player && Alive(player) ? [PlayerSnapshot(player)] : [];
        public IReadOnlyList<ActorCollisionBody> SourceBaddyBodies => services.sourceBaddyBodies();
        public bool IgnoreSphereRadius => services.IgnoreSphereRadius;
        public bool LineOfSightCheck { get => actor.World.LineOfSightCheck; set => actor.World.LineOfSightCheck = value; }
        public ActorCollisionBody? TraceEnvironmental(FixedVector3 start,FixedVector3 end) =>
            actor.World.TraceEnvironmental(start,end) is { } hit ? services.interactions.DescribeEnvironmentalContact(hit) : null;
        public void ApplyElasticHit(ActorCollisionBody target,ActorElasticHit hit) => services.ApplyHitToBody(target,hit.ToActorHit());
    }
    private sealed class WorldRecoilHost(GodotThugNativeServices services,GodotActorWorldQuery world) : IActorRecoilCollisionHost
    {
        public IReadOnlyList<ActorCollisionBody> SourcePlayerBodies => services.sourcePlayer() is { } player && Alive(player) ? [PlayerSnapshot(player)] : [];
        public IReadOnlyList<ActorCollisionBody> SourceBaddyBodies => services.sourceBaddyBodies();
        public bool IgnoreSphereRadius => services.IgnoreSphereRadius;
        public bool LineOfSightCheck { get => world.LineOfSightCheck; set => world.LineOfSightCheck = value; }
        public ActorCollisionBody? TraceEnvironmental(FixedVector3 start,FixedVector3 end) =>
            world.TraceEnvironmental(start,end) is { } hit ? services.interactions.DescribeEnvironmentalContact(hit) : null;
        public void ApplyElasticHit(ActorCollisionBody target,ActorElasticHit hit) => services.ApplyHitToBody(target,hit.ToActorHit());
    }
    private sealed class FallBackHost(GodotThugActor actor) : IThugFallBackHost
    {
        public ActorPathCheckResult PathCheck(FixedVector3 start,FixedVector3 end,bool wantHitPoint,int groundRange) =>
            actor.CheckMessagePath(start,end,wantHitPoint,groundRange,default);
    }
}
