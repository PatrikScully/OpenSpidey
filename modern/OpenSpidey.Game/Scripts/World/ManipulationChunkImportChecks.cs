using System.Numerics;
using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Audio;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Actual L1A3 node193 default Chunk path; no explosive flags or artificial collision floor are introduced.</summary>
public static class ManipulationChunkImportChecks
{
    private sealed class Queries(GodotActorWorldQuery world, List<string> order) : IManipulationDebrisHost
    {
        public int Count { get; private set; }
        public ManipulationWorldHit? LineToItem(FixedVector3 start, FixedVector3 end, int mode)
        {
            if (mode != 1) throw new InvalidDataException("Chunk world probe changed original mode1.");
            Count++; order.Add("probe");
            GodotActorWorldHit? hit = world.Trace(start, end);
            return hit is null ? null : new(hit.Collider, hit.Position, PsxCoordinates.RotateOriginalNormal(hit.Triangle.FixedNormal,
                hit.Collider is ImportedCollisionBody body ? body.SourceRotation : PsxCoordinates.OriginalRotation(hit.Collider.GlobalBasis)));
        }
    }
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository)
    {
        var viewport = new SubViewport { Size = new(640, 480), OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            var root = new Node3D(); viewport.AddChild(root);
            ImportedLevelWorld world = ImportedLevelWorld.Load(repository, "data/L1A3_G.psx"); root.AddChild(world);
            PsxAssetLibrary geometry = repository.Load("data/L1A3_G.psx"), objects = repository.Load("data/L1A3_O.psx");
            var textures = repository.Load("data/L1A3_L.psx").Textures.Concat(geometry.Textures).Concat(objects.Textures).ToArray();
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A3_T.trg"));
            TriggerActorDefinition definition = TriggerActorDefinition.Read(triggers.Nodes[193]);
            if (definition.ActorType != 401 || definition.Position != new TriggerPosition(598, -68, 704) || definition.Orientation != new TriggerOrientation(0, 4040, 0) ||
                !definition.Links.SequenceEqual(new ushort[] { 466 }))
                throw new InvalidDataException("Chunk composition lost independent original L1A3 node193 placement, angles or attached link466.");
            var environment = GodotSwitchEnvironment.Create(world, geometry, textures, archive.Read("data/L1A3_G.psx")); root.AddChild(environment);
            var camera = new Camera3D { Current = true, KeepAspect = Camera3D.KeepAspectEnum.Height,
                Fov = (float)OpenSpidey.Core.Cameras.CameraProjection.VerticalFieldOfView(OpenSpidey.Core.Cameras.CameraProjection.InitialZoom) };
            root.AddChild(camera);
            FixedVector3 position = new FixedVector3(598, -68, 704) << 12;
            camera.GlobalPosition = PsxCoordinates.FixedWorldPosition(position) + new Godot.Vector3(0, 0, 10);
            camera.LookAt(PsxCoordinates.FixedWorldPosition(position)); camera.MakeCurrent();
            var sounds = new GodotSoundChannels { Archive = archive, LevelGroup = SoundCatalog.Find("L1A3") }; root.AddChild(sounds);
            sounds.Controller.PlayerBank = SoundDefinitionBank.Read(archive.Read("data/spidey.sfx"));
            sounds.Controller.LevelBank = SoundDefinitionBank.Read(archive.Read("data/l1a3.sfx"), levelSpecific: true);
            SoundListener Listener() => new(PsxCoordinates.OriginalWorldPosition(camera.GlobalPosition) >> 12);
            var order = new List<string>(); var randomRanges = new List<int>(); var pulses = new List<ushort>();
            var random = new GameRandom(0x456E90); var environmental = new List<GodotManipulationActor>();
            var bodies = new List<GodotManipulationDebris>(); var constructed = new List<GodotManipulationDebris>(); var directory = new GodotPlayerMeleeDirectory();
            GodotActorRegistry? registryForPulse = null;
            var registry = new GodotActorRegistry { Triggers = triggers, SuspensionDistance = uint.MaxValue,
                ObjectLibrary = () => objects, ObjectTextures = () => textures, ClaimObjectModel = world.ClaimObjectModel,
                RandomSource = range => { order.Add("random"); randomRanges.Add(range); return random.Next(range); },
                ReceivePulse = node => { if (node >= triggers.Nodes.Count) throw new InvalidDataException("Original Chunk pulse references a missing node.");
                    order.Add("pulse"); pulses.Add(node); registryForPulse!.SignalActiveActors(node); }, Sounds = sounds.Controller, SoundListener = Listener };
            registryForPulse = registry; root.AddChild(registry); registry.SetPhysicsProcess(false); directory.Bind(registry);
            Exception? actorFailure = null; registry.Faulted += exception => actorFailure = exception;
            var worldQuery = new GodotActorWorldQuery(root, triggers); var queries = new Queries(worldQuery, order);
            IReadOnlyList<ManipulationTargetBody> SourceBodies() => directory.SourceBodies.Select(body =>
                new ManipulationTargetBody(new(body.Identity, body.Position, body.Radius, body.BodyFlags), body.Type)).ToArray();
            var debris = new GodotManipulationDebrisConfiguration { WorldQueries = queries,
                AttachToBaddyList = actor => { order.Add("attach"); bodies.Insert(0, actor); constructed.Add(actor); directory.RegisterBody(actor); },
                DetachFromBaddyList = actor => { order.Add("detach"); bodies.Remove(actor); directory.Remove(actor); } };
            uint soundHandle = 0; FixedVector3 soundedPosition = default; ushort soundedId = 0; int soundedPitch = -1;
            var effects = GodotManipulationEffects.Create(archive.Read("data/bits.psx"), new()
            {
                Registry = registry, Debris = debris, SourceBaddyOrder = SourceBodies,
                PlayerPosition = () => throw Outside("player position for explosive damage"),
                CameraPositionWholeUnits = () => Listener().WholePosition,
                GroundHeight = (p, above, below, flags) => { order.Add("ground"); if (above != 0 || below != 700 || flags != 0)
                        throw new InvalidDataException("Default Chunk changed original ground request0/700/0."); return worldQuery.GroundHeight(p, above, below); },
                PlayPositionSound = (id, p, pitch) => { order.Add("sound"); soundedId = id; soundedPosition = p; soundedPitch = pitch;
                    soundHandle = sounds.Controller.PlayAt(id, p, Listener()); },
                SendExplosionMessage = (target, p) => { order.Add("message"); if (target is GodotThugActor thug) thug.ReceiveMessage(10, null, p);
                    else throw Outside("explosion message recipient without a recovered CBaddy queue"); },
                CreateBigExplosion = _ => throw Outside("explosive visual"), HitPlayer = _ => throw Outside("explosive player damage"),
                HitBaddy = (_, _) => throw Outside("explosive baddy damage"), ReactSymbioteToExplosion = _ => throw Outside("symbiote explosion reaction")
            });
            root.AddChild(effects);
            var configuration = new GodotManipulationConfiguration { Environment = environment,
                AttachToEnvironmentalList = actor => environmental.Insert(0, actor), DetachFromEnvironmentalList = actor => environmental.Remove(actor),
                SourceBaddyOrder = SourceBodies, IgnoreSphereRadius = () => false, LineToItem = queries.LineToItem,
                HitBaddy = (_, _) => throw Outside("parent direct sphere impact"), ShakeCamera = (_, _) => throw Outside("parent collision camera shake"), ImpactEffects = effects };
            GodotManipulationActor parent = GodotManipulationActor.Create(triggers.Nodes[193], registry, configuration); registry.Adopt(parent);
            uint[] authoredModels = [0x7640E7BD, 0xEF49B607, 0x984E8691, 0x062A1332];
            if (parent.State.ModelChecksum != 0x5301B900 || parent.State.ShadowItem?.ModelChecksum != 0x55EFD6D9 ||
                !parent.State.ChunkModels.SequenceEqual(authoredModels) || parent.State.Flags != 0 || parent.State.Parameter108 != 85 || parent.State.ImpactSound != 0 ||
                parent.Body.Position != position || parent.Body.Angles != new ActorAngles(0, 4040, 0))
                throw new InvalidDataException("Actual OBJ constructor differs from independently decoded original model, shadow, four chunk checksums and default flags.");
            await Physics(); await Physics();
            order.Clear(); randomRanges.Clear();
            parent.Smash();
            if (actorFailure is not null) throw new InvalidOperationException("Native Chunk actor update failed.", actorFailure);
            int[] expectedRanges = [..Enumerable.Range(0, 6).SelectMany(_ => new[] { 4, 4096, 4096, 32, 32, 30 }),
                ..Enumerable.Range(0, 24).SelectMany(_ => new[] { 4096, 300, 1000, 4096, 0, 40, 5 })];
            string[] expectedOrder = ["sound", "ground", ..Enumerable.Range(0, 6).SelectMany(_ =>
                new[] { "random", "attach", "random", "random", "random", "random", "random" }),
                ..Enumerable.Repeat("random", 24 * 7), "pulse"];
            if (!randomRanges.SequenceEqual(expectedRanges) || soundedId != 27 || soundedPosition != position || soundedPitch != 0 || soundHandle is 0 or uint.MaxValue ||
                sounds.StartedCount != 1 || sounds.PlayerForChannel(BitOperations.TrailingZeroCount(soundHandle)) is not { Playing: true } ||
                !sounds.Controller.ReadChannel(BitOperations.TrailingZeroCount(soundHandle)).Playing)
                throw new InvalidDataException("Original default Chunk must play genuine positional SFX27 and preserve all204 allocation/constructor random boundaries.");
            if (!order.SequenceEqual(expectedOrder) ||
                !pulses.SequenceEqual(new ushort[] { 466 }) || parent.State.PulseSent != 1 || parent.Lifecycle.Phase != ActorLifePhase.Dead ||
                parent.Body.Velocity != new FixedVector3(0, 0x20000, 0x20000) || ((GodotSwitchEnvironmentItem)parent.State.ShadowItem!.Identity).Mesh.Visible)
                throw new InvalidDataException("Chunk changed source sound/ground/construct/pulse order, exact Smash velocity, one-shot link466 or original shadow model hiding.");
            if (constructed.Count != 6 || bodies.Count != 6 || !bodies.SequenceEqual(constructed.AsEnumerable().Reverse()) ||
                !registry.SourceActors(ActorUpdateGroup.Enemy).SequenceEqual(bodies) || !directory.SourceBodies.Select(body => body.Native).SequenceEqual(bodies) ||
                constructed.Any(actor => actor.Body.Position != position || actor.SourceUpdates != 0 || actor.MinorRadius != 0 || actor.BodyFlags != 0x16 ||
                    actor.SourceActorType != 401 || actor.SourceNodeId != ushort.MaxValue || actor.ItemFlags != 0 || !authoredModels.Contains(actor.State.ModelChecksum) ||
                    objects.Scene.Models[actor.ModelIndex].Checksum != actor.State.ModelChecksum || actor.SourceMesh.Mesh!.GetSurfaceCount() == 0))
                throw new InvalidDataException("Six original OBJ chunks must enter actual BaddyList/registry/directory head order with source zero-radius401 metadata and real meshes.");
            if (effects.Smoke.Count != 24 || effects.Smoke.Particles.Any(particle => particle.Age != 0 || particle.Scale != 0 || particle.CodeRgb != 0x2E202020 || particle.Origin != position) ||
                effects.Smoke.SourceFrame.TextureChecksum != 0xFC058B11)
                throw new InvalidDataException("Original Chunk must create24 unadvanced Misc frame0 puffs around the actual OBJ position.");
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (constructed.Any(actor => actor.SourceUpdates != 0) || effects.Smoke.Particles.Any(particle => particle.Age != 0))
                throw new InvalidDataException("Native rendering must not advance source chunk bodies or smoke.");
            order.Clear();
            registry._PhysicsProcess(1.0 / 60); registry._PhysicsProcess(1.0 / 60);
            if (actorFailure is not null) throw new InvalidOperationException("First native chunk body update failed.", actorFailure);
            if (queries.Count != 6 || constructed.Any(actor => actor.SourceUpdates != 1 || actor.State.RemainingTicks is < 58 or > 87) ||
                effects.Smoke.Particles.Any(particle => particle.Age != 0))
                throw new InvalidDataException("Actual Enemy phase must advance six bodies by2ticks before the separate source flat-bit phase.");
            effects.AdvanceBits();
            if (effects.Smoke.Count != 24 || effects.Smoke.Particles.Any(particle => particle.Age != 1 || particle.Scale <= 0) || effects.Smoke.RenderMesh.GetSurfaceCount() != 24)
                throw new InvalidDataException("Source bit phase must move all24 actual textured smoke quads exactly once after body AI.");
            for (int update = 2; update <= 50; update++)
            {
                await Physics(); await Physics();
                registry._PhysicsProcess(1.0 / 60); registry._PhysicsProcess(1.0 / 60); effects.AdvanceBits();
                if (actorFailure is not null) throw new InvalidOperationException("Native Chunk cleanup update failed.", actorFailure);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            if (registry.LiveCount != 0 || bodies.Count != 0 || environmental.Count != 0 || directory.SourceBodies.Count != 0 ||
                effects.Smoke.Count != 0 || effects.Smoke.RenderMesh.GetSurfaceCount() != 0 || order.Count(entry => entry == "detach") != 6 || pulses.Count != 1)
                throw new InvalidDataException("Source corpse/debris destructors and smoke RGB death must remove all real native owners/geometry without repeating link466.");
            sounds.Controller.Stop(soundHandle);
            GD.Print("Verified genuine L1A3 node193 default Chunk: positional SFX27, originalOBJ/ENV/four chunk packets,204source RNG calls,6BaddyList models,24Misc sprites, link466, body-before-bit phases and native cleanup. Explosive/NPC recipient branches remain outside this bounded native proof.");
        }
        finally { viewport.Free(); }
        async Task Physics() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private static InvalidOperationException Outside(string operation) => new($"Authored flags-zero native Chunk proof activated an out-of-scope source branch: {operation}.");
}
