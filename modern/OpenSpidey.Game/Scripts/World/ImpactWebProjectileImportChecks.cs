using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal static class ImpactWebProjectileImportChecks
{
    // This private viewport proof checks rendering/timing only. The production host is separately verified against a real roof.
    private sealed class GeometryHost(GodotWebImpactRings rings) : IImpactWebHost
    {
        public int InitialQueries, MotionQueries;
        public bool LowMemory => false;
        public int ScaleDamage(int damage) => damage;
        public ImpactWebEnvironmentContact? TraceInitialEnvironment(FixedVector3 start, FixedVector3 end)
        { InitialQueries++; return null; }
        public object? CheckObjectCollision(FixedVector3 start, FixedVector3 end) { MotionQueries++; return null; }
        public void CreateImpactRing(ImpactWebRingRequest request) => rings.Emit(request);
        public void SetPlayerFirstContact() => throw Unexpected();
        public void HitBody(object identity, ActorHit hit) => throw Unexpected();
        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => throw Unexpected();
        public void CreateSplat(FixedVector3 position, ActorAngles normal) => throw Unexpected();
        public void FlickEligibleSwitch(object environmentalIdentity) => throw Unexpected();
        public int TraceGroundY(FixedVector3 start, FixedVector3 end) => throw Unexpected();
        public void CreateWebFragment(ImpactWebFragmentRequest request, Func<int, int> random) => throw Unexpected();
        private static InvalidOperationException Unexpected() => new("Geometry-only web proof unexpectedly used actor/environment hit services.");
    }

    private sealed record Fixture(SubViewport View, Camera3D Camera, GodotImpactWebProjectiles Projectiles,
        GodotWebImpactRings Rings, GeometryHost Host, ImpactWebProjectile Shot);

    public static async Task VerifyGeometryAsync(Node3D owner, byte[] bitsBytes)
    {
        var fixtures = new List<Fixture>();
        try
        {
            foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
            {
                var view = new SubViewport { Size = size, OwnWorld3D = true,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                owner.AddChild(view);
                var shifted = new Node3D { Position = new(37, 81, -22) }; view.AddChild(shifted);
                var rings = GodotWebImpactRings.Create(bitsBytes); shifted.AddChild(rings);
                var host = new GeometryHost(rings);
                var projectiles = GodotImpactWebProjectiles.Create(bitsBytes, host, _ => 0); shifted.AddChild(projectiles);
                var camera = new Camera3D { Current = true, Near = .01f, Far = 1000,
                    KeepAspect = Camera3D.KeepAspectEnum.Height,
                    Fov = (float)CameraProjection.VerticalFieldOfView(CameraProjection.InitialZoom) };
                shifted.AddChild(camera); camera.GlobalPosition = new(0, 0, 10); camera.LookAt(Vector3.Zero); camera.MakeCurrent();
                SpriteAnimation source = SpriteAnimationReader.Read(bitsBytes).Single(animation =>
                    animation.Name.Equals("ImpactWb", StringComparison.OrdinalIgnoreCase));
                if (projectiles.SourceFrame != source.Frames[0] || projectiles.SourceFrame == rings.SourceFrame)
                    throw new InvalidDataException("Impact projectile reused ring frame1 instead of original ImpactWb frame0.");
                using Image uploaded = projectiles.SourceTexture.GetImage();
                DecodedTexture decoded = PsxTextureReader.Read(bitsBytes).Single(texture => texture.Checksum == source.Frames[0].TextureChecksum);
                if (!uploaded.GetData().SequenceEqual(decoded.RgbaPixels))
                    throw new InvalidDataException("Impact projectile failed to upload the exact original frame0 pixels.");
                ImpactWebProjectile shot = projectiles.Emit(new(default, new(0, 0, 0), 32, 50, 12), 100);
                if (projectiles.GlobalTransform != Transform3D.Identity || projectiles.RenderMesh.GetSurfaceCount() != 0 ||
                    shot.Scale != 0 || host.InitialQueries != 1 || host.MotionQueries != 0)
                    throw new InvalidDataException("Impact projectile changed initial scale-zero, initial source ray or world-space ownership.");
                fixtures.Add(new(view, camera, projectiles, rings, host, shot));
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (fixtures.Any(fixture => fixture.Shot.Scale != 0 || fixture.Shot.LastClock != 100))
                throw new InvalidDataException("Native rendering advanced source CImpactWeb bit simulation.");
            for (uint update = 1; update <= 6; update++)
            {
                foreach (Fixture fixture in fixtures)
                {
                    // A ring created at the head is outside the current forward FlatBit traversal.
                    fixture.Rings.AdvanceBits();
                    fixture.Projectiles.AdvanceBits(100 + update * 2);
                    if (fixture.Shot.Position != new FixedVector3(0, 0, unchecked(-(int)update * 64 * 4096)) ||
                        fixture.Shot.Age != update * 2 || fixture.Host.MotionQueries != update)
                        throw new InvalidDataException("Native impact-web flight changed source velocity, clock/age or per-Move collision queries.");
                    if (update < 6 && (fixture.Projectiles.Count != 1 || fixture.Projectiles.RenderMesh.GetSurfaceCount() != 1 || fixture.Rings.Count != 0))
                        throw new InvalidDataException("Native impact-web projectile retired early or failed its actual textured sprite.");
                    if (update == 6 && (fixture.Projectiles.Count != 0 || fixture.Projectiles.RenderMesh.GetSurfaceCount() != 0 ||
                        fixture.Rings.Count != 1 || fixture.Rings.Rings[0].Scale != 0))
                        throw new InvalidDataException("Lifetime impact did not retire its projectile and leave the new source ring unadvanced.");
                }
                if (update == 1) VerifyProportions(fixtures[0], fixtures[1]);
            }
            GD.Print("PASS: original ImpactWb frame0 projectile texture, source clock flight/retirement, next-pass ring growth and proportional widescreen sprites.");
        }
        finally { foreach (Fixture fixture in fixtures) { owner.RemoveChild(fixture.View); fixture.View.Free(); } }
    }

    public static void VerifyRealRoof(GodotPlayer player, GodotImpactWebNativeHost host,
        GodotImpactWebProjectiles projectiles, GodotWebImpactRings rings, GodotWebSurfaceSplats splats, uint clock)
    {
        FixedVector3 start = player.Controller.Position;
        ImpactWebProjectile shot = projectiles.Emit(new(start, new(1024, 0, 0), 32, 50, 120), clock);
        ImpactWebEnvironmentContact contact = shot.EnvironmentContact ??
            throw new InvalidDataException("Production impact-web constructor failed its actual first-roof downward line query.");
        if (contact.Distance <= 0 || contact.Normal.Y >= 0 || shot.Lifetime != contact.Distance / 32)
            throw new InvalidDataException("Production projectile changed original roof normal, ray distance or distance/speed lifetime.");
        int expectedDamage = player.Melee!.Controller.GetDamage(50);
        if (shot.Damage != expectedDamage) throw new InvalidDataException("Native impact-web damage did not use the actual player difficulty/suit.");
        int priorRings = rings.Count, priorSplats = splats.Count;
        for (uint update = 1; update <= 60 && shot.Alive; update++) projectiles.AdvanceBits(clock + update * 2);
        if (shot.Alive || projectiles.Shots.Contains(shot) || rings.Count != priorRings + 1 || splats.Count != priorSplats + 1)
            throw new InvalidDataException("Production impact-web roof contact failed actual splat/ring creation and projectile removal.");
        FixedVector3 impact = contact.Position + new FixedVector3(contact.Normal.X, contact.Normal.Y, contact.Normal.Z) * 10;
        if (rings.Rings[0].Position != impact || rings.Rings[0].Scale != 0 || splats.Splats[0].Mode != 0)
            throw new InvalidDataException("Production world impact double-applied its normal offset or prematurely advanced new source effects.");
        GD.Print($"PASS: live original roof impact-web initial ray, damage{expectedDamage}, source lifetime{shot.Lifetime}, real splat/ring and native cleanup.");
    }

    private static void VerifyProportions(Fixture first, Fixture wide)
    {
        static Vector2 Dimensions(Fixture fixture)
        {
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(fixture.Projectiles.RenderMesh.GetRid(), 0);
            Vector2[] points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Select(fixture.Camera.UnprojectPosition).ToArray();
            return new(points.Max(point => point.X) - points.Min(point => point.X), points.Max(point => point.Y) - points.Min(point => point.Y));
        }
        Vector2 original = Dimensions(first), updated = Dimensions(wide);
        float magnitude = Math.Max(updated.X, updated.Y), error = (MathF.BitIncrement(magnitude) - magnitude) * 16;
        if (Math.Abs(updated.X - original.X * 1.5f) > error || Math.Abs(updated.Y - original.Y * 1.5f) > error)
            throw new InvalidDataException("Widescreen distorted the source impact-web projectile sprite.");
    }
}
