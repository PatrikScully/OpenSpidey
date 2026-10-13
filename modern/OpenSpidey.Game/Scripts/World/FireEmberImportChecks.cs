using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Effects;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.World;

internal static class FireEmberImportChecks
{
    private sealed record Fixture(SubViewport View, Camera3D Camera, GodotFireEmbers Effects,
        FireEmber Ember, SourceParticleRegistration Registration, List<int> Bounds);

    public static async Task VerifyAsync(Node3D owner, byte[] bitsBytes)
    {
        var fixtures = new List<Fixture>();
        try
        {
            SpriteAnimation misc = SpriteAnimationReader.Read(bitsBytes).Single(animation => animation.Name.Equals("Misc", StringComparison.OrdinalIgnoreCase));
            DecodedTexture decoded = PsxTextureReader.Read(bitsBytes).First(texture => texture.Checksum == misc.Frames[5].TextureChecksum);
            byte width = unchecked((byte)(decoded.Width - 1)), height = unchecked((byte)(decoded.Height - 1));
            var sourceRectangle = new SpriteAnimationFrame(unchecked((sbyte)(width / -2)), unchecked((sbyte)(height / -2)),
                width, height, decoded.Checksum);
            foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
            {
                var view = new SubViewport { Size = size, OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                owner.AddChild(view);
                var shifted = new Node3D { Position = new(37, 81, -22) }; view.AddChild(shifted);
                var effects = GodotFireEmbers.Create(bitsBytes); shifted.AddChild(effects);
                var camera = new Camera3D { Current = true, Near = .01f, Far = 1000,
                    KeepAspect = Camera3D.KeepAspectEnum.Height, Fov = (float)CameraProjection.VerticalFieldOfView(CameraProjection.InitialZoom) };
                shifted.AddChild(camera); camera.GlobalPosition = new(0, 0, 10); camera.LookAt(Vector3.Zero); camera.MakeCurrent();
                using Image uploaded = effects.SourceTexture.GetImage();
                if (!uploaded.GetData().SequenceEqual(decoded.RgbaPixels) || effects.SourceFrame != sourceRectangle)
                    throw new InvalidDataException("CEmber lost the actual Misc frame5 texture or CFT4Bit's centered UV-span synthetic rectangle.");
                var bounds = new List<int>(); SourceParticleRegistration? registration = null;
                effects.SourceBitCreated += bit => registration = bit;
                FireEmber ember = effects.Emit(default, 256, bound => { bounds.Add(bound); return bound - 1; });
                if (!bounds.SequenceEqual(new[] { 10, 4096, 4096, 200, 5, 5 }) || registration is null ||
                    !ReferenceEquals(registration.Identity, ember) || !registration.IsAlive() ||
                    effects.GlobalTransform != Transform3D.Identity || ember.Scale != 549 || ember.FadeDelay != 9 ||
                    ember.Velocity.Y != 40960 || ember.CodeRgb != 0x2E0080FF)
                    throw new InvalidDataException("CEmber changed original constructor RNG, scale256 pulse, world ownership or typed Flat identity.");
                fixtures.Add(new(view, camera, effects, ember, registration, bounds));
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            foreach (Fixture fixture in fixtures)
                if (fixture.Ember.FadeDelay != 9 || fixture.Bounds.Count != 6 || fixture.Effects.RenderMesh.GetSurfaceCount() != 1)
                    throw new InvalidDataException("Rendering advanced CEmber's source bit simulation or omitted its native source textured quad.");
            VerifyProportions(fixtures[0], fixtures[1]);
            for (uint update = 1; update <= 26; update++)
                foreach (Fixture fixture in fixtures)
                {
                    fixture.Registration.Move(update); fixture.Effects.Rebuild();
                    if (fixture.Bounds.Count != update + 6 || fixture.Bounds[^1] != 256 ||
                        fixture.Ember.Position.Y != -(int)update * 40960 || fixture.Ember.OrbitPhase != 4095 + (int)update * 100 ||
                        fixture.Ember.AmplitudePhase != 4095 + (int)update * 100)
                        throw new InvalidDataException("CEmber lost the original one-Move rise/spiral or shared flicker RNG.");
                    if (update < 26 && (!fixture.Registration.IsAlive() || fixture.Effects.RenderMesh.GetSurfaceCount() != 1))
                        throw new InvalidDataException("CEmber retired before source delayed RGB15 decay completed.");
                    if (update == 26)
                    {
                        if (fixture.Registration.IsAlive() || fixture.Ember.CodeRgb != 0x2E000000 ||
                            fixture.Effects.RenderMesh.GetSurfaceCount() != 0 || fixture.Effects.Count != 1)
                            throw new InvalidDataException("CEmber lost final dying-update flicker or removed ownership before source cleanup.");
                        fixture.Registration.Destroy();
                        if (fixture.Effects.Count != 0) throw new InvalidDataException("Typed Flat cleanup retained the actual dead ember instance.");
                    }
                }
            GD.Print("PASS: actual CEmber Misc frame5 texture/synthetic rectangle, native typed Flat ownership, full source rise/spiral/RGB death and widescreen.");
        }
        finally { foreach (Fixture fixture in fixtures) { owner.RemoveChild(fixture.View); fixture.View.Free(); } }
    }

    private static void VerifyProportions(Fixture original, Fixture wide)
    {
        static (Vector2 Minimum, Vector2 Maximum, Vector2 Dimensions) Bounds(Fixture fixture)
        {
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(fixture.Effects.RenderMesh.GetRid(), 0);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            if (vertices.Length != 6) throw new InvalidDataException("CEmber source quad does not have its two native triangles.");
            Vector2[] points = vertices.Select(fixture.Camera.UnprojectPosition).ToArray();
            Vector2 min = new(points.Min(point => point.X), points.Min(point => point.Y));
            Vector2 max = new(points.Max(point => point.X), points.Max(point => point.Y));
            return (min, max, max - min);
        }
        var first = Bounds(original); var second = Bounds(wide);
        Vector2 expected = first.Dimensions * 1.5f;
        static float Spacing(float value) => Math.Abs(MathF.BitIncrement(value) - value);
        static float Error(float min, float max, float size, float wideMin, float wideMax, float wideSize, float expected) =>
            Spacing(wideMin) + Spacing(wideMax) + 1.5f * (Spacing(min) + Spacing(max)) +
            .5f * (Spacing(wideSize) + 1.5f * Spacing(size) + Spacing(expected));
        Vector2 tolerance = new(Error(first.Minimum.X, first.Maximum.X, first.Dimensions.X, second.Minimum.X,
            second.Maximum.X, second.Dimensions.X, expected.X), Error(first.Minimum.Y, first.Maximum.Y,
            first.Dimensions.Y, second.Minimum.Y, second.Maximum.Y, second.Dimensions.Y, expected.Y));
        if (Math.Abs(second.Dimensions.X - expected.X) > tolerance.X || Math.Abs(second.Dimensions.Y - expected.Y) > tolerance.Y)
            throw new InvalidDataException("Widescreen distorted the actual CEmber synthetic textured rectangle.");
    }
}
