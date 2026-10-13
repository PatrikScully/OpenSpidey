using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Hostages;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Original L1A3 hostages in their own native physics world; never claims first-level models.</summary>
public static class HostageActorImportChecks
{
    public static async Task VerifyAsync(Node3D owner,PkrArchive archive,PsxLibraryRepository repository,
        Func<ImportedAnimatedModel> playerModelFactory,IReadOnlyList<ActorPartBox> boxes)
    {
        TriggerFile triggers=TriggerFile.Read(archive.Read("data/L1A3_T.trg"));
        var viewport=new SubViewport { Name="OriginalHostageCheckWorld",OwnWorld3D=true,Size=new(640,480) };
        owner.AddChild(viewport);
        var fixture=new Node3D { Name="OriginalL1A3Hostages" };
        viewport.AddChild(fixture);
        int assertions=0;
        uint clock=0;
        var random=new GameRandom(0x12A6CC58);
        var session=new Session(triggers);
        try
        {
            HostageModelImportChecks.Run(archive,fixture);
            fixture.AddChild(PsxWorldCollisionBuilder.Build(repository.Load("data/L1A3_G.psx").Scene,"data/L1A3_G.psx"));
            fixture.AddChild(PsxWorldCollisionBuilder.Build(repository.Load("data/L1A3_O.psx").Scene,"data/L1A3_O.psx"));
            TriggerActorDefinition placement=TriggerActorDefinition.Read(triggers.Nodes[17]);
            var player=GodotPlayer.Create(new FixedVector3(placement.Position.X,placement.Position.Y,placement.Position.Z)<<12,
                placement.Orientation.Y,playerModelFactory(),new(true,new(false,10,true)),random:random.Next);
            fixture.AddChild(player);player.SetPhysicsProcess(false);
            player.Combat.ConfigureCollisionBoxes(boxes);
            var registry=new GodotActorRegistry { Triggers=triggers,Player=player,RandomSource=random.Next,Difficulty=1 };
            fixture.AddChild(registry);registry.SetPhysicsProcess(false);
            var resources=new GodotHostageResources(archive,repository);
            var configuration=new GodotHostageConfiguration
            {
                ModelFactory=resources.CreateModel,CollisionBoxes=resources.CollisionBoxes,Player=player,
                ShadowTexture=repository.Load("shell_dc").Textures.Single(texture=>texture.Checksum==0x9F6CA828),
                SourceBaddyOrder=()=>session.SourceOrder.Select(actor=>actor.CollisionSnapshot()).ToArray(),
                Speech=owner.GetNode<GodotSpeechPlayer>("Speech"),Session=session,Clock=()=>clock,
                AttackClock=()=>unchecked((int)clock),Random=random.Next,IsSourceCulled=_=>false,
                SourceConstructionOrdinal=registry.BaddyConstruction.RequireCurrentOrdinal
            };
            await Frames();
            Assert(fixture.GetWorld3D().Space!=owner.GetWorld3D().Space,"Hostage collision queries use a separate native physics space.");
            GodotHostageActor rescued=Create(17),dying=Create(22);
            Assert(session.SourceOrder.SequenceEqual(new[]{dying,rescued}),"Actual constructor insertion preserves newest-first source baddy order.");
            Assert(rescued.Type==305 && rescued.State.SurfaceClearance==100 && rescued.State.MinorRadius==128,
                "Original node17 constructs actual type305, its male model and factory defaults after parsing the script.");
            Assert((rescued.Actor.BehaviorFlags&0x02000000)!=0 && rescued.Model.ClipCount==7,
                "Original470F constructor script sets the body behavior bit and uses all fifteen raw parts.");
            await Step();
            Assert(rescued.State.State==HostageBehaviorState.WaitingForPlayer && rescued.State.GroundHeight!=-1,
                "Original node17 startup finds its genuine L1A3 ground and enters WaitForPlayer.");
            Assert(rescued.World.LastHit?.Collider is ImportedCollisionBody { SourceAsset:"data/L1A3_G.psx" or "data/L1A3_O.psx" },
                "Startup ground came from an original imported L1A3 face, with no synthetic floor or other world.");
            Assert(rescued.Position.Y==rescued.State.GroundHeight-(100<<12),"Hostage startup applies exact source ground clearance.");
            Node parent=rescued.GetParent();
            var reparent=new Node3D { Name="ActualHostageReparentProof" };fixture.AddChild(reparent);
            GodotScriptActor[] ordered=session.SourceOrder.Cast<GodotScriptActor>().ToArray();
            ActorProcedureState capturedRescued=rescued.Actor;
            var protectedShadow=rescued.Appearance.Shadow;
            rescued.ReceiveMessage(10,null,new(12345,-54321,67890));
            HostageMessage pending=rescued.Messages.Head!;
            parent.RemoveChild(rescued);
            Assert(session.SourceOrder.SequenceEqual(ordered) && ReferenceEquals(session.RecoverBody(capturedRescued),rescued.Actor.Body) &&
                ReferenceEquals(rescued.Messages.Head,pending) && GodotObject.IsInstanceValid(protectedShadow) && registry.BaddyConstruction.State.Count==2,
                "Removing a real305 native child retains its source-list membership, captured body, pending message, protected shadow and constructor lease.");
            reparent.AddChild(rescued);
            rescued.Appearance.Synchronize();rescued.Model.VerifyCurrentPose();
            Assert(ReferenceEquals(rescued.Appearance.Shadow,protectedShadow) && GodotObject.IsInstanceValid(protectedShadow) &&
                session.SourceOrder.SequenceEqual(ordered) && ReferenceEquals(session.RecoverActor(rescued),capturedRescued),
                "Reparenting real305 retains and rebuilds the same original shadow/native appearance resources and weak source body.");
            reparent.RemoveChild(rescued);parent.AddChild(rescued);reparent.Free();
            player.Controller.SetScriptPosition(rescued.Position);
            await Step();
            Assert(rescued.State.State==HostageBehaviorState.GettingUp && session.Signals.Count==3,
                "A real nearby player frees the hostage and sends a signal to each original linked node.");
            Assert(session.Signals.Select(entry=>entry.Target).SequenceEqual(new ushort[]{18,62,231}),
                "Rescue signal order matches original node17 links18,62,231.");
            for(int update=0;update<240 && rescued.State.State!=HostageBehaviorState.FollowingWaypoints;update++)await Step();
            Assert(rescued.State.State==HostageBehaviorState.FollowingWaypoints && rescued.State.CurrentWaypoint==18,
                "The real get-up clip finishes and advances to original waypoint18.");
            Assert((rescued.Route.Flags&4)!=0 && rescued.Height.QueryElapsed==0,
                "Original waypoint opcode4227 enables height queries and resets the source byte timer.");
            FixedVector3 before=rescued.Position;
            for(int update=0;update<24;update++)await Step();
            Assert(rescued.Position!=before,"Original MoveTo procedures move the hostage along its native route.");
            Assert(rescued.Model.ClipId<7 && !rescued.Model.HasSourceJoints,"Route playback stays in the real male bank without a fabricated joint packet.");

            // A native sender identity is retained by the persistent queue, not replaced with a copied body.
            dying.ReceiveMessage(2,rescued);
            registry.Difficulty=2;
            await Step();
            Assert(ReferenceEquals(dying.State.ShooterHandle,rescued) && dying.State.State==HostageBehaviorState.Begging && dying.Model.ClipId==5,
                "Code2 captures the real sender and plays the source male begging clip.");
            Assert(ReferenceEquals(session.RecoverActor(rescued),rescued.Actor) && ReferenceEquals(session.RecoverBody(rescued.Actor),rescued.Actor.Body),
                "Captured LookAt resolves the native source body independently of the stored message handle.");
            dying.ReceiveMessage(3,rescued);
            await Step();
            Assert(dying.State.State==HostageBehaviorState.Dying && dying.Model.ClipId==3 && (dying.BodyFlags&8)==0,
                "Code3 begins the actual death animation, transparency and shadow removal.");
            Assert(dying.State.TintColor==0xFF3F3F,"The first source death blink uses exact packed tint0xFF3F3F.");
            dying.Appearance.VerifyGeometry(95,95,255);
            dying.Appearance.VerifyTransparency(true);
            for(int update=0;update<40 && dying.Lifecycle.Phase!=ActorLifePhase.Dead;update++)await Step();
            Assert(dying.Lifecycle.Phase==ActorLifePhase.Dead && (dying.BodyFlags&0x40)!=0 && (dying.ItemFlags&1)!=0,
                "The source blink/fade sequence marks a genuine native body dead.");
            Assert(session.Pulses.Select(entry=>entry.Target).SequenceEqual(new ushort[]{23,62,231}),
                "Death delivers the actual original node22 linked pulse targets once and in order.");
            dying.SendDeathPulse();
            Assert(session.Pulses.Count==3,"Source field211 suppresses repeated death pulses.");
            ActorProcedureState captured=dying.Actor;
            Assert(dying.Lifecycle.PrepareUpdate(0,13000)==ActorUpdateAction.Skip && GodotObject.IsInstanceValid(dying),
                "The first dead-actor pass retains the native identity while marking pending deletion.");
            Assert(dying.Lifecycle.PrepareUpdate(0,13000)==ActorUpdateAction.Delete,"The second dead-actor pass requests source deletion.");
            dying.ReceiveMessage(1,rescued);
            Assert(dying.Messages.Head is { Code:1 },"A genuine pending source message remains owned until actual native destruction.");
            dying.Free();
            Assert(session.RecoverActor(dying) is null && session.RecoverBody(captured) is null,
                "Deleted native handles and previously captured LookAt bodies cannot recover or receive messages.");
            Assert(session.SourceOrder.SequenceEqual(new[]{rescued}) && dying.Messages.Head is null &&
                !GodotObject.IsInstanceValid(dying.Appearance.Shadow) && registry.BaddyConstruction.State.Count==1,
                "Actual native Predelete removes305 once, clears its real message queue and releases the protected shadow and base lease.");
            GD.Print($"Passed {assertions} native hostage checks: isolated original L1A3 startup, rescue/route, native messages, speech, tint/shadow, death pulse and two-pass weak identity.");

            GodotHostageActor Create(int node)
            {
                short type=TriggerActorDefinition.Read(triggers.Nodes[node]).ActorType;
                int ordinal=registry.BaddyConstruction.State.Count;
                GodotHostageActor actor=registry.BaddyConstruction.Create(type,
                    ()=>GodotHostageActor.Create(triggers.Nodes[node],registry,configuration));
                Assert(actor.State.SourceConstructionOrdinal==ordinal && actor.State.ActorPhase==unchecked((byte)ordinal),
                    $"Original hostage node{node} retains its actual shared base-constructor ordinal and byte phase.");
                fixture.AddChild(actor);actor.Begin();return actor;
            }
            async Task Step()
            {
                await Frames();clock=unchecked(clock+2);
                foreach(GodotHostageActor actor in session.SourceOrder)
                {
                    if(actor.Lifecycle.Phase!=ActorLifePhase.Active)continue;
                    actor.Advance();
                    Assert(actor.FrameTiming.LastUpdateTick==clock && actor.FrameTiming.ElapsedTicks==2 && (actor.BodyFlags&4)==0,
                        "Actual ordered hostages share one source clock increment and the common EveryFrame timing owner.");
                }
            }
        }
        finally{viewport.Free();}
        async Task Frames()
        {
            await owner.ToSignal(owner.GetTree(),SceneTree.SignalName.PhysicsFrame);
            await owner.ToSignal(owner.GetTree(),SceneTree.SignalName.PhysicsFrame);
        }
        void Assert(bool valid,string detail)
        {if(!valid)throw new InvalidDataException($"Native hostage check: {detail}");assertions++;}
    }

    internal sealed class Session(TriggerFile triggers):IGodotHostageSessionHost
    {
        public List<GodotHostageActor> SourceOrder {get;}=[];
        public List<(ushort Sender,ushort Target)> Signals {get;}=[];
        public List<(ushort Sender,ushort Target)> Pulses {get;}=[];
        private readonly Dictionary<ActorProcedureState,WeakReference<GodotHostageActor>> captured=[];
        public void AttachToSourceBaddyList(GodotHostageActor actor)
        {SourceOrder.Insert(0,actor);captured.Add(actor.Actor,new(actor));}
        public void DetachFromSourceBaddyList(GodotHostageActor actor)
        {SourceOrder.Remove(actor);captured.Remove(actor.Actor);}
        public void SendSignal(GodotHostageActor actor)=>Deliver(actor,Signals);
        public void SendDeathPulse(GodotHostageActor actor)=>Deliver(actor,Pulses);
        private void Deliver(GodotHostageActor actor,List<(ushort Sender,ushort Target)> received)
        {
            foreach(ushort target in actor.Definition.Links)
            {
                if(target>=triggers.Nodes.Count)throw new InvalidDataException("Original hostage links must remain file-bounded.");
                received.Add((actor.Definition.NodeId,target));
                foreach(GodotHostageActor recipient in SourceOrder.Where(candidate=>candidate.Definition.NodeId==target))
                    recipient.ScriptState.PendingSignals|=1;
            }
        }
        public void SendMessage(GodotHostageActor sender,object target,int code)
        {
            if(target is not GodotHostageActor actor || RecoverActor(actor) is null)
                throw new InvalidOperationException("Hostage check tried sending to an unowned/deleted native body.");
            actor.ReceiveMessage(code,sender);
        }
        public ActorProcedureState? RecoverActor(object? handle)=>handle is GodotHostageActor actor &&
            GodotObject.IsInstanceValid(actor) && SourceOrder.Contains(actor)?actor.Actor:null;
        public ActorBodyMotion? RecoverBody(ActorProcedureState actor)=>captured.TryGetValue(actor,out var reference) &&
            reference.TryGetTarget(out GodotHostageActor? native) && RecoverActor(native) is not null?native.Actor.Body:null;
        public bool ExecuteScriptCommand(GodotHostageActor actor,ushort opcode,ActorScriptCursor operands,ActorScriptEnvironment environment)=>
            throw new NotSupportedException($"Native L1A3 hostage check reached unconnected opcode{opcode:X4} at node{actor.Definition.NodeId}.");
    }
}
