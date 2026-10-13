using Godot;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.World;

internal static class CrateEffectsImportChecks
{
    private sealed record Fixture(SubViewport Viewport, Camera3D Camera, GodotExplosionFragments Fragments,
        GodotGlowFlash Glows, GodotItemShatter Shatter, SourceParticleScheduler Scheduler, List<int> RandomRanges,
        GodotCrateEffects Effects);

    public static async Task VerifyAsync(Node3D owner, byte[] bitsBytes)
    {
        var fixtures = new List<Fixture>();
        try
        {
            foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
            {
                var viewport = new SubViewport { Size = size, OwnWorld3D = true,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                owner.AddChild(viewport);
                var camera = new Camera3D { Current = true, Position = new(0, 0, 10),
                    KeepAspect = Camera3D.KeepAspectEnum.Height,
                    Fov = (float)CameraProjection.VerticalFieldOfView(CameraProjection.InitialZoom) };
                viewport.AddChild(camera); camera.LookAt(Vector3.Zero); camera.MakeCurrent();
                var shifted = new Node3D { Position = new(37, 81, -22) }; viewport.AddChild(shifted);
                var fragments = GodotExplosionFragments.Create(bitsBytes); shifted.AddChild(fragments);
                var glows = new GodotGlowFlash(); shifted.AddChild(glows);
                var ranges = new List<int>();
                int Random(int range) { ranges.Add(range); return range > 0 ? range - 1 : 0; }
                var shatter = GodotItemShatter.Create((_, _) => throw new InvalidDataException("Crate explosion leaves unexpectedly traced terrain."), Random);
                shifted.AddChild(shatter);
                var scheduler = new SourceParticleScheduler();
                fragments.SourceBitCreated += bit => scheduler.Attach(SourceParticleList.Flat, bit);
                fragments.SourceBitRemoved += identity => scheduler.Detach(identity);
                glows.SourceBitCreated += bit => scheduler.Attach(SourceParticleList.Glow, bit);
                glows.SourceBitRemoved += identity => scheduler.Detach(identity);
                var effects = new GodotCrateEffects(shatter, fragments, glows,
                    _ => throw new InvalidDataException("Crate explosion leaves unexpectedly read ENV shatter textures."),
                    _ => throw new InvalidDataException("Crate explosion leaves unexpectedly triggered a command point."), Random);
                var fixture = new Fixture(viewport, camera, fragments, glows, shatter, scheduler, ranges, effects);
                fixtures.Add(fixture);
                effects.CreateCrateExplosion(default, true);
                VerifyConstructor(fixture, 70);
                VerifyGeometry(fixture);
            }
            Vector2 normal = SpriteSize(fixtures[0]), wide = SpriteSize(fixtures[1]);
            if (Math.Abs(wide.X - normal.X * 1.5f) > .003f || Math.Abs(wide.Y - normal.Y * 1.5f) > .003f)
                throw new InvalidDataException("Widescreen changed the original CFrag sprite proportions.");
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            foreach (Fixture fixture in fixtures)
            {
                if (fixture.Fragments.SourceFragmentOrder.Any(fragment => fragment.Age != 0) ||
                    fixture.Glows.SourceGlowOrder.Single() is not GlowFlashState { Age: 0, CentreCodeBgr: 0x3200C8F0 })
                    throw new InvalidDataException("Rendering advanced the actual crate explosion's source bits.");
                for (uint tick = 1; tick <= 29; tick++)
                {
                    fixture.Scheduler.Move(tick);
                    if (tick == 1 && fixture.Fragments.SourceFragmentOrder.Any(fragment => fragment.Frame != Math.Min(1, fragment.FrameCount - 1) || fragment.DisplayFrame != 0))
                        throw new InvalidDataException("CFrag Move rewrote its retained first-frame texture pointer.");
                    if (tick == 29 && (fixture.Fragments.Count != 19 || fixture.Fragments.SourceFragmentOrder.Any(fragment => fragment.Alive)))
                        throw new InvalidDataException("CFrag source death performed deletion before the separate bit cleanup phase.");
                    fixture.Scheduler.RemoveDead();
                }
                fixture.Fragments.Rebuild(); fixture.Glows.Rebuild();
                if (fixture.Scheduler.Count != 0 || fixture.Fragments.Count != 0 || fixture.Glows.Count != 0 ||
                    fixture.Fragments.RenderMesh.GetSurfaceCount() != 0 || fixture.Glows.RenderMesh.GetSurfaceCount() != 0)
                    throw new InvalidDataException("Crate effects retained source bits or native geometry after genuine cleanup.");

                fixture.RandomRanges.Clear(); fixture.Effects.CreateCrateExplosion(default, false);
                VerifyConstructor(fixture, 200);
                foreach (ExplosionFragment fragment in fixture.Fragments.SourceFragmentOrder.ToArray()) fixture.Fragments.RemoveFragment(fragment);
                fixture.Glows.Remove(fixture.Glows.SourceGlowOrder.Single());
                var protectedGlow = new GlowState(4, 2) { Protected = true };
                fixture.Glows.Emit(protectedGlow); fixture.Scheduler.Move(30); fixture.Scheduler.RemoveDead();
                if (fixture.Glows.Count != 1 || fixture.Scheduler.Count != 1)
                    throw new InvalidDataException("Protected CGlow was treated as a lifetime-bearing CGlowFlash.");
                fixture.Glows.Remove(protectedGlow);
                if (fixture.Scheduler.Count != 0) throw new InvalidDataException("Explicit CGlow owner deletion failed to detach its actual shared source bit.");
                fixture.Shatter.LowMemory = true; fixture.RandomRanges.Clear(); fixture.Effects.CreateCrateExplosion(default, true);
                if (fixture.RandomRanges.Count != 0 || fixture.Scheduler.Count != 0)
                    throw new InvalidDataException("Low-memory source crate explosion consumed random values or constructed effects.");
            }
            GD.Print("Verified native crate effects: actual WebSplat CFrag frame0, source Small/Big radii and RNG order, proportional4:3/16:9 projection, shared Flat/Glow moves, protected ownership and separate cleanup.");
        }
        finally { foreach (Fixture fixture in fixtures) fixture.Viewport.Free(); }
    }

    private static void VerifyConstructor(Fixture fixture, int radius)
    {
        int[] expectedRanges = new[] { 20 }.Concat(Enumerable.Range(0, 19).SelectMany(_ => new[] { 161, 80, 161, 30 }))
            .Concat(Enumerable.Repeat(0, 10)).ToArray();
        if (!fixture.RandomRanges.SequenceEqual(expectedRanges) || fixture.Fragments.Count != 19 || fixture.Glows.Count != 1 ||
            fixture.Scheduler.Count != 20 || fixture.Fragments.GlobalTransform != Transform3D.Identity ||
            fixture.Fragments.SourceFragmentOrder.Any(fragment => fragment.Position != default || fragment.Velocity != new FixedVector3(80 << 12, -79 * 4096, 80 << 12) ||
                fragment.Lifetime != 29 || fragment.Scale != 750 || fragment.CodeRgb != 0x2E808080 || fragment.FrigDeltaZ != 0) ||
            fixture.Glows.SourceGlowOrder.Single() is not GlowFlashState flash || flash.CentreFadeRate != 2 || flash.SectionFadeRate != 20 ||
            flash.Sections.Count != 10 || flash.Sections.Any(section => section.Radius != radius || section.PadBgr != 0x00C8F0) ||
            flash.Fringes.Any(fringe => fringe.Width != 100 || fringe.CodeBgr != 0x3A000000))
            throw new InvalidDataException("Native crate explosion did not construct the original CFrag and CGlowFlash leaves.");
    }

    private static void VerifyGeometry(Fixture fixture)
    {
        fixture.Fragments.Rebuild(); fixture.Glows.Rebuild();
        if (fixture.Fragments.RenderMesh.GetSurfaceCount() != 19 || fixture.Glows.RenderMesh.GetSurfaceCount() != 1)
            throw new InvalidDataException("Actual crate explosion leaves lost visible native geometry.");
        for (int surface = 0; surface < 19; surface++)
        {
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(fixture.Fragments.RenderMesh.GetRid(), surface);
            if (arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length != 6 ||
                fixture.Fragments.RenderMesh.SurfaceGetMaterial(surface) is not StandardMaterial3D material ||
                material.AlbedoTexture != fixture.Fragments.SourceTexture || material.BlendMode != BaseMaterial3D.BlendModeEnum.Add ||
                material.DepthDrawMode != BaseMaterial3D.DepthDrawModeEnum.Disabled || Math.Abs(material.AlbedoColor.A - 128 / 255f) > .000001f)
                throw new InvalidDataException("Native CFrag lost its original textured, additive128 single quad.");
        }
    }

    private static Vector2 SpriteSize(Fixture fixture)
    {
        using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(fixture.Fragments.RenderMesh.GetRid(), 0);
        Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        Vector2 a = fixture.Camera.UnprojectPosition(vertices[0]), b = fixture.Camera.UnprojectPosition(vertices[1]), c = fixture.Camera.UnprojectPosition(vertices[2]);
        return new(b.X - a.X, c.Y - a.Y);
    }
}
