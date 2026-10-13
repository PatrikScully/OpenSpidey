using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Audio;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.World;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Genuine L1A4 node14 bomb and source196/201 player interactions over the real world, timer, links, audio and camera.</summary>
internal static class LevelOneBombImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository,
        OriginalPlayerData original, Func<GodotPlayer> playerFactory)
    {
        var viewport = new SubViewport { OwnWorld3D = true, Size = new(640, 480), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            var root = new Node3D { Name = "ActualL1A4BombProof" }; viewport.AddChild(root);
            byte[] triggerData = archive.Read("data/L1A4_T.trg"); TriggerFile triggers = TriggerFile.Read(triggerData);
            ImportedLevelWorld world = ImportedLevelWorld.Load(repository, "data/L1A4_G.psx"); root.AddChild(world);
            PsxAssetLibrary geometry = repository.Load("data/L1A4_G.psx"), objects = repository.Load("data/L1A4_O.psx");
            var textures = repository.Load("data/L1A4_L.psx").Textures.Concat(geometry.Textures).Concat(objects.Textures).ToArray();
            GodotPlayer player = playerFactory(); root.AddChild(player); player.SetPhysicsProcess(false); player.Model.AdvanceAutomatically = false;
            uint clock = 1000;
            var camera = new PlayerFollowCamera { Target = player, Current = true, SourceTotalTime = () => clock };
            root.AddChild(camera); camera.SetPhysicsProcess(false); camera.MakeCurrent();
            var sounds = new GodotSoundChannels { Archive = archive, LevelGroup = SoundCatalog.Find("L1A4") }; root.AddChild(sounds);
            sounds.Controller.PlayerBank = SoundDefinitionBank.Read(archive.Read("data/spidey.sfx"));
            sounds.Controller.LevelBank = SoundDefinitionBank.Read(archive.Read("data/l1a4.sfx"), levelSpecific: true);
            SoundListener Listener() => new(PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12);
            var pulses = new List<ushort>();
            var registry = new GodotActorRegistry { Triggers = triggers, Player = player, Difficulty = 2, SuspensionDistance = uint.MaxValue,
                ObjectLibrary = () => objects, ObjectTextures = () => textures, ClaimObjectModel = world.ClaimObjectModel,
                Sounds = sounds.Controller, SoundListener = Listener, ReceivePulse = pulses.Add,
                RandomSource = new GameRandom(0x4470A0).Next, Environment = new(LevelId: 0x104) };
            root.AddChild(registry); registry.SetPhysicsProcess(false);
            var combat = GodotPlayerCombatRuntime.Create(archive, repository, original, player, registry, () => world,
                camera, owner.GetNode<GodotSpeechPlayer>("Speech"), () => clock);
            root.AddChild(combat); combat.SetPhysicsProcess(false);
            player.Combat.SourceTimer = () => clock; player.Combat.CameraBehind = camera.PutBehind;
            var countdown = new MissionCountdown(); var landingSounds = new List<(FixedVector3 Position, uint Handle)>();
            GodotLevelObjectRuntime objectsRuntime = null!;
            var visibility = new GodotEnvironmentVisibility(() => world, repository, (source, index) =>
                objectsRuntime?.Environment.Items.FirstOrDefault(item => item.SourceAsset.Equals(source.AssetPath,
                    StringComparison.OrdinalIgnoreCase) && item.SourceInstanceIndex == index));
            objectsRuntime = GodotLevelObjectRuntime.Create(archive, repository, world, registry, combat, sounds,
                camera, countdown, triggerData, () => clock, visibility);
            root.AddChild(objectsRuntime);
            objectsRuntime.PositionSoundPlayed += (sound, position, handle) =>
            {
                Assert(sound == 0x819C, "Bomb collisions must retain the actual source positional819C sound.");
                landingSounds.Add((position, handle));
            };
            GodotBigExplosions explosions = objectsRuntime.Explosions;
            GodotManipulationEffects effects = objectsRuntime.Effects;
            registry.Spawn(triggers.Nodes[65]); registry.Spawn(triggers.Nodes[70]);
            GodotScriptActor[] controls = registry.SourceActors(ActorUpdateGroup.Control).ToArray();
            Assert(controls.Length == 2 && controls.All(actor => actor.SourceActorType == 203) &&
                controls.Single(actor => actor.SourceNodeId == 65).ScriptState.Active &&
                !controls.Single(actor => actor.SourceNodeId == 70).ScriptState.Active,
                "Original bomb control65 must wait at4281; control70's authored4100 must already stop its script without an invented wait.");
            int baddyCount = registry.BaddyConstructionState.Count;
            GodotManipulationActor? captured = null;
            void Capture(GodotScriptActor actor) { if (actor.SourceNodeId == 14) captured = actor as GodotManipulationActor; }
            registry.Spawned += Capture;
            try { registry.Spawn(triggers.Nodes[14]); }
            finally { registry.Spawned -= Capture; }
            GodotManipulationActor bomb = captured ?? throw new InvalidDataException("Original bomb factory600 did not construct a native object.");
            GodotLevelOneBomb specialized = bomb.LevelOneBomb ?? throw new InvalidDataException("Native bomb has no actual specialized source overrides.");
            Assert(bomb.Definition.ActorType == 600 && bomb.SourceActorType == 401 && bomb.State.ModelChecksum == 0xDFC229D6 &&
                bomb.Body.Position == new FixedVector3(-2024, -83, -871) << 12 && bomb.Body.Angles == default &&
                bomb.State.Flags == 8 && bomb.State.Parameter108 == 80 && bomb.State.ImpactSound == 1000 &&
                bomb.State.ShadowItem is null && bomb.State.ChunkModels.Count == 0 && bomb.Definition.Links.SequenceEqual(new ushort[] { 65, 70 }),
                "Genuine L1A4 node14 must retain definition600/runtime401, original OBJ, large8/reach80 and the actual adjacent impact-sound word1000.");
            Assert(registry.BaddyConstructionState.Count == baddyCount && registry.BaddyConstruction.OwnedCount == 2 &&
                registry.SourceActors(ActorUpdateGroup.Environment).SequenceEqual(new[] { bomb }) && combat.Session.SourceEnvironmentalOrder.Single().Identity == bomb &&
                combat.Directory.SourceBodies.Count == 0 && bomb.GetChildren().OfType<MeshInstance3D>().Single().Mesh!.GetSurfaceCount() > 0,
                "Bomb construction must own one real environment OBJ/collision and inherit401 identity without acquiring a NumBaddies lease.");
            Assert(countdown.Rate == 4096 && countdown.Running == 0 && countdown.DisplayEnabled == 0 &&
                specialized.State.Armed == 0 && specialized.State.ExpirationSignalSent == 0 && specialized.State.GroundContact == 0,
                "Source bomb constructor must initialize its own arm/signal fields and shared rate4096 without starting a timer.");
            await Physics(); await Physics();
            FixedVector3 approach = FindApproach(bomb, combat);
            PlacePlayer(approach, bomb.Body.Position);
            GodotPlayerMelee melee = player.Melee!;
            melee.PrepareTargets(2); TriggerKick();
            Assert(melee.CheckKick() && player.Combat.State.ActionState == 0x100000 && player.Model.ClipId == 196,
                "Real original bomb OBJ contact and large flag must select genuine pickup196.");
            Frame(9); melee.UpdateInteraction(ushort.MaxValue);
            Assert(combat.Session.HeldActor is null && bomb.State.PulseSent == 0 && pulses.Count == 0,
                "Native bomb frame9 must retain the actual source pickup threshold.");
            Frame(10); melee.UpdateInteraction(ushort.MaxValue);
            Assert(ReferenceEquals(combat.Session.HeldActor, bomb) && ReferenceEquals(melee.Kick.HeldObject?.Identity, bomb) &&
                (bomb.State.ItemFlags & 0x820) == 0x820 && bomb.State.PulseSent == 1 && pulses.SequenceEqual(new ushort[] { 65, 70 }) &&
                bomb.Colliders.SelectMany(body => body.GetChildren().OfType<CollisionShape3D>()).All(shape => shape.Disabled),
                "Actual pickup196/frame10 must hold the same native body, pulse65/70 once and exclude its real collision without crashing.");
            melee.FinishFrame(); player.Model.VerifyCurrentPose();
            FixedVector3 first = melee.GetPlayerHookPosition(1), second = melee.GetPlayerHookPosition(0), middle = first + (second - first) / 2;
            FixedVector3 expectedHeld = middle + FixedVectorMath.Normalize((middle - player.Combat.State.Position) >> 6) * 80 + player.Combat.State.ForwardDirection * 32;
            Assert(bomb.Body.Position == expectedHeld && bomb.Transform.Origin.IsEqualApprox(PsxCoordinates.FixedWorldPosition(expectedHeld)),
                "Native bomb carry must use actual hook1/0 from the original196 pose, reach80 and the source forward32 offset.");
            registry.SignalActiveActors(14); Step();
            Assert(specialized.State.Armed == 1 && bomb.ScriptState.PendingSignals == 0 && countdown.RemainingTicks == 7260 &&
                countdown.Running == 1 && countdown.DisplayEnabled == 1 && countdown.LastUpdateTick == clock &&
                specialized.LastUpdateTick == clock && specialized.ElapsedTicks == 2 && specialized.AccumulatedTicks == 2,
                "Real source signal1 must arm the difficulty2 timer7260 and preserve the genuine first EveryFrame seed2 while carried.");
            registry.SourceList(ActorUpdateGroup.Environment).Update();
            Assert(specialized.ElapsedTicks == 0 && specialized.AccumulatedTicks == 2 && countdown.RemainingTicks == 7260,
                "Repeating the exact same source clock must retain delta0 and leave shared timer decay to its separate panel phase.");
            uint beforeRender = countdown.RemainingTicks; int actorUpdates = bomb.SourceUpdates;
            await FrameRender(); await FrameRender();
            Assert(countdown.RemainingTicks == beforeRender && bomb.SourceUpdates == actorUpdates, "Rendering must not advance bomb physics or countdown.");
            player.Combat.State.FieldE18 = 1; Step();
            Assert(countdown.RemainingTicks == beforeRender && countdown.LastUpdateTick == clock,
                "Actual player E18 control mask must pause bomb decay while advancing its source panel clock.");
            player.Combat.State.FieldE18 = 0; Step();
            Assert(countdown.RemainingTicks == beforeRender - 2, "Returning to real player control must resume rate4096 timer decay.");
            player.Model.AdvanceTicks(player.Model.FrameCount * 8); melee.State.AnimationFinished = player.Model.Playback.IsFinished;
            melee.UpdateInteraction(196); melee.PrepareTargets(2); TriggerKick();
            Assert(melee.CheckKick() && player.Combat.State.ActionState == 0x200000 && player.Model.ClipId == 201,
                "Kick while holding the actual large bomb must select genuine hard throw201.");
            Frame(17); melee.UpdateInteraction(ushort.MaxValue);
            Assert(ReferenceEquals(combat.Session.HeldActor, bomb), "Hard bomb throw201 must retain ownership before source frame18.");
            melee.Kick.TorsoTarget = null; Frame(18); melee.UpdateInteraction(ushort.MaxValue);
            Assert(combat.Session.HeldActor is null && melee.Kick.HeldObject is null && !player.Combat.State.HeldObjectPresent &&
                bomb.Body.Velocity == player.Combat.State.ForwardDirection * -24 + player.Combat.State.UpDirection * 14 &&
                bomb.Body.Acceleration.Y == 4096 && (bomb.State.Flags & 9) == 9 && (bomb.State.ItemFlags & 0x800) == 0,
                "Original201/frame18 must release the same bomb with inherited Throw velocity/acceleration and preserve its actual specialized AI.");
            int groundQueries = combat.World.GroundQueries;
            for (int update = 0; update < 100 && specialized.State.GroundContact == 0; update++)
            { Step(); await Physics(); }
            Assert(specialized.State.GroundContact == 1 && countdown.Rate == 8192 && combat.World.GroundQueries > groundQueries &&
                landingSounds.Count > 0 && landingSounds.Any(sound => sound.Handle is not 0 and not uint.MaxValue) &&
                camera.ShakeState.Amplitude == new OpenSpidey.Core.Cameras.CameraShakeAxes(0, 0, 25) &&
                explosions.ModelCount == 0 && effects.Smoke.Count == 0 && combat.Directory.SourceBodies.Count == 0,
                "Actual L1A4 ground must land the thrown bomb through source100/72, play original819C and camera shake, clamp timer rate8192 and avoid generic Chunk/destruction.");
            Assert(Math.Abs(bomb.Transform.Origin.Y - PsxCoordinates.FixedWorldPosition(bomb.Body.Position).Y) < .00001f,
                "Rendered original bomb OBJ must follow its genuine grounded fixed position.");
            clock += 10000; countdown.Update(clock, false);
            Assert(countdown.RemainingTicks == 0, "Shared genuine rate8192 countdown must clamp the large clock jump to expiry.");
            registry.SourceList(ActorUpdateGroup.Environment).Update();
            Assert(specialized.ElapsedTicks == 6 && specialized.State.ExpirationSignalSent == 1 && controls.All(actor => (actor.ScriptState.PendingSignals & 1) != 0),
                "Actual bomb expiry must send source signal1 to both original controls once, with resumed elapsed clamp6.");
            registry.SourceList(ActorUpdateGroup.Control).Update();
            Assert(controls.All(actor => actor.ScriptState.PendingSignals == 0 && !actor.ScriptState.Active) &&
                controls.Single(actor => actor.SourceNodeId == 70).Lifecycle.Phase == ActorLifePhase.Dead &&
                controls.Single(actor => actor.SourceNodeId == 65).Lifecycle.Phase == ActorLifePhase.Active &&
                pulses.Skip(2).SequenceEqual(new ushort[] { 355 }),
                "Actual first Control pass must retire already-stopped70 and parse65 from4281 to4100, preserving its next-update retirement.");
            registry.SourceList(ActorUpdateGroup.Control).Update();
            Assert(controls.All(actor => actor.ScriptState.Retired) && pulses.Skip(2).SequenceEqual(new ushort[] { 355, 66, 357 }),
                "Next original Control visit must retire65 and emit its genuine downstream66/357 pulses after70's355.");
            registry.SourceList(ActorUpdateGroup.Environment).Update();
            Assert(controls.All(actor => actor.ScriptState.PendingSignals == 0), "Expired bomb AI must never repeat its one-shot linked signal.");
            pulses.Clear(); bomb.Die();
            Assert(pulses.SequenceEqual(new ushort[] { 65, 70 }) && countdown.Running == 0 && countdown.DisplayEnabled == 0 &&
                countdown.LastUpdateTick == clock && (bomb.State.BodyFlags & 0x40) == 0 && registry.Owns(bomb),
                "Original bomb Die virtual must pulse65/70 and stop the timer without inventing CBody dead40 or deleting the object.");
            PlayerKickHandle handle = combat.Session.MakeEnvironmentalHandle(bomb.KickObject); uint validation = handle.Validation;
            bomb.State.BodyFlags |= 0x40; bomb.Lifecycle.Kill();
            for (int update = 0; update < 4; update++) registry._PhysicsProcess(1.0 / 60);
            await FrameRender(); await FrameRender();
            Assert(!registry.Owns(bomb) && registry.SourceActors(ActorUpdateGroup.Environment).Count == 0 && combat.Session.RecoverEnvironmentalHandle(ref handle) is null &&
                handle.Target is null && handle.Validation == validation && countdown.Running == 0 && countdown.LastUpdateTick == clock &&
                combat.Particles.Count == 0 && registry.BaddyConstructionState.Count == 0 && pulses.SequenceEqual(new ushort[] { 65, 70 }),
                "Actual pending-delete/destructor must retire the one original OBJ and weak pointer, preserve validation and leave no source particles or duplicate bomb pulse.");
            GD.Print("Verified real L1A4 bomb14: exact adjacent TRG word1000, definition600/runtime401/noBaddy lease, posed pickup196/carry/throw201, original floor819C/shake, shared paused countdown/rate8192, one-shot links65/70, Die and native cleanup.");

            void Step()
            {
                clock += 2; registry.SourceList(ActorUpdateGroup.Environment).Update();
                countdown.Update(clock, player.Combat.State.FieldE18 != 0 || player.Combat.State.SynthesizedInputActive);
                combat._PhysicsProcess(1.0 / 60); combat._PhysicsProcess(1.0 / 60);
            }
            void TriggerKick() { melee.State.Input.Triggered[18] = melee.State.Input.Mirrors[18] = 1; }
            void Frame(int frame)
            {
                player.Model.SetSourceAnimationFrame(frame); player.Combat.State.AnimationId = (ushort)player.Model.ClipId;
                player.Combat.State.AnimationFrame = frame; melee.State.AnimationFinished = player.Model.Playback.IsFinished;
            }
            void PlacePlayer(FixedVector3 position, FixedVector3 target)
            {
                player.Controller.TeleportFromScript(position);
                player.Controller.Orientation.Orient(new(0, -4096, 0), 0, FixedVectorMath.Normalize(((position - target) >> 12) with { Y = 0 }));
                player.Controller.CaptureCombatState(2); player.Combat.State.ActionState = 1; player.RefreshCombatTransform();
                camera.GlobalPosition = player.GlobalPosition + new Vector3(0, 3, 8); camera.LookAt(PsxCoordinates.FixedWorldPosition(target));
            }
        }
        finally { viewport.Free(); }
        async Task Physics() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        async Task FrameRender() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static FixedVector3 FindApproach(GodotManipulationActor actor, GodotPlayerCombatRuntime combat)
    {
        foreach (ImportedCollisionBody body in actor.Colliders)
            foreach (CollisionTriangle face in body.SourceTriangles)
            {
                Vector3 center = PsxCoordinates.WorldPosition((face.A + face.B + face.C) / 3);
                FixedVector3 normal = PsxCoordinates.RotateOriginalNormal(face.FixedNormal, body.SourceRotation);
                if (Math.Abs(normal.Y) > 1024) continue;
                FixedVector3 point = PsxCoordinates.OriginalWorldPosition(body.ToGlobal(center));
                foreach (int distance in new[] { 60, 100, 160, 200 })
                {
                    FixedVector3 position = point + normal * distance;
                    GodotActorWorldHit? hit = combat.World.Trace(position, actor.Body.Position);
                    if (hit?.SupportId != actor.GetInstanceId()) continue;
                    FixedVector3 delta = (hit.Position - position) >> 12;
                    int range = FixedVectorMath.OriginalSquareRoot(unchecked(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z));
                    if (range <= 256 && ActorLifecycle.Distance(position, actor.Body.Position) < 768u) return position;
                }
            }
        throw new InvalidDataException("Genuine bomb OBJ has no original facing/ray approach within source pickup range.");
    }
    private static ManipulationWorldHit? Trace(GodotActorWorldQuery world, GodotPlayerMeleeSession session,
        FixedVector3 start, FixedVector3 end, int mode)
    {
        if (mode != 1) throw new InvalidDataException("Original bomb/manipulation probe requires source mode1.");
        GodotActorWorldHit? hit = world.Trace(start, end);
        return hit is null ? null : new(session.ResolveWorldContact(hit), hit.Position, PsxCoordinates.RotateOriginalNormal(hit.Triangle.FixedNormal,
            hit.Collider is ImportedCollisionBody body ? body.SourceRotation : PsxCoordinates.OriginalRotation(hit.Collider.GlobalBasis)));
    }
    private static void Assert(bool condition, string evidence) { if (!condition) throw new InvalidDataException(evidence); }
}
