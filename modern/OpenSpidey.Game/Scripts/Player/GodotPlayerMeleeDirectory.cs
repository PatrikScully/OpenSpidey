using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Player;

/// <summary>Actual source-list order, pre-AI distance snapshots and recoverable native identities.</summary>
public sealed class GodotPlayerMeleeDirectory : IDisposable
{
    public sealed class Body : IPlayerMeleeTarget
    {
        private readonly GodotPlayerMeleeDirectory owner;
        internal Body(GodotPlayerMeleeDirectory owner,GodotScriptActor actor,uint validation)
        {this.owner=owner;Native=actor;Validation=validation;}
        public GodotScriptActor Native {get;}
        public uint Validation {get;}
        public uint CachedPlayerDistance {get;internal set;}
        public object Identity => Native;
        public FixedVector3 Position => Native.SourcePosition;
        public ushort Type => checked((ushort)Native.SourceActorType);
        public ushort ItemFlags => Native switch
        {GodotThugActor thug=>thug.ItemFlags,GodotHostageActor hostage=>hostage.ItemFlags,GodotBlackCatActor cat=>cat.ItemFlags,IGodotSourceBaddyBody baddy=>baddy.ItemFlags,GodotManipulationDebris debris=>debris.ItemFlags,_=>throw Unsupported()};
        public ushort Radius => Native switch
        {GodotThugActor thug=>unchecked((ushort)thug.State.MinorRadius),GodotHostageActor hostage=>hostage.State.MinorRadius,GodotBlackCatActor cat=>cat.State.MinorRadius,IGodotSourceBaddyBody baddy=>baddy.MinorRadius,GodotManipulationDebris debris=>debris.MinorRadius,_=>throw Unsupported()};
        public ushort BodyFlags => Native switch
        {GodotThugActor thug=>unchecked((ushort)thug.State.BodyFlags),GodotHostageActor hostage=>unchecked((ushort)hostage.BodyFlags),GodotBlackCatActor cat=>unchecked((ushort)cat.State.BodyFlags),IGodotSourceBaddyBody baddy=>baddy.BodyFlags,GodotManipulationDebris debris=>debris.BodyFlags,_=>throw Unsupported()};
        public uint BehaviorFlags => Native switch
        {GodotThugActor thug=>thug.Actor.BehaviorFlags,GodotHostageActor hostage=>hostage.Actor.BehaviorFlags,GodotBlackCatActor cat=>cat.State.BehaviorFlags,IGodotSourceBaddyBody baddy=>baddy.BehaviorFlags,GodotManipulationDebris debris=>debris.SourceBehaviorFlags,_=>throw Unsupported()};
        public byte CanShowSenseIndicator => Native is GodotThugActor thug && thug.State.TurnRate!=0?(byte)1:(byte)0;
        // CBaddy constructor initializes source fieldF4 to128 for these supported families.
        public ushort ReticleHeight => Native is GodotManipulationDebris
            ? throw new NotSupportedException("Zero-radius debris cannot be a reticle target.") : (ushort)128;
        public IPlayerMeleeTarget? Next
        {get{int index=owner.bodies.IndexOf(this);return index>=0 && index+1<owner.bodies.Count?owner.bodies[index+1]:null;}}
        public PlayerTargetCandidate Candidate => new(this,Radius,BodyFlags,CachedPlayerDistance,BehaviorFlags,CanShowSenseIndicator);
        private NotSupportedException Unsupported()=>new($"Native melee body family{Native.GetType().Name} has no source metadata adapter.");
    }
    private sealed record Handle(WeakReference<GodotScriptActor> Native,uint Validation);
    private readonly List<Body> bodies=[];
    private readonly List<GodotSwitchActor> controls=[];
    private readonly Dictionary<GodotScriptActor,uint> validations=[];
    private readonly Dictionary<GodotScriptActor,Body> registeredBodies=[];
    private readonly Dictionary<GodotScriptActor,GodotNativeDestructionObserver> lifetimeObservers=[];
    private readonly HashSet<GodotActorRegistry> boundRegistries=[];
    private uint nextValidation;
    private bool disposed;
    public IReadOnlyList<Body> SourceBodies => bodies;
    public IReadOnlyList<GodotSwitchActor> SourceControls => controls;
    public IPlayerMeleeTarget? BaddyHead => bodies.Count==0?null:bodies[0];
    public Body RegisterBody(GodotScriptActor actor)
    {
        if(actor is not (GodotThugActor or GodotHostageActor or GodotBlackCatActor or GodotManipulationDebris or IGodotSourceBaddyBody))throw new NotSupportedException("Native melee directory requires a proven source baddy body adapter.");
        uint validation=Register(actor);
        var body=new Body(this,actor,validation);registeredBodies.Add(actor,body);bodies.Insert(0,body);
        return body;
    }
    public void RegisterControl(GodotSwitchActor actor)
    {Register(actor);controls.Insert(0,actor);}
    private uint Register(GodotScriptActor actor)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(!GodotObject.IsInstanceValid(actor) || actor.IsQueuedForDeletion() || validations.ContainsKey(actor))throw new ArgumentException("Source native identity is invalid or already registered.",nameof(actor));
        if(++nextValidation==0)throw new InvalidOperationException("Native source handle validation counter exhausted.");
        validations.Add(actor,nextValidation);
        var observer=new GodotNativeDestructionObserver {Name="SourceMeleeDirectoryLifetime",Destroyed=()=>Remove(actor)};
        lifetimeObservers.Add(actor,observer);
        try {actor.AddChild(observer);return nextValidation;}
        catch {Remove(actor);throw;}
    }
    public void Remove(GodotScriptActor actor)
    {StopObserving(actor);validations.Remove(actor);registeredBodies.Remove(actor);bodies.RemoveAll(body=>ReferenceEquals(body.Native,actor));if(actor is GodotSwitchActor control)controls.Remove(control);}
    private void StopObserving(GodotScriptActor actor)
    {
        if(!lifetimeObservers.Remove(actor,out GodotNativeDestructionObserver? observer))return;
        bool inNativeDestruction=observer.Destroyed is null;
        observer.Destroyed=null;
        if(!inNativeDestruction && GodotObject.IsInstanceValid(observer))observer.Free();
    }
    /// <summary>Bind source lifecycle notifications without treating suspension as destruction.</summary>
    public void Bind(GodotActorRegistry registry)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        ArgumentNullException.ThrowIfNull(registry);
        if(!boundRegistries.Add(registry))return;
        registry.ActorSuspended+=Suspend;
        registry.ActorResumed+=Resume;
        registry.ActorRemoved+=Remove;
        registry.ActorDistanceMeasured+=SetMeasuredDistance;
    }
    public void Unbind(GodotActorRegistry registry)
    {
        if(!boundRegistries.Remove(registry))return;
        registry.ActorSuspended-=Suspend;
        registry.ActorResumed-=Resume;
        registry.ActorRemoved-=Remove;
        registry.ActorDistanceMeasured-=SetMeasuredDistance;
    }
    public void Suspend(GodotScriptActor actor)
    {
        bodies.RemoveAll(body=>ReferenceEquals(body.Native,actor));
        if(actor is GodotSwitchActor control)controls.Remove(control);
    }
    public void Resume(GodotScriptActor actor)
    {
        if(!validations.ContainsKey(actor))return;
        Suspend(actor);
        if(registeredBodies.TryGetValue(actor,out Body? body))bodies.Insert(0,body);
        else if(actor is GodotSwitchActor control)controls.Insert(0,control);
    }
    public void SetMeasuredDistance(GodotScriptActor actor,uint distance)
    {if(registeredBodies.TryGetValue(actor,out Body? body))body.CachedPlayerDistance=distance;}
    /// <summary>Call at the original list updater's distance stage, before EveryFrame/animation/AI, never during player selection.</summary>
    public void CaptureBeforeActorUpdate(GodotScriptActor actor,FixedVector3 playerPosition)
    {if(!registeredBodies.TryGetValue(actor,out Body? body))throw new InvalidOperationException("Actor is not a registered melee body.");body.CachedPlayerDistance=ActorLifecycle.Distance(playerPosition,body.Position);}
    public bool Contains(object identity)=>identity is GodotScriptActor actor && validations.ContainsKey(actor) && GodotObject.IsInstanceValid(actor);
    public Body RequireBody(object identity)=>identity is GodotScriptActor actor && registeredBodies.TryGetValue(actor,out Body? body)
        ? body : throw new InvalidOperationException("Melee target is not a registered live source body.");
    public PlayerKickHandle MakeHandle(object? identity)
    {
        if(identity is null)return default;
        if(identity is not GodotScriptActor actor || !validations.TryGetValue(actor,out uint validation) || !GodotObject.IsInstanceValid(actor))
            throw new ArgumentException("Source target handle requires a registered live native actor.",nameof(identity));
        return new(new Handle(new(actor),validation),validation);
    }
    /// <summary>Raw pointer half of a handle, as passed unvalidated to CAIProc_LookAt.</summary>
    public GodotScriptActor HandlePointer(object target)=>target is Handle stored && stored.Native.TryGetTarget(out GodotScriptActor? actor) && GodotObject.IsInstanceValid(actor)
        ? actor : throw new InvalidOperationException("The source pointer of this target handle no longer names a live native actor.");
    public GodotScriptActor? Recover(ref PlayerKickHandle handle)
    {
        if(handle.Target is Handle stored && stored.Validation==handle.Validation && stored.Native.TryGetTarget(out GodotScriptActor? actor) &&
            GodotObject.IsInstanceValid(actor) && validations.TryGetValue(actor,out uint validation) && validation==stored.Validation)return actor;
        handle=handle with{Target=null};return null; // Source recovery clears the pointer and retains its validation word.
    }
    public void Dispose()
    {
        if(disposed)return;
        disposed=true;
        foreach(GodotActorRegistry registry in boundRegistries.ToArray())Unbind(registry);
        foreach(GodotScriptActor actor in lifetimeObservers.Keys.ToArray())StopObserving(actor);
        validations.Clear();registeredBodies.Clear();bodies.Clear();controls.Clear();
    }
}
