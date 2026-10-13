using System.Security.Cryptography;
using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Independent original Misc frame0 pixels and real chunk-smoke quads, contraction, fading and widescreen proportions.</summary>
public static class ManipulationEffectsImportChecks
{
    private sealed class DebrisQueries(GodotActorWorldQuery query) : IManipulationDebrisHost
    {
        public int Queries { get; private set; }
        public GodotActorWorldHit? LastContact { get; private set; }
        public ManipulationWorldHit? LineToItem(FixedVector3 start, FixedVector3 end, int mode)
        {
            if (mode != 1) throw new InvalidDataException("Original chunk probe changed source environmental query mode1.");
            Queries++;
            LastContact = query.Trace(start, end);
            return LastContact is not { } hit ? null : new(hit.Collider, hit.Position,
                PsxCoordinates.RotateOriginalNormal(hit.Triangle.FixedNormal, hit.Collider is ImportedCollisionBody body
                    ? body.SourceRotation : PsxCoordinates.OriginalRotation(hit.Collider.GlobalBasis)));
        }
    }
    private sealed record Fixture(SubViewport Viewport, GodotManipulationSmoke Effect, Camera3D Camera, ManipulationChunkSmoke Particle);
    public static async Task VerifySmokeAsync(Node3D owner, byte[] originalBits)
    {
        var fixtures = new List<Fixture>();
        var viewports = new List<SubViewport>();
        try
        {
            foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
            {
                var viewport = new SubViewport { Size = size, OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                viewports.Add(viewport);
                owner.AddChild(viewport);
                var shifted = new Node3D { Position = new(37, 81, -22) }; viewport.AddChild(shifted);
                var effect = GodotManipulationSmoke.Create(originalBits); shifted.AddChild(effect);
                var camera = new Camera3D { Current = true, KeepAspect = Camera3D.KeepAspectEnum.Height,
                    Fov = (float)CameraProjection.VerticalFieldOfView(CameraProjection.InitialZoom) };
                viewport.AddChild(camera); camera.GlobalPosition = new(0, 0, 10); camera.LookAt(Vector3.Zero); camera.MakeCurrent();
                var calls = new List<int>();
                var particle = new ManipulationChunkSmoke(new(new(128 << 12, 0, 0), default, 1), range =>
                { calls.Add(range); return range == 40 ? 2 : range == 5 ? 1 : 0; });
                effect.Emit(particle);
                if (!calls.SequenceEqual(new[] { 1000, 4096, 0, 40, 5 }) || effect.GlobalTransform != Transform3D.Identity ||
                    effect.SourceFrame != new SpriteAnimationFrame(-12, -12, 24, 24, 0xFC058B11))
                    throw new InvalidDataException("Chunk smoke changed original quickanim14/Misc frame0, RNG0 order or world-space ownership.");
                using (Image image = effect.SourceTexture.GetImage())
                    if (image.GetWidth() != 24 || image.GetHeight() != 24 || Convert.ToHexString(SHA256.HashData(image.GetData())).ToLowerInvariant() !=
                        "17e077c803b6938de8ee5dd7a5a16ce18d9234ccdd3e89d87840bd9ab07ae0dd")
                        throw new InvalidDataException("Chunk smoke no longer uses independently extracted original24x24 indexed palette pixels.");
                effect.Advance();
                if (particle.Position != new FixedVector3(96 << 12, 0, 0) || particle.Scale != 500 || particle.Angle != 150 || particle.Age != 1 || particle.CodeRgb != 0x2E202020)
                    throw new InvalidDataException("Original smoke first Move must contract one quarter towards origin, halve target scale and delay fading.");
                var fixture = new Fixture(viewport, effect, camera, particle); fixtures.Add(fixture); VerifyGeometry(fixture);
            }
            VerifyAspect(fixtures[0], fixtures[1]);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (fixtures.Any(fixture => fixture.Particle.Age != 1)) throw new InvalidDataException("Render frames advanced original smoke lifetime.");
            for (int move = 2; move <= 10; move++)
            {
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                foreach (Fixture fixture in fixtures)
                {
                    fixture.Effect.Advance();
                    if (move == 2 && fixture.Particle.Position != new FixedVector3(72 << 12, 0, 0))
                        throw new InvalidDataException("Original smoke second Move changed contraction through exact fade-delay equality.");
                    if (move == 3 && (fixture.Particle.Position != new FixedVector3(72 << 12, -4096, 0) || fixture.Particle.CodeRgb != 0x2E1C1C1C))
                        throw new InvalidDataException("Original smoke first fade Move changed vertical rise or per-channel decay.");
                    if (move < 10) VerifyGeometry(fixture);
                    else if (fixture.Effect.Count != 0 || fixture.Effect.RenderMesh.GetSurfaceCount() != 0 || fixture.Particle.Alive)
                        throw new InvalidDataException("Original smoke black-color death retained a native particle or its quad.");
                }
            }
            GD.Print("Verified native original Misc chunk-smoke palette, source contraction/fade/roll, fixed frame,4:3/16:9 proportions and actual quad cleanup.");
        }
        finally { foreach (SubViewport viewport in viewports) viewport.Free(); }
    }
    public static async Task VerifyDebrisAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository)
    {
        var viewport = new SubViewport { Size = new(640, 480), OwnWorld3D = true };
        owner.AddChild(viewport);
        try
        {
            ImportedLevelWorld world = ImportedLevelWorld.Load(repository, "data/L1A3_G.psx"); viewport.AddChild(world);
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A3_T.trg"));
            TriggerActorDefinition source = TriggerActorDefinition.Read(triggers.Nodes[193]);
            if (source.ActorType != 401 || source.Position != new TriggerPosition(598, -68, 704) || source.Orientation.Y != 4040)
                throw new InvalidDataException("Debris checks lost genuine L1A3 node193 independent original definition.");
            var library = repository.Load("data/L1A3_O.psx");
            var calls = new List<int>(); var baddies = new List<GodotManipulationDebris>();
            var registry = new GodotActorRegistry { Triggers = triggers, ObjectLibrary = () => library,
                ObjectTextures = () => repository.Load("data/L1A3_L.psx").Textures.Concat(library.Textures).ToArray(),
                ClaimObjectModel = world.ClaimObjectModel, RandomSource = range => { calls.Add(range); return 0; } };
            viewport.AddChild(registry); registry.SetPhysicsProcess(false);
            var query = new GodotActorWorldQuery(registry, triggers); var queries = new DebrisQueries(query);
            FixedVector3 position = new FixedVector3(598, -68, 704) << 12;
            await Physics(); await Physics();
            int ground = query.GetGroundHeight(position, 0, 500, out _);
            if (ground == -1 || query.LastHit is not { SourceAsset: { } asset, SupportId: null } ||
                !asset.Equals("data/L1A3_G.psx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Original node193 debris must fall onto genuine imported G-library collision.");
            var configuration = new GodotManipulationDebrisConfiguration { WorldQueries = queries,
                AttachToBaddyList = actor => { calls.Add(-1); baddies.Insert(0, actor); },
                DetachFromBaddyList = actor => { calls.Add(-2); baddies.Remove(actor); } };
            GodotManipulationDebris actor = GodotManipulationDebris.Create(
                () => new ManipulationDebrisRequest(0x7640E7BD, position, default), registry, configuration);
            registry.Adopt(actor);
            if (!calls.SequenceEqual(new[] { -1, 4096, 4096, 32, 32, 30 }) || actor.State.ModelChecksum != 0x7640E7BD ||
                library.Scene.Models[actor.ModelIndex].Checksum != 0x7640E7BD || actor.SourceMesh.Mesh!.GetSurfaceCount() == 0 ||
                actor.UpdateGroup != ActorUpdateGroup.Enemy || actor.MinorRadius != 0 || actor.ItemFlags != 0 || actor.BodyFlags != 0x16 ||
                actor.ScriptState.ActorType != 401 || actor.ScriptState.NodeId != ushort.MaxValue || actor.State.RemainingTicks != 60 || !registry.Owns(actor))
                throw new InvalidDataException("Actual CManipObChunk lost original OBJ model, zero-radius BaddyList ownership or constructor RNG/attachment order.");
            FixedVector3 previous = actor.Body.Position;
            registry._PhysicsProcess(1.0 / 60); registry._PhysicsProcess(1.0 / 60);
            if (actor.Body.Position == previous || actor.SourceUpdates != 1 || queries.Queries != 1 || actor.Body.Angles != new ActorAngles(128, 128, 0) ||
                actor.State.RemainingTicks != 58 || !actor.Transform.Origin.IsEqualApprox(PsxCoordinates.FixedWorldPosition(actor.Body.Position)))
                throw new InvalidDataException("Actual chunk did not integrate two source substeps, retain unmasked angular velocity or move its rendered original mesh.");
            for (int update = 0; update < 24 && actor.Lifecycle.Phase == ActorLifePhase.Active; update++)
            { await Physics(); await Physics(); registry._PhysicsProcess(1.0 / 60); registry._PhysicsProcess(1.0 / 60); }
            if (actor.Lifecycle.Phase != ActorLifePhase.Dead || actor.State.HitCount != 1 || queries.LastContact is not { SourceAsset: { } contactAsset } ||
                !contactAsset.Equals("data/L1A3_G.psx", StringComparison.OrdinalIgnoreCase) || queries.LastContact.Position.Y != ground ||
                (actor.BodyFlags & 0x40) == 0)
                throw new InvalidDataException($"Source chunk floor contact mismatch: phase={actor.Lifecycle.Phase}, hits={actor.State.HitCount}, " +
                    $"bodyFlags={actor.BodyFlags:X}, position={actor.Body.Position}, ground={ground}, contact={queries.LastContact?.Position}, " +
                    $"asset={queries.LastContact?.SourceAsset}, updates={actor.SourceUpdates}, remaining={actor.State.RemainingTicks}.");
            registry._PhysicsProcess(1.0 / 60); registry._PhysicsProcess(1.0 / 60);
            if (actor.Lifecycle.Phase != ActorLifePhase.PendingDeletion || !registry.Owns(actor))
                throw new InvalidDataException("Source chunk first death update must retain the body for pending deletion.");
            registry._PhysicsProcess(1.0 / 60); registry._PhysicsProcess(1.0 / 60);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (baddies.Count != 0 || registry.LiveCount != 0 || calls.Count(call => call == -2) != 1)
                throw new InvalidDataException("Original chunk destructor must remove exactly one native BaddyList entry after source delayed cleanup.");
            GD.Print("Verified actual L1A3 OBJ chunk, original zero-radius BaddyList metadata, source constructor RNG, real G-floor first-contact death, rendered motion and delayed destructor cleanup.");
        }
        finally { viewport.Free(); }
        async Task Physics() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private static void VerifyGeometry(Fixture fixture)
    {
        if (fixture.Effect.RenderMesh.GetSurfaceCount() != 1) throw new InvalidDataException("Source smoke lost its actual textured quad.");
        using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(fixture.Effect.RenderMesh.GetRid(), 0);
        Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        Vector2[] uvs = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
        Color[] colors = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
        if (vertices.Length != 6 || uvs.Length != 6 || colors.Length != 6 || fixture.Effect.RenderMesh.SurfaceGetMaterial(0) is not StandardMaterial3D material ||
            material.AlbedoTexture != fixture.Effect.SourceTexture || material.BlendMode != BaseMaterial3D.BlendModeEnum.Add || material.DepthDrawMode != BaseMaterial3D.DepthDrawModeEnum.Disabled)
            throw new InvalidDataException("Original smoke quad lost source texture/additive blending or its six emitted vertices.");
        Vector2 pixelScale = new(fixture.Viewport.Size.Y / 240f * .625f, fixture.Viewport.Size.Y / 240f);
        Vector3 center = PsxCoordinates.FixedWorldPosition(fixture.Particle.Position);
        Vector2 logical = fixture.Camera.UnprojectPosition(center) / pixelScale;
        var corners = ThugPingProjection.Project(fixture.Effect.SourceFrame, unchecked((ushort)fixture.Particle.Scale), fixture.Particle.Angle,
            1000, (int)logical.X, (int)logical.Y, fixture.Particle.PostScale)!;
        int[] order = [0, 1, 2, 1, 3, 2];
        byte raw = (byte)fixture.Particle.CodeRgb;
        float channel = raw == 0 ? 0 : ((raw >> 1) + 15) * 2 / 255f;
        for (int index = 0; index < 6; index++)
        {
            int corner = order[index];
            Vector2 pixel = (new Vector2(corners[corner].X, corners[corner].Y) + logical - new Vector2((int)logical.X, (int)logical.Y)) * pixelScale;
            Vector3 expected = fixture.Camera.ProjectPosition(pixel, 10);
            float coordinate = Math.Max(Math.Max(Math.Abs(expected.X), Math.Abs(expected.Y)), Math.Abs(expected.Z));
            float error = (MathF.BitIncrement(coordinate) - coordinate) * 4;
            if (vertices[index].DistanceTo(expected) > error || uvs[index] != new Vector2((corner & 1) != 0 ? .99f : .01f, (corner & 2) != 0 ? .99f : .01f) ||
                !colors[index].IsEqualApprox(new(channel, channel, channel)))
                throw new InvalidDataException("Original smoke changed source flat-bit roll/post-scale projection, palette UVs or RGB fade.");
        }
    }
    private static void VerifyAspect(Fixture original, Fixture wide)
    {
        static Vector2 Extent(Fixture fixture)
        {
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(fixture.Effect.RenderMesh.GetRid(), 0);
            Vector2[] positions = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Select(fixture.Camera.UnprojectPosition).ToArray();
            return new(positions.Max(p => p.X) - positions.Min(p => p.X), positions.Max(p => p.Y) - positions.Min(p => p.Y));
        }
        Vector2 first = Extent(original), second = Extent(wide);
        float coordinate = 10, error = (MathF.BitIncrement(coordinate) - coordinate) * 16 * 1200 / (20 * Mathf.Tan(Mathf.DegToRad(original.Camera.Fov) / 2));
        if (Math.Abs(second.X - first.X * 1.5f) > error || Math.Abs(second.Y - first.Y * 1.5f) > error)
            throw new InvalidDataException("Widescreen smoke stretched horizontally instead of retaining original height-derived proportions.");
    }
}
