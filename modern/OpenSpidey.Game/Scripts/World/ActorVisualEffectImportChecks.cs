using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Untouched L1A4 fire189 consumes42A7 and nine42A6sub3 calls into genuine source Glow/Bullet owners and original expgrnd geometry.</summary>
internal static class ActorVisualEffectImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive)
    {
        var viewport = new SubViewport { Name = "OriginalActorVisualEffects", Size = new(960, 540), OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        GodotBigExplosions? explosions = null; GodotGlowFlash? glows = null;
        try
        {
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/l1a4_t.trg"));
            TriggerNode node = triggers.Nodes[189]; TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
            var scene = new Node3D { Position = new(37, 81, -22) }; viewport.AddChild(scene);
            var random = new GameRandom(0x405939); var bounds = new List<int>();
            int Next(int bound) { bounds.Add(bound); return random.Next(bound); }
            var registry = new GodotActorRegistry { Triggers = triggers, RandomSource = Next }; scene.AddChild(registry); registry.SetPhysicsProcess(false);
            var particles = new SourceParticleScheduler();
            glows = new GodotGlowFlash(); scene.AddChild(glows);
            GodotGlowFlash glowOwner = glows;
            glows.SourceBitCreated += bit => particles.Attach(SourceParticleList.Glow, bit);
            glows.SourceBitRemoved += identity => particles.Detach(identity);
            explosions = GodotBigExplosions.Create(archive.Read("data/expgrnd.psx")); scene.AddChild(explosions);
            GodotBigExplosions modelOwner = explosions;
            explosions.ModelAttached += model => registry.SourceList(ActorUpdateGroup.Bullet).Attach(model, () => modelOwner.AdvanceCapturedModel(model));
            explosions.ModelDetached += model => registry.SourceList(ActorUpdateGroup.Bullet).Detach(model);
            var effects = new GodotActorVisualEffects(() => glowOwner, modelOwner, Next);
            registry.ActorVisualEffects = effects;
            FixedVector3 origin = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12;
            var camera = new Camera3D { Current = true, Fov = (float)OpenSpidey.Core.Cameras.CameraProjection.VerticalFieldOfView(
                OpenSpidey.Core.Cameras.CameraProjection.InitialZoom), KeepAspect = Camera3D.KeepAspectEnum.Height };
            viewport.AddChild(camera); Vector3 focus = PsxCoordinates.FixedWorldPosition(origin);
            camera.GlobalPosition = focus + new Vector3(0, 0, 10); camera.LookAt(focus); camera.MakeCurrent();
            registry.Spawn(node);
            GodotPlatformActor platform = registry.SourceActors(ActorUpdateGroup.Environment).OfType<GodotPlatformActor>().Single();
            Assert(registry.Player is null && platform.SourceUpdates == 0 && platform.Definition.NodeId == 189 &&
                platform.SourcePosition == origin && glows.Count == 0 && explosions.Count == 0 && registry.BaddyConstruction.State.Count == 1,
                "Untouched real402/189 constructor retains its source position/count without running effect bytecode or inventing a player/camera actor.");
            registry.SourceList(ActorUpdateGroup.Bullet).Update();
            registry.SourceList(ActorUpdateGroup.Environment).Update();
            GodotExplosionModel[] models = explosions.SourceModelOrder.ToArray();
            GlowFlashState laser = glows.SourceGlowOrder.OfType<GlowFlashState>().Single();
            int[] ranges = Enumerable.Range(0, 16).Select(index => index % 2 == 0 ? 130 : 260).Concat(new[] { 4096 })
                .Concat(new[] { 11,11,11,2,11,11,11,10,11,11,11,10,81,81,81,1,101,101,101,5,111,111,111,5,121,121,121,5,131,131,131,5,101,101,101,2 }).ToArray();
            Assert(platform.SourceUpdates == 1 && !platform.ScriptState.Active && bounds.SequenceEqual(ranges) &&
                explosions.Count == 9 && models.Length == 9 && registry.SourceList(ActorUpdateGroup.Bullet).SourceOrder.SequenceEqual(models) &&
                laser.Position == origin && laser.Sections.Count == 16 && laser.CentreCodeBgr == 0x32FFFFFF && laser.Mask == uint.MaxValue &&
                laser.Lifetime == 2 && laser.Age == 0 && laser.FringeCount == 1 && laser.FringeCount * laser.Sections.Count == laser.Fringes.Count &&
                laser.Sections.All(section => section.PadBgr == 0x7BFF) && particles.Identities(SourceParticleList.Glow).Single() == laser,
                "Actual189 first AI executes its unchanged laser words plus all nine raw wibbling tuples: genuine shared GlowList/BulletList head order and53 exact constructor RNG calls occur after the earlier captured Bullet phase.");
            int[] scales = [3000,2500,2250,2000,2500,3500,2048,2048,2048];
            int[] steps = [200,150,100,100,100,100,1500,1750,1750];
            Assert(models.Select(model => model.State.Scale.X).SequenceEqual(scales) &&
                models.Select(model => model.State.ScaleStep).SequenceEqual(steps) && models.All(model => model.State.ModelName == "expgrnd" &&
                    model.State.ModelIndex == 1 && model.State.GrowthAge == 0 && model.State.ItemFlags == 0x600 && model.Visible &&
                    model.RenderMesh.GetSurfaceCount() > 0 && model.Materials.Count > 0 &&
                    model.GlobalTransform.Origin.IsEqualApprox(PsxCoordinates.FixedWorldPosition(model.State.Position))),
                "All nine original expgrnd1 models retain exact authored scales/steps, imported native geometry and world-space jitter under a shifted scene owner; model physics has not run in its earlier source phase.");
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            glows.Rebuild();
            Assert(laser.Age == 0 && glows.RenderMesh.GetSurfaceCount() > 0 && bounds.SequenceEqual(ranges) && models.All(model => model.State.GrowthAge == 0),
                "Native widescreen laser geometry renders all source sections while rendering alone consumes no source RNG/age/model phase.");
            particles.Move(2); particles.RemoveDead();
            Assert(laser.Age == 1 && glows.Count == 1, "New laser receives one actual source Glow move in the creation frame, with no extra render-driven aging.");
            registry.SourceList(ActorUpdateGroup.Bullet).Update(); particles.Move(4); particles.RemoveDead();
            Assert(laser.Age == 2 && glows.Count == 1 && models.All(model => model.State.GrowthAge == 1),
                "The next captured Bullet phase advances each existing model once; laser remains alive at its exact lifetime2.");
            particles.Move(6); particles.RemoveDead();
            Assert(laser.IsDead && glows.Count == 0 && particles.Count == 0,
                "Original laser dies strictly after its lifetime and real shared bit cleanup detaches only that Glow identity.");
            Node originalParent = models[0].GetParent(); originalParent.RemoveChild(models[0]); viewport.AddChild(models[0]);
            Assert(explosions.Count == 9 && !models[0].ResourcesDisposed && registry.SourceList(ActorUpdateGroup.Bullet).SourceOrder.Contains(models[0]),
                "Native explosion reparenting preserves its actual derived count, geometry and Bullet identity.");
            for (int phase = 0; phase < 24; phase++) registry.SourceList(ActorUpdateGroup.Bullet).Update();
            Assert(explosions.Count == 0 && explosions.ModelCount == 0 && registry.SourceList(ActorUpdateGroup.Bullet).Count == 0 &&
                models.All(model => !GodotObject.IsInstanceValid(model) && model.ResourcesDisposed),
                "Real source growth/fade/dead/pending/delete phases retire all nine original native models and derived counts, including the reparented model.");
            registry.DestroyImmediately(platform);
            Assert(registry.LiveCount == 0 && registry.BaddyConstruction.State.Count == 0,
                "Actual source402 deletion releases only its own CBaddy lease after its independently owned visual effects have retired.");
            // The alternate source laser opcode is the same six-word leaf; half-mask construction remains a real glow.
            byte[] halfWords = new byte[] { 0,2,1,0,255,0,123,0,0,0,2,0 };
            ActorVisualEffectCommands.TryExecute(0x42A8, new(halfWords), origin, effects);
            GlowFlashState half = glows.SourceGlowOrder.OfType<GlowFlashState>().Single();
            Assert(half.Mask == 126 && half.Angle >= unchecked((ushort)-1224) && half.Angle <= unchecked((ushort)-824) && glows.Count == 1,
                "Actual42A8 half-laser path retains the original401-range negative angle wrap and126 section mask.");
            glows.Free(); glows = null;
            Assert(glowOwner.ResourcesDisposed && particles.Count == 0, "Actual native Glow owner Predelete releases its remaining bit identities exactly once.");
            GD.Print("Verified native actor effects: untouched L1A4 fire189, real42A7/42A8 laser, nine42A6sub3 expgrnd models,53 exact RNG calls, shared source phases, wide glow geometry and reparent/destruction/count cleanup.");
        }
        finally { viewport.Free(); }
    }
    private static void Assert(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException("Native actor visual effects: " + evidence); }
}
