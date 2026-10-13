using System.Security.Cryptography;
using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

internal static class SpriteRingImportChecks
{
    // Independent original bits.psx packet/palette extraction, not renderer-generated expectations.
    private static readonly SpriteAnimationFrame[] OriginalFrames =
    [
        new(-2, -5, 16, 10, 0xD037077D), new(-7, -7, 23, 13, 0xABB11A0A),
        new(-10, -9, 27, 17, 0x4E2590A0), new(-13, -10, 31, 20, 0xE4FEFFBE),
        new(-16, -11, 35, 22, 0xEDD316AE), new(-17, -11, 36, 22, 0x0A05AE00),
        new(-18, -12, 37, 24, 0xAA33D7CA), new(-18, -12, 37, 23, 0xB3BD1DFB)
    ];
    private static readonly string[] OriginalPixelHashes =
    [
        "c7c4e9a7aeaceeddf84c7d056ca42288a31353665c13a56cdce019e07aee1b8c",
        "97a9384988b09a6d204935eebaba3d45a6b9dbdfb435f7a3b233ca3e86c1343f",
        "719094555bc9e99c62e197ad9aa95386a881ee4b20972a95c01c39354a29a9d0",
        "c42de24ada462cc4948a577ff233118b735e86b17a1bc7dd40b49af4bd23f05d",
        "c2c70c522775065846324fbf91413fd87aebfb2acc303a625a1c42b79d423154",
        "8a1a30e2bec09111e4cbd302df4cc4e826756e8247440f8edc3098889f012a18",
        "1164e3df77d153263fd846f616e601da1944ef6ce5d95cf6c44733b99b03a697",
        "69e52b43b5c941f58b87cab91558cbc20dad7b5794eb65fbf26ad79e745f41d8"
    ];

    private sealed class Host(GodotActorWorldQuery world, GodotActorSpriteRing effect) : IActorSpriteRingHost
    {
        public int Calls;
        public int Ground;
        public FixedVector3 QueryPosition;
        public int GroundHeight(FixedVector3 position, int above, int below)
        {
            if (above != 300 || below != 600) throw new InvalidDataException("The actor ring changed its source ground ranges.");
            Calls++; QueryPosition = position;
            return Ground = world.GroundHeight(position, above, below);
        }
        public void EmitSprite(ActorSpriteRingParticle particle) => effect.Emit(particle);
    }

    private sealed record ViewFixture(SubViewport Viewport, GodotActorSpriteRing Effect, Camera3D Camera,
        SpriteRingParticleMotion First, FixedVector3 Start);

    public static async Task VerifyAsync(Node3D owner, byte[] bitsBytes, TriggerFile triggers, GodotActorWorldQuery world)
    {
        if (Engine.PhysicsTicksPerSecond != 60) throw new InvalidOperationException("Source bit checks require two60Hz physics frames per30Hz update.");
        TriggerActorDefinition definition = TriggerActorDefinition.Read(triggers.Nodes[9]);
        if (definition.ActorType != 312 || definition.Position != new TriggerPosition(-18937, 1242, -19681))
            throw new InvalidDataException("Sprite ring check requires the original L1A1 startup thug node9.");
        var body = new ActorBodyMotion
        {
            Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
            Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z)
        };
        int actualFloor = world.GroundHeight(body.Position, 300, 600);
        if (actualFloor == -1 || world.LastHit is not { SourceAsset: { } asset } || !asset.Contains("L1A1_G", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Sprite ring grounding did not hit the original imported L1A1 floor.");
        var fixtures = new List<ViewFixture>();
        var viewports = new List<SubViewport>();
        try
        {
            foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
            {
                var viewport = new SubViewport { Name = $"SourceSmoke{size.X}x{size.Y}", Size = size,
                    OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                viewports.Add(viewport);
                owner.AddChild(viewport);
                var shiftedParent = new Node3D { Position = new(37, 81, -22) };
                viewport.AddChild(shiftedParent);
                var effect = GodotActorSpriteRing.Create(bitsBytes);
                shiftedParent.AddChild(effect);
                VerifyAssets(effect);
                var host = new Host(world, effect);
                ActorSpriteRing.Create(body, new(0, 0, 204800), host);
                if (host.Calls != 1 || host.QueryPosition != body.Position || host.Ground != actualFloor || effect.Count != 24 ||
                    effect.LiveParticles.Any(particle => particle.Position.Y != unchecked(actualFloor - 0xA000)) || effect.GlobalTransform != Transform3D.Identity)
                    throw new InvalidDataException("Original ring placement changed its actor-center floor query,24 emissions, ground clearance or world-space ownership.");
                SpriteRingParticleMotion first = effect.LiveParticles[^1];
                if (first.Velocity != new FixedVector3(32768, 0, 0)) throw new InvalidDataException("Ring angle0 lost its original cos*8 velocity.");
                Vector3 center = PsxCoordinates.FixedWorldPosition(first.Position);
                var camera = new Camera3D { Name = "SpriteRingCheckCamera", Current = true, Near = 0.01f, Far = 1000,
                    KeepAspect = Camera3D.KeepAspectEnum.Height, Fov = (float)CameraProjection.VerticalFieldOfView(CameraProjection.InitialZoom) };
                shiftedParent.AddChild(camera);
                camera.GlobalPosition = center + new Vector3(0, 0, 10);
                camera.LookAt(center);
                camera.MakeCurrent();
                effect.Rebuild();
                fixtures.Add(new(viewport, effect, camera, first, first.Position));
                VerifyGeometry(effect, camera, 0);
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            foreach (ViewFixture fixture in fixtures)
                if (fixture.First.Age != 0 || fixture.First.Frame != 0 || fixture.First.Position != fixture.Start)
                    throw new InvalidDataException("Native render frames advanced source sprite motion without a bit update.");
            VerifyAspect(fixtures[0], fixtures[1]);
            for (int update = 1; update <= 8; update++)
            {
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                foreach (ViewFixture fixture in fixtures)
                {
                    fixture.Effect.Advance();
                    if (update < 8)
                    {
                        if (fixture.Effect.Count != 24 || fixture.First.Position != fixture.Start + new FixedVector3(32768 * update, 0, 0) ||
                            fixture.First.Scale != 512 - 32 * update || fixture.First.Age != update || fixture.First.Frame != (update + 1) / 2)
                            throw new InvalidDataException("Native SMOKE ring changed source motion, shrinkage or odd-age animation timing.");
                        VerifyGeometry(fixture.Effect, fixture.Camera, update);
                    }
                }
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            foreach (ViewFixture fixture in fixtures)
            {
                if (fixture.Effect.Count != 0 || fixture.Effect.VisibleSprites != 0 || fixture.Effect.RenderMesh.GetSurfaceCount() != 0 ||
                    fixture.First.Alive || fixture.First.Age != 7 || fixture.First.Scale != 256)
                    throw new InvalidDataException("Ring particles must expire at the eighth color update and release every visible quad.");
                fixture.Effect.Free();
                if (!fixture.Effect.ResourcesDisposed) throw new InvalidDataException("The ring emitter retained its native meshes/materials/textures after scene exit.");
            }
            GD.Print($"Verified native sprite ring: original L1A1 node9 floor{actualFloor},8SMOKE frame/pixel hashes,24source quads in640x480/1280x720, Hor+ proportions, exact UV/additive brightness,30Hz8-update motion/fade and cleanup.");
        }
        finally { foreach (SubViewport viewport in viewports) viewport.Free(); }
    }

    private static void VerifyAspect(ViewFixture standard, ViewFixture wide)
    {
        static Vector2 Pixels(ViewFixture fixture)
        {
            var sprite = fixture.Effect.RenderedSprites.Single(sprite => ReferenceEquals(sprite.Motion, fixture.First));
            int surface = fixture.Effect.RenderedSprites.ToList().IndexOf(sprite);
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(fixture.Effect.RenderMesh.GetRid(), surface);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Vector2 first = fixture.Camera.UnprojectPosition(vertices[0]), right = fixture.Camera.UnprojectPosition(vertices[1]), bottom = fixture.Camera.UnprojectPosition(vertices[2]);
            return new(right.X - first.X, bottom.Y - first.Y);
        }
        Vector2 original = Pixels(standard), widescreen = Pixels(wide);
        float error = ProjectionRoundoff(standard.Camera, standard.Start, standard.Viewport.Size) +
            ProjectionRoundoff(wide.Camera, wide.Start, wide.Viewport.Size);
        if (Math.Abs(widescreen.X - original.X * 1.5f) > error || Math.Abs(widescreen.Y - original.Y * 1.5f) > error)
            throw new InvalidDataException("16:9 changed the sprite's proportions instead of scaling both axes from viewport height.");
        // An actual source ring remains in place; only the two native cameras move sideways.
        foreach (ViewFixture fixture in new[] { standard, wide })
        {
            Vector3 center = PsxCoordinates.FixedWorldPosition(fixture.Start);
            fixture.Camera.GlobalPosition = center + new Vector3(-6.5f, 0, 10);
            fixture.Effect.Rebuild();
        }
        Vector3 point = PsxCoordinates.FixedWorldPosition(standard.Start);
        if (standard.Camera.IsPositionInFrustum(point) || !wide.Camera.IsPositionInFrustum(point))
            throw new InvalidDataException("The real16:9 viewport did not add horizontal view over the original4:3 framing.");
        var side = wide.Effect.RenderedSprites.Single(sprite => ReferenceEquals(sprite.Motion, wide.First));
        float scaleX = wide.Viewport.Size.Y / 240f * 0.625f;
        if (side.LogicalCorners.Any(corner => corner.X * scaleX < 0 || corner.X * scaleX > wide.Viewport.Size.X))
            throw new InvalidDataException("A genuine Hor+ side sprite was clipped by the original narrow horizontal span.");
        foreach (ViewFixture fixture in new[] { standard, wide })
        {
            fixture.Camera.GlobalPosition = PsxCoordinates.FixedWorldPosition(fixture.Start) + new Vector3(0, 0, 10);
            fixture.Effect.Rebuild();
        }
    }

    private static float ProjectionRoundoff(Camera3D camera, FixedVector3 source, Vector2I size)
    {
        Vector3 position = PsxCoordinates.FixedWorldPosition(source);
        float coordinate = Math.Max(Math.Max(Math.Abs(position.X), Math.Abs(position.Y)), Math.Abs(position.Z));
        float ulp = MathF.BitIncrement(coordinate) - coordinate;
        float depth = -camera.GlobalBasis.Z.Dot(position - camera.GlobalPosition);
        return ulp * 16 * size.Y / (2 * depth * Mathf.Tan(Mathf.DegToRad(camera.Fov) / 2));
    }

    private static void VerifyAssets(GodotActorSpriteRing effect)
    {
        if (!effect.SourceFrames.SequenceEqual(OriginalFrames)) throw new InvalidDataException("The original eight SMOKE rectangles/checksums changed.");
        for (int frame = 0; frame < OriginalFrames.Length; frame++)
        {
            using Image image = effect.Textures[frame].GetImage();
            byte[] pixels = image.GetData();
            if (image.GetWidth() != OriginalFrames[frame].Width || image.GetHeight() != OriginalFrames[frame].Height ||
                Convert.ToHexString(SHA256.HashData(pixels)).ToLowerInvariant() != OriginalPixelHashes[frame])
                throw new InvalidDataException($"Native SMOKE frame{frame} changed its original palette-expanded pixels or dimensions.");
            StandardMaterial3D material = effect.Materials[frame];
            if (material.AlbedoTexture != effect.Textures[frame] || material.BlendMode != BaseMaterial3D.BlendModeEnum.Add ||
                material.DepthDrawMode != BaseMaterial3D.DepthDrawModeEnum.Disabled || material.TextureFilter != BaseMaterial3D.TextureFilterEnum.Linear ||
                material.AlbedoColor.A != 128 / 255f || material.TextureRepeat)
                throw new InvalidDataException("Original sprite quads lost their frame texture, SRCALPHA+ONE blend, no-depth-write or source alpha/filter contract.");
        }
    }

    private static void VerifyGeometry(GodotActorSpriteRing effect, Camera3D camera, int update)
    {
        if (effect.VisibleSprites != 24 || effect.RenderMesh.GetSurfaceCount() != 24)
            throw new InvalidDataException("Original sprite ring did not render all24 textured quads at a valid source depth.");
        Vector2 viewport = effect.GetViewport().GetVisibleRect().Size;
        int[] cornerOrder = [0, 1, 2, 1, 3, 2];
        for (int surface = 0; surface < 24; surface++)
        {
            var sprite = effect.RenderedSprites[surface];
            SpriteAnimationFrame frame = OriginalFrames[sprite.Motion.Frame];
            int perspective = (sprite.Motion.Scale << 7) / sprite.Depth;
            Vector3 center = PsxCoordinates.FixedWorldPosition(sprite.Motion.Position);
            Vector2 pixelCenter = camera.UnprojectPosition(center);
            float scaleY = viewport.Y / 240, scaleX = scaleY * 0.625f;
            float left = pixelCenter.X / scaleX + ((frame.OffsetX * perspective) >> 7), top = pixelCenter.Y / scaleY + ((frame.OffsetY * perspective) >> 7);
            int width = (frame.Width * perspective) >> 7, height = (frame.Height * perspective) >> 7;
            if (width < 5) width = 2;
            if (height < 5) height = 1;
            Vector2[] originalCorners = [new(left, top), new(left + width, top), new(left, top + height), new(left + width, top + height)];
            if (!sprite.LogicalCorners.SequenceEqual(originalCorners))
                throw new InvalidDataException("Native quad sizing/offsets differ from original DisplayFlatBitList integer pixels.");
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(effect.RenderMesh.GetRid(), surface);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Vector2[] uv = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            Color[] colors = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
            if (vertices.Length != 6 || uv.Length != 6 || colors.Length != 6 || effect.RenderMesh.SurfaceGetMaterial(surface) != effect.Materials[sprite.Motion.Frame])
                throw new InvalidDataException("A native source sprite lost its six textured triangle vertices or actual frame material.");
            int gray = 128 - update * 16;
            float brightness = gray == 0 ? 0 : Math.Min(255, ((gray >> 1) + 15) * 2) / 255f;
            float depth = -camera.GlobalBasis.Z.Dot(center - camera.GlobalPosition);
            for (int vertex = 0; vertex < 6; vertex++)
            {
                int corner = cornerOrder[vertex];
                Vector2 pixel = originalCorners[corner] * new Vector2(scaleX, scaleY);
                Vector3 expected = camera.ProjectPosition(pixel, depth);
                float coordinate = Math.Max(Math.Max(Math.Abs(expected.X), Math.Abs(expected.Y)), Math.Abs(expected.Z));
                float roundoff = (MathF.BitIncrement(coordinate) - coordinate) * 4;
                if (vertices[vertex].DistanceTo(expected) > roundoff || uv[vertex] != new Vector2((corner & 1) != 0 ? 0.99f : 0.01f,
                        (corner & 2) != 0 ? 0.99f : 0.01f) || !colors[vertex].IsEqualApprox(new(brightness, brightness, brightness)))
                    throw new InvalidDataException("Native SMOKE geometry changed source pixel placement, UV inset, RGB decay or default PC brightness.");
            }
        }
    }
}
