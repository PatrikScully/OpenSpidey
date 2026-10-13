using System.Buffers.Binary;
using System.Diagnostics;
using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Gameplay;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Unchanged L1A3 control230, actual world RNG/shared Pixel phase, and bounded source CPixel GPU/destructor ownership.</summary>
internal static class SparkImportChecks
{
    private sealed record Emission(SparkParticle Bit, FixedVector3 Position, FixedVector3 Velocity, ushort Lifetime,
        byte Red, byte Green, byte Blue, uint Clock);

    public static async Task VerifyAsync(StoryLevelSession story)
    {
        GodotActorRegistry registry = story.Registry;
        GodotSparkParticles renderer = registry.Sparks as GodotSparkParticles ??
            throw new InvalidDataException("Actual story sparks must use the original native Pixel owner.");
        IActorSparkEmitter original = renderer;
        TriggerNode node = registry.Triggers.Nodes[230];
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        ushort[] words = Words(definition.BehaviorData);
        ushort[] expected = [0x4104, 0, 0x4114, 1, 0x2129, 56, 0x4114, 0x2129, 2, 0, 0x428F, 56,
            0x4292, 32, 0x4102, 0, 0x4120, 0x428F, 57, 0x4292, 32, 0x4120, 0x4102, 0, 0x4100];
        Assert(story.Failure is null && story.Startup.Phase == MissionStartupPhase.Playing &&
            story.Application.MissionName.Equals("L1A3_T", StringComparison.OrdinalIgnoreCase) &&
            definition.ActorType == 203 && !definition.InitializeOnLoad && !definition.StartsSuspended && words.SequenceEqual(expected),
            "Authored230 retains its actual56/2 random conditions,56/57 positional SFX, two raw32-spark4292 batches and label0 jumps; no replacement program or preseeded outcome is used.");
        var records = new List<Emission>();
        int published = 0, gateReads = 0, beforeBatch = -1;
        SparkParticle? allocated = null;
        bool constructing = false;
        bool createdActor = false;
        GodotControlActor? actor = registry.SourceList(ActorUpdateGroup.Control).SourceOrder.OfType<GodotControlActor>()
            .SingleOrDefault(body => body.SourceNodeId == 230);
        GodotPlayerCombatRuntime combat = Descendants(story).OfType<GodotPlayerCombatRuntime>().Single();
        Assert(ReferenceEquals(combat.Particles, renderer.SourceScheduler),
            "The actual actor4292 emitter and late source bit phase must use the same canonical14-head scheduler.");
        int behaviorBase = checked(node.SourceOffset + definition.ObjectDataOffset + 18);
        int[] emissionCursors = Enumerable.Range(0, words.Length).Where(index => words[index] == 0x4292)
            .Select(index => behaviorBase + (index + 2) * 2).ToArray();
        var recorder = new Recorder(renderer, () =>
        {
            GodotControlActor? current = registry.SourceList(ActorUpdateGroup.Control).SourceOrder.OfType<GodotControlActor>()
                .SingleOrDefault(body => body.SourceNodeId == 230);
            if (current is null || !emissionCursors.Contains(current.Runtime.SourcePosition)) return false;
            actor = current; return records.Count < 32;
        }, request =>
        {
            constructing = true; allocated = null;
            if (records.Count == 0) beforeBatch = renderer.LiveBitCount;
            Assert(actor is not null && request == new SparkSpawnRequest(actor.SourcePosition, 32, 8192, 32) &&
                renderer.Settings.AllocationUsage == 0 && renderer.Settings.Trajectory == actor.Body.Angles &&
                renderer.Settings.Cone == new ActorAngles(512, 4096, 0) && renderer.Settings.Size == 0x10001 &&
                renderer.Settings.Red == 128 && renderer.Settings.Green == 128 && renderer.Settings.Blue == 128 &&
                renderer.Settings.FadeRed == 4 && renderer.Settings.FadeGreen == 4 && renderer.Settings.FadeBlue == 4,
                "Actual230 commits the count operand before each original76-byte constructor and retains its live body pose and exact process cone/RGB/fade/usage0 globals.");
        }, request =>
        {
            SparkParticle bit = allocated ?? throw new InvalidDataException("The original native Spark constructor did not publish its source identity.");
            Assert(bit.Position == request.Position && bit.Age == 0 && bit.Lifetime < 32 && bit.Acceleration == new FixedVector3(0, 8192, 0) &&
                bit.Friction == new FixedVector3(3, 3, 3) && bit.Tag == 0x02000000 && bit.WidthHeight == 1 &&
                bit.Code == (renderer.Settings.SemiTransparent ? 106 : 104) && bit.Alive && renderer.LiveBitCount == beforeBatch + records.Count + 1,
                "The genuine native Spark completes its real cone/lifetime draws after publication, retaining CPixel tag/size/code and the one-time batch allocation policy.");
            records.Add(new(bit, bit.Position, bit.Velocity, bit.Lifetime, bit.Red, bit.Green, bit.Blue, story.SourceClock));
            constructing = false;
            if (records.Count == 32)
                Assert(renderer.SourceScheduler.Identities(SourceParticleList.Pixel).Take(32).SequenceEqual(records.AsEnumerable().Reverse().Select(entry => (object)entry.Bit)),
                    "The untouched live batch inserts all32 genuine identities at the canonical Pixel head in reverse successful-constructor order.");
        }, () => gateReads++);
        renderer.Spawned += OnPublication;
        registry.Sparks = recorder;
        try
        {
            if (actor is null)
            {
                createdActor = true; registry.Spawn(node);
                actor = registry.SourceList(ActorUpdateGroup.Control).SourceOrder.OfType<GodotControlActor>().Single(body => body.SourceNodeId == 230);
            }
            var deadline = Stopwatch.StartNew();
            while (records.Count < 32)
            {
                Healthy();
                if (deadline.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Actual230 world RNG did not select its original32-spark branch.");
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Assert(published == 32 && gateReads >= 1 && beforeBatch < 200 && renderer.Settings.AllocationUsage == 1 &&
                ReferenceEquals(registry.Sparks, recorder) && registry.Owns(actor!) && actor!.ScriptState.Active,
                "The actual signed BitCount gate precedes the whole batch; source usage returns to1 while the original looping control remains attached and active.");
            registry.Sparks = original; renderer.Spawned -= OnPublication;
            if (createdActor) { registry.DestroyImmediately(actor!); actor = null; }
            deadline.Restart();
            while (records.Any(entry => renderer.SourceOrder.Contains(entry.Bit)))
            {
                Healthy(); VerifyMovedRecords();
                if (deadline.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("The actual late source Pixel Move/dead-cleanup phase retained an expired Spark.");
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            VerifyMovedRecords();
            Assert(records.All(entry => !entry.Bit.Alive && entry.Bit.Age == Math.Max(1, (int)entry.Lifetime) &&
                !renderer.SourceScheduler.Identities(SourceParticleList.Pixel).Contains(entry.Bit)) &&
                GodotObject.IsInstanceValid(renderer) && !renderer.ResourcesDisposed,
                "Actual age1..31, including zero-lifeage1, must retire only its real source bits while retaining the live scene's native renderer resources.");
            await VerifyRendererOwnershipAsync(story, definition, registry);
            GD.Print("Verified actual L1A3 control2304292: untouched56/2 world-RNG selection,32 source allocations before RNG, canonical Pixel head/late move/death phases, two-aspect native CPixel quads and deterministic reparent/destructor resources.");
        }
        finally
        {
            registry.Sparks = original; renderer.Spawned -= OnPublication;
            if (createdActor && actor is not null && GodotObject.IsInstanceValid(actor)) registry.DestroyImmediately(actor);
        }

        void Healthy()
        {
            if (story.Failure is { } failure) throw new InvalidOperationException("Actual Spark story phase failed.", failure);
        }
        void OnPublication(SparkParticle bit)
        {
            if (!constructing) return;
            published++; allocated = bit;
            Assert(bit.Position == default && bit.Velocity == default && bit.Age == 0 && bit.Lifetime == 0 &&
                ReferenceEquals(renderer.SourceOrder[0], bit) && ReferenceEquals(renderer.SourceScheduler.Identities(SourceParticleList.Pixel)[0], bit),
                "Native CPixel publication happens at the original base constructor before Spark position, velocity and random lifetime initialization.");
        }
        void VerifyMovedRecords()
        {
            foreach (Emission entry in records)
            {
                SparkParticle bit = entry.Bit;
                FixedVector3 position = entry.Position, velocity = entry.Velocity;
                for (int update = 0; update < bit.Age; update++)
                {
                    position += velocity;
                    velocity += new FixedVector3(0, 8192, 0);
                    velocity = new(unchecked(velocity.X - (velocity.X >> 3)), unchecked(velocity.Y - (velocity.Y >> 3)),
                        unchecked(velocity.Z - (velocity.Z >> 3)));
                }
                Assert(bit.Age is >= 0 and <= 31 && position == bit.Position && velocity == bit.Velocity &&
                    bit.Red == unchecked((byte)(entry.Red - bit.Age * 4)) && bit.Green == unchecked((byte)(entry.Green - bit.Age * 4)) &&
                    bit.Blue == unchecked((byte)(entry.Blue - bit.Age * 4)) && (!renderer.SourceOrder.Contains(bit) || bit.Alive),
                    $"The real late Pixel phase retains source integrate→accelerate→arithmetic-friction→signedage→bytefade order: emittedclock{entry.Clock}, sourceclock{story.SourceClock}, age{bit.Age}, life{entry.Lifetime}.");
            }
        }
    }

    private static async Task VerifyRendererOwnershipAsync(StoryLevelSession story, TriggerActorDefinition definition, GodotActorRegistry registry)
    {
        GameRandom actualRandom = registry.RandomSource.Target as GameRandom ??
            throw new InvalidDataException("The genuine source RNG owner must expose its current state for an independent private render proof.");
        var random = new GameRandom(0); random.Restore(actualRandom.State);
        var scheduler = new SourceParticleScheduler();
        var settings = new SparkSettings { SemiTransparent = ((GodotSparkParticles)registry.Sparks!).Settings.SemiTransparent };
        var viewport = new SubViewport { Size = new(640, 480), OwnWorld3D = true }; story.AddChild(viewport);
        FixedVector3 sourcePosition = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12;
        GodotSparkParticles? effect = null, sibling = null;
        Resource[] resources = [], siblingResources = [];
        try
        {
            var root = new Node3D(); viewport.AddChild(root);
            Vector3 center = PsxCoordinates.FixedWorldPosition(sourcePosition);
            var camera = new Camera3D { Current = true, Near = 0.01f, Far = 500 }; root.AddChild(camera);
            camera.GlobalPosition = center + new Vector3(0, 0, 12); camera.LookAt(center);
            int randomCalls = 0, publications = 0;
            SparkParticle? current = null;
            effect = GodotSparkParticles.Create(scheduler, settings, bound =>
            {
                Assert(current is not null && effect is not null && settings.AllocationUsage == 0 &&
                    ReferenceEquals(effect.SourceOrder[0], current) && ReferenceEquals(scheduler.Identities(SourceParticleList.Pixel)[0], current),
                    "Each actual cloned-source RNG call must observe the already allocated native CPixel source head and batch usage0.");
                randomCalls++; return random.Next(bound);
            }); root.AddChild(effect);
            effect.Spawned += bit => { current = bit; publications++; };
            EmitOriginal(effect);
            Assert(publications == 32 && randomCalls == 96 && scheduler.Count == 32 && settings.AllocationUsage == 1 &&
                effect.SourceOrder.All(bit => bit.Position == sourcePosition && bit.Age == 0),
                "The unchanged source4292 operand supplies32 private genuine-position pixels and exactly X/Y/lifetime96 draws from a clone of the actual current stream, without changing or preseeding production RNG outcomes.");
            foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
            {
                viewport.Size = size;
                await story.ToSignal(story.GetTree(), SceneTree.SignalName.ProcessFrame);
                effect.Rebuild();
                Assert(effect.PixelMesh.GetSurfaceCount() == 32, "The actual positive-depth source pixels must upload one real native quad surface per source bit at both game aspects.");
                VerifyPixels(effect, camera, size);
            }
            camera.GlobalPosition = center;
            effect.Rebuild(); Assert(effect.PixelMesh.GetSurfaceCount() == 0, "Zero-depth actual source pixels must not call Godot projection or upload fabricated vertices.");
            camera.GlobalPosition = center - new Vector3(0, 0, 12);
            effect.Rebuild(); Assert(effect.PixelMesh.GetSurfaceCount() == 0, "Original positive-inverseW gating rejects the same untouched positions behind the camera.");
            camera.GlobalPosition = center + new Vector3(0, 0, 12);
            for (uint tick = 0; scheduler.Count != 0 && tick < 32; tick++) { scheduler.Move(tick); scheduler.RemoveDead(); }
            effect.Rebuild();
            Assert(scheduler.Count == 0 && effect.SourceOrder.Count == 0 && effect.PixelMesh.GetSurfaceCount() == 0,
                "Actual private source lifetimes and dead cleanup detach all32 original pixels and clear their native submitted geometry.");

            resources = effect.CaptureOwnedResources();
            sibling = GodotSparkParticles.Create(scheduler, settings, random.Next); root.AddChild(sibling); EmitOriginal(sibling);
            siblingResources = sibling.CaptureOwnedResources();
            Assert(scheduler.Count == 32 && siblingResources.All(resource => resources.All(other => other.GetInstanceId() != resource.GetInstanceId())),
                "Independent actual Spark render owners retain separate ImmediateMesh/opaque/semitransparent resources and genuine source bits.");
            var former = new Node3D(); root.AddChild(former); sibling.Reparent(former); sibling.Reparent(root); former.Free();
            Assert(!sibling.ResourcesDisposed && siblingResources.All(GodotObject.IsInstanceValid) && scheduler.Count == 32,
                "Native Spark mesh/material/list ownership survives reparent and the former scene container's destruction.");
            sibling.Free();
            Assert(sibling.ResourcesDisposed && scheduler.Count == 0 && siblingResources.All(resource => !GodotObject.IsInstanceValid(resource)) &&
                !effect.ResourcesDisposed && resources.All(GodotObject.IsInstanceValid),
                "Actual live Spark owner Predelete must detach its32 unexpired identities and dispose only its own native mesh/material resources.");
            effect.Free();
            Assert(effect.ResourcesDisposed && resources.All(resource => !GodotObject.IsInstanceValid(resource)),
                "The remaining renderer's real Predelete releases its private native resources after source lifetime cleanup.");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(viewport)) viewport.Free();
            await story.ToSignal(story.GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        void EmitOriginal(GodotSparkParticles emitter)
        {
            ushort[] words = Words(definition.BehaviorData);
            int opcode = Array.IndexOf(words, (ushort)0x4292);
            var operands = new ActorScriptCursor(definition.BehaviorData[(opcode * 2 + 2)..],
                registry.Triggers.Nodes[230].SourceOffset + definition.ObjectDataOffset + 18 + opcode * 2 + 2);
            Assert(ActorSparkCommand.TryExecute(0x4292, operands, sourcePosition,
                new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z), emitter),
                "Private renderer proof consumes the original authored source count bytes through the same typed4292 leaf.");
        }
    }

    private static void VerifyPixels(GodotSparkParticles effect, Camera3D camera, Vector2I size)
    {
        byte[] brightness = GodotGlowFlash.SourceBrightness();
        for (int surface = 0; surface < effect.SourceOrder.Count; surface++)
        {
            SparkParticle bit = effect.SourceOrder[surface];
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(effect.PixelMesh.GetRid(), surface);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Color[] colours = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
            Vector2[] projected = vertices.Select(camera.UnprojectPosition).ToArray();
            Vector2 origin = camera.UnprojectPosition(PsxCoordinates.FixedWorldPosition(bit.Position));
            Vector2 extent = new(size.Y / 240f * .625f * bit.DisplayWidth, size.Y / 240f * bit.DisplayWidth);
            int[] corners = [0, 1, 2, 1, 3, 2];
            Color wanted = new(brightness[bit.Red] / 255f, brightness[bit.Green] / 255f, brightness[bit.Blue] / 255f);
            Assert(vertices.Length == 6 && colours.Length == 6 && colours.All(colour => colour.IsEqualApprox(wanted)) &&
                projected.Select((point, index) => point.DistanceTo(origin + new Vector2((corners[index] & 1) != 0 ? extent.X : 0,
                    (corners[index] & 2) != 0 ? extent.Y : 0)) < .1f).All(value => value) &&
                effect.PixelMesh.SurfaceGetMaterial(surface) is StandardMaterial3D material && material.AlbedoTexture is null &&
                material.DepthDrawMode == BaseMaterial3D.DepthDrawModeEnum.Disabled &&
                material.BlendMode == ((bit.Code & 2) != 0 ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix) &&
                Math.Abs(material.AlbedoColor.A - ((bit.Code & 2) != 0 ? 128 / 255f : 1)) < .0001f,
                "Native CPixel uses the original width low nibble for both extents, six real vertices, direct-RGB PC brightness, slot1's untextured quad and actual code2 alpha128 blend; widescreen retains the established source pixel density.");
        }
    }

    private sealed class Recorder(GodotSparkParticles renderer, Func<bool> selected, Action<SparkSpawnRequest> before,
        Action<SparkSpawnRequest> after, Action gate) : IActorSparkEmitter
    {
        public SparkSettings Settings => renderer.Settings;
        public int LiveBitCount { get { gate(); return renderer.LiveBitCount; } }
        public void TrySpawnSpark(SparkSpawnRequest request)
        {
            bool capture = selected(); if (capture) before(request);
            renderer.TrySpawnSpark(request); if (capture) after(request);
        }
    }
    private static ushort[] Words(ReadOnlyMemory<byte> bytes) => Enumerable.Range(0, bytes.Length / 2)
        .Select(index => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Span[(index * 2)..])).ToArray();
    private static IEnumerable<Node> Descendants(Node root)
    { foreach (Node child in root.GetChildren()) { yield return child; foreach (Node descendant in Descendants(child)) yield return descendant; } }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
