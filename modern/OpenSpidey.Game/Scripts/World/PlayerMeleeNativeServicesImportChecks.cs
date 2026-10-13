using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Audio;
using OpenSpidey.Core.Audio;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Import;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Runs on the genuine imported actors in the interaction fixture's separate physics world.</summary>
public static class PlayerMeleeNativeServicesImportChecks
{
    public static async Task VerifyFixtureAsync(Node3D owner,PlayerInteractionFixture fixture,PkrArchive archive,
        PsxLibraryRepository repository,OriginalPlayerData original)
    {
        GodotThugResources resources=new(archive,repository);
        var sounds=new GodotSoundChannels {Name="OriginalL1A3MeleeSounds",Archive=archive,LevelGroup=SoundCatalog.Find("data/L1A3")};
        fixture.Root.AddChild(sounds);
        sounds.Controller.PlayerBank=SoundDefinitionBank.Read(archive.Read("data/spidey.sfx"));
        sounds.Controller.LevelBank=SoundDefinitionBank.Read(archive.Read("data/L1A3.sfx"),levelSpecific:true);
        GodotSpeechPlayer speech=owner.GetNode<GodotSpeechPlayer>("Speech");
        var sourceThugs=new List<GodotThugActor>();
        var visuals=GodotThugVisualEffects.Create(archive,repository,sounds.Controller,
            ()=>new(fixture.Camera.SourcePosition>>12),fixture.Registry.RandomSource,()=>fixture.Clock);
        fixture.Root.AddChild(visuals);
        var npcRegistry=new GodotActorRegistry {Name="OriginalMeleeContactActors",Triggers=fixture.Registry.Triggers,Player=fixture.Player,
            Difficulty=fixture.Registry.Difficulty,Environment=fixture.Registry.Environment,RandomSource=fixture.Registry.RandomSource,
            BaddyConstructionState=fixture.Registry.BaddyConstructionState,
            Sounds=sounds.Controller,SoundListener=()=>new(fixture.Camera.SourcePosition>>12),ReceivePulse=fixture.Pulses.Add,SuspensionDistance=uint.MaxValue};
        fixture.Root.AddChild(npcRegistry);npcRegistry.SetPhysicsProcess(false);
        fixture.Directory.Bind(npcRegistry);
        using var actorDirectory=new GodotActorSessionDirectory(npcRegistry,fixture.Directory,new ActorSessionCheckPlatforms());
        var nativeEffects=new GodotThugNativeServices(visuals,sounds.Controller,()=>new(fixture.Camera.SourcePosition>>12),
            speech,fixture.Player,()=>GodotActorCollisionDirectory.SourceBaddyBodies(fixture.Directory),new ThugNativeServicesImportChecks.Interactions(),actorDirectory);
        var thugConfiguration=new GodotThugConfiguration
        {
            ModelFactory=resources.CreateModel,Player=fixture.Player,Combat=new(),SourceBaddyOrder=()=>sourceThugs.AsEnumerable().Reverse().ToArray(),
            Effects=nativeEffects,SpitParticles=visuals.SpitParticles,SpriteRings=visuals.SpriteRings,
            AttackClock=()=>unchecked((int)fixture.Clock),Timer=()=>fixture.Clock,Random=fixture.Registry.RandomSource,IsSourceCulled=_=>false,
            SourceConstructionOrdinal=npcRegistry.BaddyConstruction.RequireCurrentOrdinal
        };
        var hostageResources=new GodotHostageResources(archive,repository);
        var hostageSession=new HostageActorImportChecks.Session(fixture.Registry.Triggers);
        var sharedSession=new GodotActorSessionServices(actorDirectory,new() {
            DomeCount=()=>nativeEffects.DomeCount,IgnoreSphereRadius=()=>nativeEffects.IgnoreSphereRadius,
            PlayerWebComboValue=()=>nativeEffects.PlayerWebComboValue,SpawnGrenade=nativeEffects.SpawnGrenade,
            NotifyKill=nativeEffects.NotifyKill,GrabUpdate=fixture.Melee.ZipDive.GrabUpdate,RecoverWeb=nativeEffects.RecoverWeb,
            SetPlayerWebCombo=nativeEffects.SetPlayerWebCombo,SelectHostageCamera=nativeEffects.SelectHostageCamera,
            ExecuteScriptCommand=(actor,opcode,operands,environment)=>actor switch {
                GodotThugActor thugActor=>nativeEffects.ExecuteScriptCommand(thugActor,opcode,operands,environment),
                GodotHostageActor hostageActor=>hostageSession.ExecuteScriptCommand(hostageActor,opcode,operands,environment),
                _=>throw new NotSupportedException("Native NPC message proof cannot execute another family's scripts.") }
        });
        var hostageConfiguration=new GodotHostageConfiguration
        {
            ModelFactory=hostageResources.CreateModel,CollisionBoxes=hostageResources.CollisionBoxes,Player=fixture.Player,
            ShadowTexture=repository.Load("shell_dc").Textures.Single(texture=>texture.Checksum==0x9F6CA828),
            SourceBaddyOrder=()=>fixture.Directory.SourceBodies.Select(body=>new ActorCollisionBody(body.Identity,body.Type,body.Position,body.Radius,body.BodyFlags,
                body.Native switch {GodotThugActor thug=>thug.State.Health,GodotHostageActor hostage=>hostage.State.Health,_=>throw new NotSupportedException("Native melee NPC setup encountered an unsupported source body health owner.")})).ToArray(),
            Speech=speech,Session=sharedSession,Clock=()=>fixture.Clock,AttackClock=()=>unchecked((int)fixture.Clock),
            Random=fixture.Registry.RandomSource,IsSourceCulled=_=>false,
            SourceConstructionOrdinal=npcRegistry.BaddyConstruction.RequireCurrentOrdinal
        };
        GodotThugActor? thug=null;
        GodotHostageActor? hostage=null;
        try
        {
            int thugOrdinal=npcRegistry.BaddyConstruction.State.Count;
            thug=npcRegistry.BaddyConstruction.Create(304,
                ()=>GodotThugActor.Create(fixture.Registry.Triggers.Nodes[8],npcRegistry,thugConfiguration));
            if(thug.State.SourceConstructionOrdinal!=thugOrdinal || thug.State.ActorPhase!=unchecked((byte)thugOrdinal))
                throw new InvalidDataException("Actual L1A3 thug did not capture the shared source-scene CBaddy phase.");
            if(thug.Type!=304)throw new InvalidDataException("L1A3 node8 must be the genuine type304 thug.");
            npcRegistry.Adopt(thug);sourceThugs.Add(thug);if(!fixture.Directory.Contains(thug))fixture.Directory.RegisterBody(thug);
            if((thug.Actor.BehaviorFlags&0x02000000)==0)throw new InvalidDataException("Genuine thug node8 constructor script470F must set source body field2A8 bit02000000.");
            int hostageOrdinal=npcRegistry.BaddyConstruction.State.Count;
            hostage=npcRegistry.BaddyConstruction.Create(305,
                ()=>GodotHostageActor.Create(fixture.Registry.Triggers.Nodes[17],npcRegistry,hostageConfiguration));
            if(hostage.State.SourceConstructionOrdinal!=hostageOrdinal || hostage.State.ActorPhase!=unchecked((byte)hostageOrdinal))
                throw new InvalidDataException("Actual L1A3 hostage did not capture the count after the earlier real thug construction.");
            npcRegistry.Adopt(hostage);if(!fixture.Directory.Contains(hostage))fixture.Directory.RegisterBody(hostage);
            IReadOnlyList<ActorCollisionBody> fullBodies=GodotActorCollisionDirectory.SourceBaddyBodies(fixture.Directory);
            if(!ReferenceEquals(fullBodies[0].Identity,hostage) || !fullBodies.Any(body=>ReferenceEquals(body.Identity,thug)) ||
                fullBodies[0].Health!=hostage.State.Health || fullBodies[0].MinorRadius!=hostage.State.MinorRadius)
                throw new InvalidDataException("Full source collision list must include the genuine hostage ahead of the earlier thug, with actual health/radius.");
            foreach(GodotManipulationDebris debris in fixture.Directory.SourceBodies.Select(body=>body.Native).OfType<GodotManipulationDebris>())
                if(!fullBodies.Any(body=>ReferenceEquals(body.Identity,debris) && body.Type==401 && body.MinorRadius==0 && body.Health==debris.Variables.Health))
                    throw new InvalidDataException("Full collision list lost actual zero-radius transient401 metadata.");
            GodotSwitchActor control=fixture.Directory.SourceControls.FirstOrDefault() ?? fixture.SpawnSwitch(176);
            await owner.ToSignal(owner.GetTree(),SceneTree.SignalName.PhysicsFrame);
            await owner.ToSignal(owner.GetTree(),SceneTree.SignalName.PhysicsFrame);
            Verify(fixture.Root,fixture.Player,fixture.Directory,fixture.NativeServices,original,resources,thug,hostage,control,fixture.Impacts,fixture.WorldQuery,repository.Load("items"));
            if(sounds.StartedCount==0)throw new InvalidDataException("Actual thug pain must start native playback from the original L1A3 level bank.");
            GrenadeSceneServicesImportChecks.VerifyFixture(fixture,archive,repository,visuals,sounds.Controller,thug,hostage);
            await TrapWebImportChecks.VerifyAsync(owner,fixture,archive,thug,visuals);
            await PlayerWebTugImportChecks.VerifyAsync(owner,fixture,archive,thug,visuals);
            ActorSessionDirectoryImportChecks.Verify(fixture,actorDirectory,nativeEffects,thug,hostage);
        }
        finally
        {
            if(thug is not null)fixture.Directory.Remove(thug);
            if(hostage is not null)fixture.Directory.Remove(hostage);
            fixture.Directory.Unbind(npcRegistry);npcRegistry.Free();
            visuals.Free();sounds.Free();
        }
    }
    private static void Verify(Node3D fixture,GodotPlayer player,GodotPlayerMeleeDirectory directory,
        GodotPlayerMeleeNativeServices services,OriginalPlayerData original,GodotThugResources resources,
        GodotThugActor thug,GodotHostageActor hostage,GodotSwitchActor control,GodotPlayerCombatImpacts impacts,GodotActorWorldQuery world,PsxAssetLibrary items)
    {
        if(fixture.GetWorld3D().Space==fixture.GetTree().Root.GetWorld3D().Space)
            throw new InvalidDataException("Native player melee requires a fresh isolated imported physics world.");
        int checks=0;
        void Assert(bool condition,string description)
        {if(!condition)throw new InvalidDataException($"Native player melee: {description}");checks++;}
        GodotPlayerMelee melee=player.Melee ?? throw new InvalidOperationException("The imported fixture player has no melee owner.");
        var thugBody=directory.RequireBody(thug);
        var hostageBody=directory.RequireBody(hostage);
        Assert(directory.SourceControls.Contains(control),"Original switch belongs to the actual control directory.");
        PlayerKickHandle handle=directory.MakeHandle(thug);
        uint validation=handle.Validation;
        directory.Suspend(thug);
        Assert(!directory.SourceBodies.Contains(thugBody) && ReferenceEquals(directory.Recover(ref handle),thug),
            "Suspension removes selection membership while retaining the original weak handle.");
        directory.Resume(thug);
        Assert(ReferenceEquals(directory.BaddyHead,thugBody) && directory.RequireBody(thug).Validation==validation,
            "Resume prepends the same source body with the same validation.");
        directory.SetMeasuredDistance(thug,123);
        Assert(thugBody.CachedPlayerDistance==123,"Registry distance snapshot is stored without recomputation.");
        directory.CaptureBeforeActorUpdate(thug,player.Controller.Position);
        Assert(thugBody.CachedPlayerDistance==ActorLifecycle.Distance(player.Controller.Position,thug.Position),
            "Distance uses the actual native body position and original whole-unit 3D length.");

        services.BuildTargetTransform(thugBody);
        IReadOnlyList<ActorPartBox> boxes=resources.CollisionBoxes(thug.State.ModelName!);
        FixedJointTransform[] rawPose=Enumerable.Range(0,boxes.Count).Select(thug.Model.GetSourceAnimationPartPose).ToArray();
        bool found=false;
        ActorHook contact=default;
        FixedVector3 from=default,to=default;
        // Construct a face-crossing segment from genuine signed part bounds; no synthetic actor AABB.
        for(int part=1;part<boxes.Count && !found;part++)
        {
            ActorPartBox box=boxes[part];
            int lower=(box.LowerX*0x1800)>>12,upper=(box.UpperX*0x1800)>>12;
            int y=(((int)box.LowerY+box.UpperY)*0x1800)>>13;
            int z=(((int)box.LowerZ+box.UpperZ)*0x1800)>>13;
            if(upper<=lower || box.UpperY<=box.LowerY || box.UpperZ<=box.LowerZ)continue;
            from=ActorHookPosition.Evaluate(thug.Position,thug.SourceTransformTranslation,thug.SourceTransformBasis,rawPose,new(unchecked((short)((lower-8)<<4)),unchecked((short)(y<<4)),unchecked((short)(z<<4)),(ushort)part));
            to=ActorHookPosition.Evaluate(thug.Position,thug.SourceTransformTranslation,thug.SourceTransformBasis,rawPose,new(unchecked((short)((upper+8)<<4)),unchecked((short)(y<<4)),unchecked((short)(z<<4)),(ushort)part));
            found=services.CollideWithTarget(thugBody,from,to,0x1800,out contact);
        }
        Assert(found && contact.PartIndex>0 && contact.PartIndex<boxes.Count,"Actual animated part boxes accept a strict face crossing.");
        Assert(!services.CollideWithTarget(thugBody,from,from,0x1800,out _),"Zero-length contact does not become a whole-body hit.");
        FixedVector3 far=new(3000<<12,0,0);
        Assert(!services.CollideWithTarget(thugBody,from+far,to+far,0x1800,out _),"A separated native segment misses every animated part box.");
        FixedVector3 impact=services.GetTargetDynamicHookPosition(thugBody,contact);
        Assert(impact!=default,"Contact is placed by the current source dynamic hook transform.");
        short health=thug.State.Health;
        Assert(health>1,"The genuine fixture thug has enough health for a nonlethal one-point source hit.");
        int stateBefore=thug.State.State;
        ushort stateFlagsBefore=thug.StateFlags.Check(stateBefore);
        uint actionFlagsBefore=thug.State.ActionFlags;
        bool accepted=services.HitTarget(thugBody,new(4,unchecked((byte)contact.PartIndex),0,1,default));
        GD.Print($"Native thug hit diagnostic: state={stateBefore}, stateFlags={stateFlagsBefore:X4}, actionFlags={actionFlagsBefore:X8}, health={health}->{thug.State.Health}, accepted={accepted}.");
        Assert(thug.State.Health==health-1,"Native Hit must apply source flag4 health damage before reaction eligibility is checked.");
        // CBaddy initializing state0 has flags19: bit10 rejects TakeHit after health/pain effects.
        Assert(stateBefore==0 && stateFlagsBefore==19 && !accepted,
            "A genuine newly constructed thug applies damage but rejects a reaction while initializing, exactly as CThug::Hit.");
        short hostageHealth=hostage.State.Health;
        Assert(services.HitTarget(hostageBody,new(6,0,8,5,default)) && hostage.State.Health==hostageHealth,"Hostage uses its genuine inherited CBody::Hit acceptance.");
        int rings=impacts.Rings.Count,lines=impacts.Lines.Count;
        services.CreateCombatImpactEffect(impact,0);
        Assert(impacts.Rings.Count==rings+2 && impacts.Lines.Count==lines+6,"Actual player impact emits two original rings and six line particles.");

        melee.SelectAndResetAnimationSounds(21);
        PlayerFrameSound firstSound=player.FrameSounds.Update(21,1) ?? throw new InvalidDataException("Original running frame1 has no actual acoustic event.");
        melee.PlaySound(checked((ushort)firstSound.Sound),firstSound.Volume,firstSound.PitchOffset);
        PlayerFrameSound secondSound=player.FrameSounds.Update(21,11) ?? throw new InvalidDataException("Original running frame11 has no actual acoustic event.");
        melee.PlaySound(checked((ushort)secondSound.Sound),secondSound.Volume,secondSound.PitchOffset);
        Assert(firstSound.Sound is >=1 and <=4 && secondSound.Sound is >=1 and <=4 && firstSound.Sound!=secondSound.Sound && player.FrameSounds.Cursor==2,
            "Actual acoustic running events consume the shared RNG, avoid repeated steps and invoke real native sound playback.");
        Assert(services.AnimationSoundCursor==0 && services.SelectedAnimationSoundWords is {Count:3} words &&
            words[0]==1 && words[1]==11 && words[2]==uint.MaxValue,"Genuine original running sound stream is selected and reset.");
        services.ProcessAnimationSoundWords(21,0);
        Assert(services.AnimationSoundCursor==0,"Sound word does not consume before its source frame.");
        services.ProcessAnimationSoundWords(21,11);
        Assert(services.AnimationSoundCursor==1 && services.SelectedAnimationSoundWords![0]==0xFFFF0001,
            "A source update marks only one overdue sound word.");
        services.ProcessAnimationSoundWords(21,11);
        Assert(services.AnimationSoundCursor==2 && services.SelectedAnimationSoundWords![1]==0xFFFF000B,
            "The following update consumes the second word rather than skipping it.");
        services.ProcessAnimationSoundWords(21,11);
        Assert(services.SelectedAnimationSoundWords is null && services.AnimationSoundCursor==2,"Original terminator ends the sound stream on the following source update.");
        services.SelectAndResetAnimationSounds(21);
        Assert(services.SelectedAnimationSoundWords![0]==1 && services.SelectedAnimationSoundWords[1]==11,
            "Replay clears original high-word consumed flags.");
        services.SelectAndResetAnimationSounds(100);
        Assert(services.SelectedAnimationSoundWords is null && !original.AnimationSounds.ContainsKey(100),
            "An actual null fight sound slot remains null without invented footsteps.");

        PlayerSwitchCandidate candidate=services.SourceControlOrder.Single(entry=>ReferenceEquals(entry.Identity,control));
        PlayerSwitchAimPoint? point=services.GetSwitchAutoAimPoint(candidate);
        Assert(point is not null && point == control.AutoAimPoint,"Switch targeting uses the distinct genuine ENV interior identity and position.");
        GodotSwitchEnvironmentItem visibleItem=(GodotSwitchEnvironmentItem)(control.State.Mode==OpenSpidey.Core.Actors.Switches.SourceSwitchMode.Off
            ?control.State.OffItem!.Identity:control.State.OnItem!.Identity);
        bool switchContact=false;
        foreach(ImportedCollisionBody body in visibleItem.Colliders)
        {
            foreach(var triangle in body.SourceTriangles)
            {
                Vector3 center=visibleItem.Mesh.Transform*PsxCoordinates.WorldPosition((triangle.A+triangle.B+triangle.C)/3);
                Vector3 normal=(visibleItem.Mesh.Transform.Basis*PsxCoordinates.WorldPosition(triangle.Normal)).Normalized();
                FixedVector3 rayStart=PsxCoordinates.OriginalWorldPosition(center+normal*.16f);
                FixedVector3 rayEnd=PsxCoordinates.OriginalWorldPosition(center-normal*.16f);
                GodotActorWorldHit? nativeContact=world.Trace(rayStart,rayEnd);
                if(nativeContact is null || !visibleItem.Colliders.Any(item=>ReferenceEquals(item,nativeContact.Collider)))continue;
                PlayerKickTrace kickContact=services.TraceKickItem(rayStart,rayEnd,1);
                PlayerSwitchTrace aimContact=services.LineToItem(rayStart,rayEnd,1);
                switchContact=ReferenceEquals(kickContact.Item,visibleItem.State.Identity) && ReferenceEquals(aimContact.Item,visibleItem.State.Identity) &&
                    aimContact.FaceFlags==nativeContact.Triangle.PackedMetadata;
                break;
            }
            if(switchContact)break;
        }
        Assert(switchContact,"Actual switch face resolves its claimed ENV identity and original face flags through both player queries.");
        PlayerKickHandle switchHandle=services.MakeSwitchHandle(candidate);
        Assert(ReferenceEquals(directory.Recover(ref switchHandle),control),"Switch target stores the owning control actor handle.");

        FixedVector3 savedPosition=player.Combat.State.Position;
        int savedElapsed=player.Combat.State.ElapsedTicks;
        bool savedLookaround=player.Combat.State.LookaroundActive;
        try
        {
            services.DeleteReticle();
            directory.Suspend(hostage);
            // Place the isolated player three hundred units behind the real target in its source89C frame.
            FixedVector3 forward=player.Controller.Orientation.Forward;
            player.Combat.State.Position=thug.Position+forward*300;
            player.Combat.State.ElapsedTicks=2;
            player.Combat.State.LookaroundActive=false;
            directory.CaptureBeforeActorUpdate(thug,player.Combat.State.Position);
            Assert(services.LineOfSight(player.Combat.State.Position,thug.Position,0,0),"Original target placement has a clear imported-world visibility ray.");
            services.UpdateAutoAimTarget(melee);
            Assert(ReferenceEquals(melee.AutoAim.Target,thugBody) && services.HasReticle,"Actual selector allocates the original items reticle for the real body.");
            GodotPlayerTargetReticle nativeReticle=fixture.GetChildren().OfType<GodotPlayerTargetReticle>().Single();
            Assert(items.Scene.Models[nativeReticle.ModelIndex].Checksum==0xB08EC1FB && nativeReticle.GetNode<MeshInstance3D>("OriginalItemsReticleModel").Mesh!.GetSurfaceCount()>0,
                "Native type503 reticle renders the actual original items model B08EC1FB.");
            Assert(services.ReticlePosition==(thug.Position with{Y=unchecked(thug.Position.Y-(128<<12))}) && services.ReticleYaw==32,
                "Reticle uses source body height and exact two-tick yaw advance.");
            player.Combat.State.LookaroundActive=true;
            services.UpdateAutoAimTarget(melee);
            Assert(!services.HasReticle && ReferenceEquals(melee.AutoAim.Target,thugBody),"Source lookaround removes the actual reticle while preserving DCC target.");
        }
        finally
        {
            player.Combat.State.Position=savedPosition;
            player.Combat.State.ElapsedTicks=savedElapsed;
            player.Combat.State.LookaroundActive=savedLookaround;
            directory.Resume(hostage);
            services.DeleteReticle();
        }
        directory.Remove(thug);
        Assert(directory.Recover(ref handle) is null && handle.Target is null && handle.Validation==validation,
            "Removal invalidates captured handles immediately before native node destruction.");
        GodotPlayerMeleeDirectory.Body replacement=directory.RegisterBody(thug);
        PlayerKickHandle replacementHandle=directory.MakeHandle(thug);
        Assert(replacement.Validation!=validation && ReferenceEquals(directory.Recover(ref replacementHandle),thug) && directory.Recover(ref handle) is null,
            "Reinserted native identity receives a new generation without reviving the old source handle.");
        GD.Print($"Verified {checks} native player melee service checks: genuine posed contacts, live damage/effects, directory lifecycle, original sound words, switch identities and original target reticle.");
    }
}
