using Godot;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

internal static class KnottedWebSplatImportChecks
{
    private sealed record Fixture(SubViewport View, Camera3D Camera, GodotKnottedWebSplats Splats, int Variant);

    public static async Task VerifyAsync(Node3D owner, byte[] bitsBytes)
    {
        var fixtures = new List<Fixture>();
        try
        {
            foreach ((Vector2I size, int variant) in new[] { (new Vector2I(640, 480), 0), (new Vector2I(1280, 720), 1) })
            {
                var view = new SubViewport { Size = size, OwnWorld3D = true,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                owner.AddChild(view);
                var shifted = new Node3D { Position = new(37, 81, -22) }; view.AddChild(shifted);
                var splats = GodotKnottedWebSplats.Create(bitsBytes); shifted.AddChild(splats);
                var camera = new Camera3D { Current = true, Near = .01f, Far = 1000,
                    KeepAspect = Camera3D.KeepAspectEnum.Height,
                    Fov = (float)CameraProjection.VerticalFieldOfView(CameraProjection.InitialZoom) };
                shifted.AddChild(camera); camera.GlobalPosition = new(0, 0, 10); camera.LookAt(Vector3.Zero); camera.MakeCurrent();
                var bounds = new List<int>();
                splats.Emit(default, new ActorAngles(0, 0, -4096), variant, bound => { bounds.Add(bound); return 0; });
                if (!bounds.SequenceEqual(new[] { 4096 }) || splats.GlobalTransform != Transform3D.Identity ||
                    splats.Splats[0].Age != 1 || splats.Splats[0].Scale != 16 ||
                    splats.Splats[0].ColourTint != (variant == 0 ? 0x808080u : 0x007BFFu))
                    throw new InvalidDataException("Knotted surface blob changed original roll RNG, constructor Move or ordinary/fire tint.");
                using Image uploaded = splats.SourceTexture.GetImage();
                DecodedTexture decoded = PsxTextureReader.Read(bitsBytes).Single(texture => texture.Checksum == WebImpactSplat.TextureChecksum);
                if (!uploaded.GetData().SequenceEqual(decoded.RgbaPixels))
                    throw new InvalidDataException("Knotted web surface blob lost original3AF6DFF3 texture pixels.");
                fixtures.Add(new(view, camera, splats, variant));
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (fixtures.Any(fixture => fixture.Splats.Splats[0].Age != 1))
                throw new InvalidDataException("Native rendering advanced source knotted splat simulation.");
            VerifyProportions(fixtures[0], fixtures[1]);
            foreach (Fixture fixture in fixtures)
            {
                using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(fixture.Splats.RenderMesh.GetRid(), 0);
                Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                int[] corners = [0, 1, 2, 1, 3, 2];
                for (int index = 0; index < vertices.Length; index++)
                    if (vertices[index] != PsxCoordinates.FixedWorldPosition(fixture.Splats.Splats[0].Corners[corners[index]]))
                        throw new InvalidDataException("Knotted splat renderer changed the actual source oriented quad.");
                if (vertices.Length != 6 || fixture.Splats.RenderMaterial.BlendMode != BaseMaterial3D.BlendModeEnum.Add)
                    throw new InvalidDataException("Knotted splat lost its original semi-transparent additive quad.");
            }
            for (int update = 1; update <= 80; update++)
                foreach (Fixture fixture in fixtures)
                {
                    fixture.Splats.AdvanceBits();
                    int end = fixture.Variant == 0 ? 55 : 80;
                    if (update < end && fixture.Splats.Count != 1 || update >= end && fixture.Splats.Count != 0)
                        throw new InvalidDataException("Native ordinary/fire knotted web splat changed original per-channel lifetime.");
                }
            GD.Print("PASS: genuine ordinary/fire knotted surface blobs, original texture/quad/RNG, source growth/fade and widescreen proportions.");
        }
        finally { foreach (Fixture fixture in fixtures) { owner.RemoveChild(fixture.View); fixture.View.Free(); } }
    }

    private static void VerifyProportions(Fixture original, Fixture wide)
    {
        static (Vector2 Minimum, Vector2 Maximum, Vector2 Dimensions) Bounds(Fixture fixture)
        {
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(fixture.Splats.RenderMesh.GetRid(), 0);
            Vector2[] points = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Select(fixture.Camera.UnprojectPosition).ToArray();
            Vector2 minimum = new(points.Min(point => point.X), points.Min(point => point.Y));
            Vector2 maximum = new(points.Max(point => point.X), points.Max(point => point.Y));
            return (minimum, maximum, maximum - minimum);
        }
        var first = Bounds(original);
        var second = Bounds(wide);
        Vector2 expected = first.Dimensions * 1.5f;
        // UnprojectPosition's division and viewport scaling round coordinates near320/640.
        // Their coordinate ULPs survive the later subtraction; the tiny resulting width's ULP alone cannot bound that loss.
        static float Spacing(float value) => Math.Abs(MathF.BitIncrement(value) - value);
        static float Bound(float firstMin, float firstMax, float firstSize, float secondMin, float secondMax,
            float secondSize, float expectedSize) => Spacing(secondMin) + Spacing(secondMax) +
            1.5f * (Spacing(firstMin) + Spacing(firstMax)) + .5f *
            (Spacing(secondSize) + 1.5f * Spacing(firstSize) + Spacing(expectedSize));
        Vector2 error = new(Bound(first.Minimum.X, first.Maximum.X, first.Dimensions.X,
            second.Minimum.X, second.Maximum.X, second.Dimensions.X, expected.X),
            Bound(first.Minimum.Y, first.Maximum.Y, first.Dimensions.Y,
            second.Minimum.Y, second.Maximum.Y, second.Dimensions.Y, expected.Y));
        GD.Print($"Knotted splat projected dimensions:4:3={first.Dimensions},16:9={second.Dimensions},expected={expected},coordinate-rounding bound={error}.");
        if (Math.Abs(second.Dimensions.X - expected.X) > error.X || Math.Abs(second.Dimensions.Y - expected.Y) > error.Y)
            throw new InvalidDataException("Widescreen distorted an original knotted surface web splat.");
    }
}
