using Godot;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.World;

internal static class FlamingWebImportChecks
{
    private sealed class GeometryHost : IGodotFlamingWebCollisionHost
    {
        private readonly object body = new();
        public int Queries, Hits, Sounds, GroundQueries;
        public ImpactWebEnvironmentContact? TraceInitialEnvironment(FixedVector3 start, FixedVector3 end) => null;
        public object? CheckObjectCollision(FixedVector3 start, FixedVector3 end) => ++Queries == 3 ? body : null;
        public void HitBody(object identity, ActorHit hit)
        {
            if (!ReferenceEquals(identity, body) || hit.Flags != 14 || hit.AttackKind != 7 || hit.Damage != 50 ||
                hit.Direction != new FixedVector3(0, 0, -4096) || hit.SlideDistance != 0 || hit.SlideFrames != 0)
                throw new InvalidDataException("Flaming viewport proof changed the actual written source hit fields or fabricated uninitialized slide values.");
            Hits++;
        }
        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch)
        {
            if (sound != 16 || position != new FixedVector3(0, 0, -(192 << 12)) || pitch != 0)
                throw new InvalidDataException("Flaming source impact sound lost its actual overshot projectile position.");
            Sounds++;
        }
        public void FlickEligibleSwitch(object identity) => throw new InvalidOperationException("Private viewport fixture has no environmental switch.");
        public int GetGroundY(FixedVector3 position)
        {
            if (position != new FixedVector3(0, -(100 << 12), -(192 << 12)))
                throw new InvalidDataException("Flaming spark ground query lost the actual body-hit normal100 offset.");
            GroundQueries++; return unchecked(position.Y + 0x1388000);
        }
    }
    private sealed record Fixture(SubViewport View, Camera3D Camera, GodotFlamingImpactWebs Effects,
        GeometryHost Host, FlamingImpactWeb Shot);

    public static async Task VerifyAsync(Node3D owner, byte[] fireBitsBytes)
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
                var host = new GeometryHost(); var effects = GodotFlamingImpactWebs.Create(fireBitsBytes, host, _ => 0);
                shifted.AddChild(effects);
                var camera = new Camera3D { Current = true, Near = .01f, Far = 1000,
                    KeepAspect = Camera3D.KeepAspectEnum.Height,
                    Fov = (float)CameraProjection.VerticalFieldOfView(CameraProjection.InitialZoom) };
                shifted.AddChild(camera); camera.GlobalPosition = new(0, 0, 10); camera.LookAt(Vector3.Zero); camera.MakeCurrent();
                for (int frame = 0; frame < effects.SourceFrames.Count; frame++)
                {
                    using Image uploaded = effects.SourceTextures[frame].GetImage();
                    DecodedTexture original = PsxTextureReader.Read(fireBitsBytes).First(texture => texture.Checksum == effects.SourceFrames[frame].TextureChecksum);
                    if (!uploaded.GetData().SequenceEqual(original.RgbaPixels))
                        throw new InvalidDataException("Flaming impact web lost its genuine fireimp frame pixels.");
                }
                FlamingImpactWeb shot = effects.Emit(default, new(0, 0, 0), 50, 100);
                if (effects.GlobalTransform != Transform3D.Identity || effects.FlatMesh.GetSurfaceCount() != 0 || shot.Scale != 0)
                    throw new InvalidDataException("Flaming web lost world-space ownership or source scale-zero creation.");
                fixtures.Add(new(view, camera, effects, host, shot));
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (fixtures.Any(fixture => fixture.Shot.Scale != 0 || fixture.Host.Queries != 0))
                throw new InvalidDataException("Rendering advanced flaming source bit simulation.");
            for (uint update = 1; update <= 29; update++)
            {
                foreach (Fixture fixture in fixtures)
                {
                    fixture.Effects.AdvanceBits(100 + update * 2);
                    if (update == 1 && (fixture.Shot.Scale != 1000 || fixture.Effects.ProjectileCount != 1 ||
                        fixture.Effects.TrailCount != 1 || fixture.Effects.Trails[0].Scale != 0 || fixture.Effects.FlatMesh.GetSurfaceCount() != 1))
                        throw new InvalidDataException("Flaming source scale/blur-before-flight failed or newborn blur moved during its creation pass.");
                    if (update == 2 && fixture.Effects.TrailCount != 0)
                        throw new InvalidDataException("Source zero-scale first blur survived its next captured flat-list visit.");
                    if (update == 3)
                    {
                        if (fixture.Effects.ProjectileCount != 0 || fixture.Effects.TrailCount != 1 ||
                            fixture.Effects.Trails[0].Scale != 1000 || fixture.Effects.Trails[0].Age != 0 ||
                            fixture.Effects.SparkCount != 20 || fixture.Effects.PixelMesh.GetSurfaceCount() != 20 ||
                            fixture.Host.Hits != 1 || fixture.Host.Sounds != 1 || fixture.Host.GroundQueries != 1)
                            throw new InvalidDataException("Flaming impact failed genuine blur retirement,20spark pixels or original body/sound/ground callbacks.");
                        if (fixture.Effects.Sparks.Any(spark => spark.Red != 255 || spark.Green != 128 || spark.Blue != 0))
                            throw new InvalidDataException("New fire sparks moved during creation despite actual PC PixelList preceding FlatBitList.");
                        VerifyPixelColour(fixture, new(1, 1, 0));
                    }
                    if (update == 4 && (fixture.Effects.Trails[0].Scale != 900 || fixture.Effects.Trails[0].Age != 1 ||
                        fixture.Effects.Trails[0].Position != new FixedVector3(0, 0, -(144 << 12))))
                        throw new InvalidDataException("Actual CMotionBlur changed one-Move velocity/scale decay or its odd-age animation step.");
                    if (update == 4) VerifyPixelColour(fixture, new(1, 236 / 255f, 0));
                    if (update == 10 && fixture.Effects.TrailCount != 0 || update == 29 &&
                        (fixture.Effects.SparkCount != 0 || fixture.Effects.FlatMesh.GetSurfaceCount() != 0 || fixture.Effects.PixelMesh.GetSurfaceCount() != 0))
                        throw new InvalidDataException("Flaming blur/spark source colour death retained native effects.");
                }
                if (update == 1) VerifyProportions(fixtures[0], fixtures[1], pixel: false);
                if (update == 3) VerifyProportions(fixtures[0], fixtures[1], pixel: true);
            }
            GD.Print("PASS: actual fireimp projectile/CMotionBlur frames, source flat-list ordering, next-pass20pixel sparks, lifecycle and widescreen geometry.");
        }
        finally { foreach (Fixture fixture in fixtures) { owner.RemoveChild(fixture.View); fixture.View.Free(); } }
    }

    private static void VerifyPixelColour(Fixture fixture, Color expected)
    {
        using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(fixture.Effects.PixelMesh.GetRid(), 0);
        Color[] colours = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
        if (colours.Length != 6 || colours.Any(colour => colour != expected))
            throw new InvalidDataException("Fire sparks incorrectly used flat-quad halve/bias RGB instead of PixelList's raw brightness lookup.");
    }

    private static void VerifyProportions(Fixture original, Fixture wide, bool pixel)
    {
        static (Vector2 Minimum, Vector2 Maximum, Vector2 Dimensions) Bounds(Fixture fixture, bool pixel)
        {
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays((pixel ? fixture.Effects.PixelMesh : fixture.Effects.FlatMesh).GetRid(), 0);
            Vector2[] points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Select(fixture.Camera.UnprojectPosition).ToArray();
            Vector2 min = new(points.Min(point => point.X), points.Min(point => point.Y));
            Vector2 max = new(points.Max(point => point.X), points.Max(point => point.Y));
            return (min, max, max - min);
        }
        var first = Bounds(original, pixel); var second = Bounds(wide, pixel);
        Vector2 expected = first.Dimensions * 1.5f;
        static float Spacing(float value) => Math.Abs(MathF.BitIncrement(value) - value);
        static float Error(float min, float max, float size, float wideMin, float wideMax, float wideSize, float expected) =>
            Spacing(wideMin) + Spacing(wideMax) + 1.5f * (Spacing(min) + Spacing(max)) +
            .5f * (Spacing(wideSize) + 1.5f * Spacing(size) + Spacing(expected));
        Vector2 tolerance = new(Error(first.Minimum.X, first.Maximum.X, first.Dimensions.X, second.Minimum.X,
            second.Maximum.X, second.Dimensions.X, expected.X), Error(first.Minimum.Y, first.Maximum.Y,
            first.Dimensions.Y, second.Minimum.Y, second.Maximum.Y, second.Dimensions.Y, expected.Y));
        if (Math.Abs(second.Dimensions.X - expected.X) > tolerance.X || Math.Abs(second.Dimensions.Y - expected.Y) > tolerance.Y)
            throw new InvalidDataException("Widescreen distorted actual flaming web sprite/pixel geometry.");
    }
}
