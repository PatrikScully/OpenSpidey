using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Actual player/304 target virtual web release and broken-strand lifetime in the existing original L1A3 world.</summary>
internal static class PlayerWebTugImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PlayerInteractionFixture fixture, PkrArchive archive,
        GodotThugActor thug, GodotThugVisualEffects visuals)
    {
        byte[] bits = archive.Read("data/bits.psx");
        var group = new Node3D { Name = "NativePlayerWebTugProof" }; fixture.Root.AddChild(group);
        var particles = new SourceParticleScheduler();
        var fragments = new GodotWebFragments(); group.AddChild(fragments);
        var glows = new GodotGlowFlash(); group.AddChild(glows);
        Func<int, int> random = new GameRandom(0x4F66A0).Next;
        var strands = GodotWebStrands.Create(bits); group.AddChild(strands);
        var projectiles = GodotImpactWebProjectiles.Create(bits, new ExcludedProjectileHost(), random); group.AddChild(projectiles);
        GodotPlayer player = fixture.Player; GodotPlayerMelee melee = fixture.Melee;
        PlayerCombatState combat = player.Combat.State;
        void FirstContact() => melee.Sense.Quips.SetFirstContactDetails(combat.Health, fixture.Clock);
        var contacts = GodotActorWebContacts.Create(bits, fixture.Directory,
            actor => visuals.Resources.CollisionBoxes(actor.State.ModelName!), fragments, random,
            position => GodotBouncingRocks.GetGroundY(fixture.WorldQuery, position),
            (_, _) => throw Outside("burning trap relocation"), FirstContact);
        group.AddChild(contacts);
        fragments.SourceBitCreated += bit => particles.Attach(SourceParticleList.GLine, bit);
        fragments.SourceBitRemoved += identity => particles.Detach(identity);
        glows.SourceBitCreated += bit => particles.Attach(SourceParticleList.Glow, bit);
        glows.SourceBitRemoved += identity => particles.Detach(identity);
        contacts.Emitted += bit => particles.Attach(SourceParticleList.NonRendered, bit);
        contacts.LineEmitted += bit => particles.Attach(SourceParticleList.GPolyLine, bit);
        contacts.QuadEmitted += bit => particles.Attach(SourceParticleList.Quad, bit);
        contacts.Removed += identity => particles.Detach(identity);
        contacts.LineRemoved += identity => particles.Detach(identity);
        contacts.QuadRemoved += identity => particles.Detach(identity);
        strands.PolylineEmitted += bit => particles.Attach(SourceParticleList.GPolyLine, bit);
        strands.KnotEmitted += bit => particles.Attach(SourceParticleList.Flat, bit);
        strands.PolylineRemoved += identity => particles.Detach(identity);
        strands.KnotRemoved += identity => particles.Detach(identity);
        var actions = new GodotPlayerFireWeb(player, fixture.Directory, fixture.Session, fixture.WorldQuery,
            projectiles, strands, contacts, fixture.Impacts.Lines, new()
            {
                Clock = () => fixture.Clock, CameraPositionWholeUnits = () => fixture.Camera.SourcePosition >> 12,
                Random = random, ActuatorTime = (_, _) => throw Outside("controller feedback"),
                PlayPositionSound = (sound, position, pitch) => unchecked((int)owner.GetNode<GodotSoundChannels>("SoundChannels")
                    .Controller.PlayAt(sound, position, new(fixture.Camera.SourcePosition >> 12))),
                CreateFireImpactWeb = (_, _, _) => throw Outside("fire projectile"),
                EmitKnottedSplat = (_, _, _, _) => throw Outside("environment splat")
            });
        var tug = new GodotPlayerWebTug(player, actions, strands, new()
        {
            Clock = () => fixture.Clock, CameraPositionWholeUnits = () => fixture.Camera.SourcePosition >> 12,
            Random = random, GroundY = position => GodotBouncingRocks.GetGroundY(fixture.WorldQuery, position),
            EmitFirePulse = (_, _, _) => throw Outside("fire strand"), SetFirstContactDetails = FirstContact, EmitSnapGlow = glows.Emit
        });
        short health = thug.State.Health; uint behavior = thug.Actor.BehaviorFlags, actionFlags = thug.State.ActionFlags;
        int state = thug.State.State, substate = thug.State.Substate, counter = thug.State.ActionCounter, pathCount = thug.State.PathPointIndex;
        FixedVector3 velocity = thug.Actor.Body.Velocity;
        FixedVector3 acceleration = thug.Actor.Body.Acceleration, localVelocity = thug.Actor.Body.LocalVelocity;
        ActorAngles angularVelocity = thug.Actor.Body.AngularVelocity, angularAcceleration = thug.Actor.Body.AngularAcceleration;
        FixedVector3[]? oldThrowPoints = thug.SnapshotThrowPoints();
        FixedVector3[] oldPath = Enumerable.Range(0, 6).Select(thug.Path.GetPoint).ToArray();
        int oldPathCount = thug.Path.PathCount, oldWaypoint = thug.Path.CurrentWaypoint, oldGroundY = thug.Path.StoredBodyY;
        var procedures = thug.Procedures.SourceOrder.Select(procedure => (Procedure: procedure, procedure.Flags)).ToArray();
        uint action = combat.ActionState; int release = combat.ActionProgress; short suppression = combat.FieldEA6;
        IPlayerMeleeTarget? aim = melee.State.AimTarget; byte shot = melee.State.WebAttackMode;
        int playerClip = player.Model.ClipId, playerFrame = player.Model.Playback.Frame;
        byte finished = player.Model.Playback.Finished;
        ushort animation = combat.AnimationId; int animationFrame = combat.AnimationFrame;
        int inventoryUnits = player.Inventory!.State.Webbing; byte fireVariant = player.Inventory.State.FireWebbing;
        byte encounter = melee.Sense.Quips.EncounterActive; int startingHealth = melee.Sense.Quips.StartingHealth;
        uint encounterClock = melee.Sense.Quips.EncounterClock, sourceClock = fixture.Clock;
        Transform3D camera = fixture.Camera.GlobalTransform;
        WebStrandOwner? web = null;
        try
        {
            thug.State.Health = Math.Max((short)1, health); thug.Actor.BehaviorFlags &= ~0x200u;
            melee.State.AimTarget = fixture.Directory.RequireBody(thug); melee.State.WebAttackMode = 2;
            player.Inventory.State.FireWebbing = 0;
            melee.PlaySingleAnimation(0x94, 0, -1); player.Model.SetAnimationFinished(0);
            combat.ActionState = 0x20000; combat.ActionProgress = 0;
            web = new WebStrandOwner(actions); actions.State.CurrentStrand = web;
            Assert(actions.FireWeb(new(true, 0, default, false, default)) == 2 && thug.Messages.Head is { Code: 6, SenderHandle: null },
                "Actual posed player FireWeb must resolve its genuine304 target, emit the original yank contact and enqueue null-sender message6.");
            thug.Messages.Head!.Flags |= 1; thug.Messages.Cleanup();
            KnottedWebStrand original = web.Strand ?? throw new InvalidDataException("Source native FireWeb did not own its knotted line.");
            FixedVector3 first = original.Start, last = original.Ends[^1];
            player.Model.SetSourceAnimationFrame(13); combat.AnimationFrame = 13; tug.UpdateThrow();
            Assert(ReferenceEquals(actions.State.CurrentStrand, web) && web.Mode == 1 && combat.ActionProgress == 0,
                "Source action20000 clip148 must retain the one current web before frame14.");
            IReadOnlyList<FixedVector3>? path = tug.CalculatePath(web);
            FixedVector3 impulse = combat.ForwardDirection * 32 + combat.UpDirection * 16;
            player.Model.SetSourceAnimationFrame(14); combat.AnimationFrame = 14; tug.UpdateThrow();
            Assert(actions.State.CurrentStrand is null && !combat.WebPresent && tug.State.SnapReleased == 1 && combat.ActionProgress == 1,
                "Actual native throw must release at frame14 and alias fieldE20 through the shared combat state.");
            if (path is not null)
            {
                Assert(web.Mode == 2 && thug.State.State == 16 && thug.State.Substate == 0,
                    "A native path accepted by the genuine height gate must enter actual304 state16 and retain the loose source chain.");
                for (int index = 0; index < 8; index++) Assert(thug.GetThrowPoint(index) == path[index],
                    "Actual thug WebThrow must own every original computed path point.");
            }
            else if (web.Phase != ActorLifePhase.Dead)
                Assert(web.Mode == 2 && thug.State.State == 14 && thug.State.Substate == 0 && thug.Actor.Body.Velocity == (impulse with { Y = 0 }),
                    "A source height-gate miss must call the genuine world WallHitCheck/GetLaunched route before entering state14.");
            else Assert(strands.Fragments.Count > 0,
                "A genuine blocked native launch must use source BreakIntoBits rather than fabricate an accepted target or chain.");
            actions.AdvanceOwnersBeforeInput(); particles.Move(fixture.Clock); particles.RemoveDead();
            // Use the actual emitted endpoints to verify fragment rendering and independent protected-child lifetime in every branch.
            KnottedWebFragment fragment = strands.EmitFragment(new(first, last, impulse, impulse * -1), random,
                position => GodotBouncingRocks.GetGroundY(fixture.WorldQuery, position), () => fixture.Camera.SourcePosition >> 12);
            FixedVector3 initialStart = fragment.Start, initialEnd = fragment.End;
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(fragment.Start == initialStart && fragment.End == initialEnd && fragment.Age == 0,
                "Render-only native frames must not advance broken web endpoint physics.");
            for (int frameIndex = 0; frameIndex < 31 && fragment.Alive; frameIndex++)
            {
                fixture.Clock += 2; actions.AdvanceOwnersBeforeInput(); particles.Move(fixture.Clock);
                if (!fragment.Alive)
                {
                    Assert(!fragment.Strand.IsDisposed && particles.Identities(SourceParticleList.Flat).Contains(fragment.Strand.Knots[0]) &&
                        particles.Identities(SourceParticleList.GPolyLine).Contains(fragment.Strand.InnerPolyline),
                        "Dead parent must preserve actual live protected children until its GPolyLine destructor phase.");
                }
                particles.RemoveDead();
            }
            Assert(!fragment.Alive && fragment.Strand.IsDisposed &&
                !particles.Identities(SourceParticleList.GPolyLine).Contains(fragment.Strand) &&
                !particles.Identities(SourceParticleList.GPolyLine).Contains(fragment.Strand.InnerPolyline) &&
                fragment.Strand.Knots.All(knot => !particles.Identities(SourceParticleList.Flat).Contains(knot)),
                "Native age31 fragment cleanup must remove exactly its actual owner, protected inner line and all flat knots without double destruction.");
            GD.Print($"Verified genuine304 native source20000 throw: one current web, posed yank/message6, height-gate path={(path is not null)}, accepted/rejected virtual launch, chain and full fragment shared-phase retirement.");
        }
        finally
        {
            web?.Dispose(); group.Free(); particles.Clear();
            thug.State.Health = health; thug.Actor.BehaviorFlags = behavior; thug.State.ActionFlags = actionFlags;
            thug.State.State = state; thug.State.Substate = substate; thug.State.ActionCounter = counter; thug.State.PathPointIndex = pathCount;
            thug.Actor.Body.Velocity = velocity;
            thug.Actor.Body.Acceleration = acceleration; thug.Actor.Body.LocalVelocity = localVelocity;
            thug.Actor.Body.AngularVelocity = angularVelocity; thug.Actor.Body.AngularAcceleration = angularAcceleration;
            thug.RestoreThrowPoints(oldThrowPoints);
            for (int index = 0; index < oldPath.Length; index++) thug.Path.SetPoint(index, oldPath[index]);
            thug.Path.PathCount = oldPathCount; thug.Path.CurrentWaypoint = oldWaypoint; thug.Path.StoredBodyY = oldGroundY;
            thug.Procedures.CleanUp(all: true);
            foreach (var saved in procedures.Reverse()) thug.Procedures.Attach(saved.Procedure);
            foreach (var saved in procedures) saved.Procedure.Flags = saved.Flags;
            combat.ActionState = action; combat.ActionProgress = release; combat.FieldEA6 = suppression;
            melee.State.AimTarget = aim; melee.State.WebAttackMode = shot;
            player.Model.RunClip(playerClip); player.Model.SetSourceAnimationFrame(playerFrame); player.Model.SetAnimationFinished(finished);
            combat.AnimationId = animation; combat.AnimationFrame = animationFrame;
            player.Inventory.State.Webbing = inventoryUnits; player.Inventory.State.FireWebbing = fireVariant;
            melee.Sense.Quips.EncounterActive = encounter; melee.Sense.Quips.StartingHealth = startingHealth; melee.Sense.Quips.EncounterClock = encounterClock;
            fixture.Clock = sourceClock; fixture.Camera.GlobalTransform = camera;
        }
    }
    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native player web tug: {evidence}"); }
    private static InvalidOperationException Outside(string operation) => new($"Strand-only native tug proof unexpectedly activated {operation}.");
    private sealed class ExcludedProjectileHost : IImpactWebHost
    {
        public bool LowMemory => throw Outside("projectile allocation");
        public int ScaleDamage(int damage) => throw Outside("projectile damage");
        public ImpactWebEnvironmentContact? TraceInitialEnvironment(FixedVector3 start, FixedVector3 end) => throw Outside("projectile initial ray");
        public object? CheckObjectCollision(FixedVector3 start, FixedVector3 end) => throw Outside("projectile collision");
        public void SetPlayerFirstContact() => throw Outside("projectile first contact");
        public void HitBody(object identity, ActorHit hit) => throw Outside("projectile hit");
        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => throw Outside("projectile sound");
        public void CreateSplat(FixedVector3 position, ActorAngles normal) => throw Outside("projectile splat");
        public void FlickEligibleSwitch(object identity) => throw Outside("projectile switch");
        public void CreateImpactRing(ImpactWebRingRequest request) => throw Outside("projectile ring");
        public int TraceGroundY(FixedVector3 start, FixedVector3 end) => throw Outside("projectile ground");
        public void CreateWebFragment(ImpactWebFragmentRequest request, Func<int, int> random) => throw Outside("projectile fragment");
    }
}
