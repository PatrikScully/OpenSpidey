using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Hostages;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Original CHostage construction, model/physics/AI, persistent messages, speech and staged death.</summary>
public partial class GodotHostageActor : GodotScriptActor, IHostageHost, IHostageInitializationHost,
    IHostageDeathHost, IActorScriptHost, IActorScriptVariableWorld, IActorHeightHost, IGodotActorAppearanceHost
{
    private GodotActorRegistry registry = null!;
    private GodotHostageConfiguration configuration = null!;
    private HostageBrain brain = null!;
    private IReadOnlyList<ActorPartBox> boxes = [];
    private readonly FixedJointTransform[] hookPose = new FixedJointTransform[15];
    private bool begun, attached, deathPulseSent, destroyed;
    public HostageState State { get; private set; } = null!;
    public ActorProcedureState Actor { get; private set; } = null!;
    public ActorProcedureList Procedures { get; } = new();
    public HostageMessageQueue Messages { get; } = new();
    public ActorFrameTiming FrameTiming { get; } = new();
    public ActorSpeechAttachment SpeechAttachment { get; } = new();
    public ActorWaypointRoute Route { get; private set; } = null!;
    public ActorHeightController Height { get; private set; } = null!;
    public GodotActorWorldQuery World { get; private set; } = null!;
    public ImportedAnimatedModel Model { get; private set; } = null!;
    public GodotActorAppearance Appearance { get; private set; } = null!;
    public FixedJointTransform SourceTransformBasis { get; private set; }
    public FixedVector3 SourceTransformTranslation { get; private set; }
    public uint HiddenParts { get; set; }
    public int Type => State.Type;
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public new FixedVector3 Position { get => Actor.Body.Position; set { Actor.Body.Position=value; Variables.Position=value; } }
    public ushort ItemFlags { get => State.ItemFlags; set => State.ItemFlags=value; }
    public uint BodyFlags { get => State.BodyFlags; set => State.BodyFlags=value; }
    public uint TintColor => State.TintColor;
    public FixedVector3 ShadowPosition => State.ShadowPosition;
    public int ShadowScale => State.ShadowScale;
    public Node3D AppearanceNode => this;
    public object Identity => this;
    public override FixedVector3 SourcePosition => Position;
    public FixedVector3 PlayerPosition => configuration.Player.Controller.Position;
    FixedVector3? IActorScriptVariableWorld.PlayerPosition => configuration.SourcePlayer?.Controller.Position;
    public int AttackClock => configuration.AttackClock();
    public int Difficulty => registry.Difficulty;
    public int MovieInputPolls => registry.MovieInputPolls;
    public int AnimationId => Model.ClipId;
    public byte AnimationFinished => Model.Playback.Finished;
    public uint WaypointFlags => Route.Flags;
    public uint SpeechClock => configuration.Clock();
    public SpeechRequest? CurrentSpeech => configuration.Speech.Controller.Current;
    public IReadOnlyList<ActorCollisionBody> SourceBaddyOrder => configuration.SourceBaddyOrder();

    public static GodotHostageActor Create(TriggerNode node,GodotActorRegistry registry,GodotHostageConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition=TriggerActorDefinition.Read(node);
        if(definition.ActorType is not (305 or 315))throw new NotSupportedException("Original hostage factory requires type305 or315.");
        int ordinal=configuration.SourceConstructionOrdinal();
        var actor=new GodotHostageActor { Name=$"Hostage{node.Id}",registry=registry,configuration=configuration,Definition=definition,
            State=new(node.Id,ordinal),Actor=new(new()),ScriptState=new(){ ActorType=definition.ActorType,NodeId=node.Id } };
        try
        {
            actor.State.BindSourceSignals(actor.ScriptState);
            actor.AddChild(new GodotNativeDestructionObserver { Name="SourceHostageDestructor",Destroyed=actor.DestroySourceOwnedState });
            registry.ActorRemoved+=actor.OnSourceRemoved;
            actor.Actor.Body.UseLocalVelocity=true;
            actor.State.ItemFlags=2;
            actor.Variables=new(actor.ScriptState,actor){ ItemFlags=2,CollisionRadius=128,AnimationSpeed=0x10000 };
            actor.Lifecycle=new(!definition.ClearsBodyFlag2,false);
            actor.World=new(actor,registry.Triggers);
            actor.Height=new(actor.Actor,actor);
            actor.Route=new(registry.Triggers,node.Id,actor.ParseWaypoint);
            // The original constructor script runs before SetHostageType/InitItem; never substitute a temporary actor model.
            HostageInitialization.Construct(actor.State,actor.Actor,definition,actor,node.SourceOffset);
            actor.State.SetHostageType(definition.ActorType,name=>
            {
                actor.Model=configuration.ModelFactory(name);
                actor.Model.AdvanceAutomatically=false;
                actor.AddChild(actor.Model);
                actor.boxes=configuration.CollisionBoxes(name);
                if(actor.boxes.Count!=15)throw new InvalidDataException("Original hostage needs all fifteen source part boxes.");
            });
            if(definition.ClearsBodyFlag2)actor.State.BodyFlags&=~2u;
            actor.Height.Clearance=actor.State.SurfaceClearance;
            actor.Model.PlaybackSpeed=actor.Variables.AnimationSpeed;
            actor.Appearance=GodotActorAppearance.Create(actor,configuration.ShadowTexture);
            actor.brain=new(actor.State,actor.Actor,actor.Procedures,actor.Messages,actor.SpeechAttachment,actor,configuration.Random);
            actor.SyncScriptStorage();
            actor.BuildTransform();
            return actor;
        }
        catch
        {
            actor.Free();throw;
        }
    }
    internal override void Begin() { begun=true; }
    internal override void Advance()
    {
        SourceUpdates++;
        State.BodyFlags = FrameTiming.Prepare(configuration.Clock(), unchecked((ushort)State.BodyFlags), ItemFlags,
            new(Model.Playback.Frame, Model.Playback.ClipId, Model.Playback.Direction));
        if((ItemFlags&2)!=0)FrameTiming.RecordAnimationElapsed(Model.AdvanceSourceTicks(FrameTiming.ElapsedTicks));
        brain.Advance(FrameTiming.ElapsedTicks);
        Height.GroundHeight=State.GroundHeight;
        SyncScriptStorage();
        Appearance.Synchronize();
    }
    public override void _Process(double delta)
    {
        if(!begun || destroyed || configuration.SourcePlayer is null)return;
        ItemFlags=configuration.IsSourceCulled(this)?(ushort)(ItemFlags|0x8000):(ushort)(ItemFlags&0x7FFF);
        Model.Visible=(ItemFlags&0x8001)==0;
    }
    private void OnSourceRemoved(GodotScriptActor actor)
    { if(ReferenceEquals(actor,this))DestroySourceOwnedState(); }
    private void DestroySourceOwnedState()
    {
        if(destroyed)return;
        destroyed=true;
        registry.ActorRemoved-=OnSourceRemoved;
        if(attached){ configuration.Session.DetachFromSourceBaddyList(this);attached=false; }
        Appearance?.Dispose();
        Messages.Cleanup(all:true);
        Procedures.CleanUp(all:true);
    }
    public void AttachToSourceBaddyList()
    { configuration.Session.AttachToSourceBaddyList(this);attached=true; }
    public void EnableShadow() => State.BodyFlags|=8;
    public void ParseScript(ActorScriptCursor script)
    { SyncScriptStorage(); Runtime=new(script,this);Runtime.Start(registry.Environment); }
    private void ParseWaypoint(ActorScriptCursor script)
    { State.CurrentWaypoint=Route.CurrentNode;SyncScriptStorage();Runtime.ParseAt(script,registry.Environment); }
    public bool GetNextWaypoint()
    { bool next=Route.Advance();State.CurrentWaypoint=Route.CurrentNode;return next; }
    public FixedVector3 WaypointPosition(int node) => World.WaypointPosition(node);
    public void DoPhysics(int mode)
    {
        if(mode!=0)throw new NotSupportedException("Source hostage AI calls only DoPhysics(0).");
        Actor.Body.UseLocalVelocity=(Actor.BehaviorFlags&1)!=0;
        Actor.Body.Integrate(FrameTiming.ElapsedTicks);
    }
    public void BuildTransform()
    {
        ActorAngles angles=Actor.Body.Angles;
        SourceTransformBasis=FixedJointTransform.FromJoint(new(angles.X,angles.Y,angles.Z,0,0,0));
        SourceTransformTranslation=Position>>12;
        Transform3D transform=PsxCoordinates.CharacterTransform(SourceTransformBasis);
        transform.Origin=PsxCoordinates.FixedWorldPosition(SourceTransformTranslation<<12);
        Transform=transform;
    }
    public int GroundHeight(FixedVector3 position,int above,int below) => World.GroundHeight(position,above,below);
    public int GetGroundHeight(FixedVector3 position,int above,int below,out ActorBodyMotion? support) => World.GetGroundHeight(position,above,below,out support);
    public int SetHeight(bool force,int above,int below)
    {
        Height.Clearance=State.SurfaceClearance;Height.RouteFlags=Route.Flags;Height.GroundHeight=State.GroundHeight;
        int result=Height.SetHeight(force,above,below,FrameTiming.ElapsedTicks);State.GroundHeight=Height.GroundHeight;return result;
    }
    public void RunAnimation(ushort clip,int from=0,int to=-1)
    { RequireModel();Model.RunClip(clip,from,to); }
    public void CycleAnimation(ushort clip,sbyte direction=1)
    { RequireModel();Model.CycleClip(clip,direction); }
    public bool PlayPositionalSpeech(int bank,int slot,FixedVector3 position,int priority) => configuration.Speech.Play(bank,slot,priority);
    public void StopSpeech() => configuration.Speech.Controller.Stop();
    public void SendSignal() => configuration.Session.SendSignal(this);
    public void SendMessage(object target,int code) => configuration.Session.SendMessage(this,target,code);
    public ActorProcedureState? RecoverShooter(object? handle) => configuration.Session.RecoverActor(handle);
    public ActorBodyMotion? RecoverBody(ActorProcedureState actor) => configuration.Session.RecoverBody(actor);
    public void ReceiveMessage(int code,object? senderHandle=null,FixedVector3 vector=default) =>
        Messages.Enqueue(new(code,senderHandle) { Vector0=vector });
    public bool Die(int stage) => HostageDeathLifecycle.Step(State,Actor,this,stage);
    public void SendDeathPulse()
    {
        // CBaddy::field_211 suppresses every subsequent death pulse from this body.
        if(deathPulseSent || Definition.NodeId==ushort.MaxValue)return;
        deathPulseSent=true;
        configuration.Session.SendDeathPulse(this);
    }
    public void MarkDead() => Lifecycle.Kill();
    public void KillShadow() => Appearance.KillShadow();
    public void Retire() { SendDeathPulse();State.BodyFlags|=0x40;ItemFlags|=1;MarkDead(); }
    public void ReportUnknownSubstate(HostageBehaviorState state,int substate) => throw new InvalidOperationException($"Hostage{Definition.NodeId}: source state{state}/{substate} is invalid.");
    public FixedVector3 GetDynamicHookPosition(ActorHook hook)
    {
        RequireModel();
        for(int part=0;part<hookPose.Length;part++)hookPose[part]=Model.GetSourcePartPose(part);
        return ActorHookPosition.Evaluate(Position,SourceTransformTranslation,SourceTransformBasis,hookPose,hook);
    }
    public bool CollideWithPartBoxes(FixedVector3 start,FixedVector3 end,int scale,out ActorHook hit)
    {
        RequireModel();
        for(int part=0;part<hookPose.Length;part++)hookPose[part]=Model.GetSourceAnimationPartPose(part);
        ActorAngles angles=Actor.Body.Angles;
        FixedJointTransform basis=FixedJointTransform.FromJoint(new(angles.X,angles.Y,angles.Z,0,0,0));
        return ActorPartCollision.TryCollide(Position,basis,hookPose,boxes,HiddenParts,start,end,scale,out hit);
    }
    public ActorCollisionBody CollisionSnapshot() => new(this,Type,Position,State.MinorRadius,State.BodyFlags,State.Health);
    /// <summary>CHostage inherits CBody::Hit (ob.cpp154), which accepts the contact without damage; shooter message3 owns its death.</summary>
    public bool Hit(ActorHit hit) => true;
    private void RequireModel()
    { if(Model is null)throw new NotSupportedException("Original hostage constructor invoked animation before its factory selected the source model."); }
    private void SyncScriptStorage()
    {
        Variables.Position=Actor.Body.Position;Variables.Health=State.Health;Variables.CollisionRadius=State.MinorRadius;
        Variables.GroundOffset=State.SurfaceClearance;Variables.ItemFlags=State.ItemFlags;
        ScriptState.BehaviorFlags=Actor.BehaviorFlags;
    }
    public short ReadVariable(ushort variable,ActorScriptCursor operands) { SyncScriptStorage();return Variables.Read(variable,operands); }
    public void WriteVariable(ushort variable,ActorScriptCursor operands)
    {
        SyncScriptStorage();Variables.Write(variable,operands);
        Actor.Body.Position=Variables.Position;State.Health=Variables.Health;State.MinorRadius=Variables.CollisionRadius;
        State.SurfaceClearance=Variables.GroundOffset;State.ItemFlags=Variables.ItemFlags;
        if(Model is not null)Model.PlaybackSpeed=Variables.AnimationSpeed;
    }
    public bool ExecuteCommand(ushort opcode,ActorScriptCursor operands,ActorScriptEnvironment environment)
    {
        if(GodotLinkedBaddyCommands.TryExecute(registry,SourceNodeId,opcode,operands))return true;
        if(GodotActorLinkEventCommands.TryExecute(registry,opcode,operands,ReadVariable))return true;
        if(GodotActorDirectEventCommands.TryExecute(registry,opcode,operands,ReadVariable))return true;
        if(GodotEnvironmentChunkCommands.TryExecute(registry,opcode,operands))return true;
        if(GodotActorScreenFlashCommands.TryExecute(registry,opcode,operands))return true;
        if (GodotActorSparkCommands.TryExecute(registry, opcode, operands, SourcePosition, Actor.Body.Angles)) return true;
        if (GodotActorCameraShakeCommands.TryExecute(registry, opcode, operands, ReadVariable, SourcePosition)) return true;
        if (GodotActorVisualEffectCommands.TryExecute(registry, opcode, operands, SourcePosition)) return true;
        if(Route.TryExecuteCommand(opcode,operands))return true;
        if(opcode==0x470F){Actor.BehaviorFlags|=0x02000000;return true;}
        if(opcode==0x4227)
        {
            // CBaddy::ExecuteCommand reads a raw word, not a variable expression.
            // field_2F0 is the route word; fields212/213 own SetHeight query timing.
            ushort interval=operands.ReadWord();
            Route.Flags|=4;
            Height.QueryInterval=unchecked((byte)interval);
            Height.QueryElapsed=0;
            return true;
        }
        if(configuration.Session.ExecuteScriptCommand(this,opcode,operands,environment))return true;
        throw new NotSupportedException($"Hostage{Definition.NodeId}: original script command0x{opcode:X4} is not connected.");
    }
    public bool HasLineOfSight(FixedVector3 from,FixedVector3 to)
    { FixedVector3 hit=default;return World.LineOfSight(from,to,0,ref hit); }
    public int Random(int bound) => configuration.Random(bound);
    public ushort FirstLink(ushort node) => registry.FirstLink(node);
    public void SelectModel(uint checksum) => registry.SelectModel(checksum);
    public ActorScriptCursor ScriptAtNode(ushort node) => registry.ScriptAtNode(node);
    public bool PlayerCeilingCheck(short x,short z) => registry.PlayerCeilingCheck(x,z);
    public void FlashCompass() => registry.FlashCompass();
}
