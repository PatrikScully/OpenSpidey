using System.Security.Cryptography;
using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

internal static class ThugCombatRingImportChecks
{
    private sealed record Fixture(SubViewport Viewport, GodotThugCombatRings Effect, Camera3D Camera, FixedVector3 Position);

    public static async Task VerifyAsync(Node3D owner, byte[] bitsBytes, TriggerFile triggers)
    {
        if (Engine.PhysicsTicksPerSecond != 60) throw new InvalidOperationException("Combat ring checks require two60Hz physics frames per source update.");
        var definition = TriggerActorDefinition.Read(triggers.Nodes[9]);
        if (definition.Position != new TriggerPosition(-18937, 1242, -19681)) throw new InvalidDataException("Combat ring checks lost the original L1A1 actor position.");
        FixedVector3 position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12;
        var fixtures = new List<Fixture>();
        var viewports = new List<SubViewport>();
        try
        {
            foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
            {
                var viewport = new SubViewport { Size = size, OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                viewports.Add(viewport); owner.AddChild(viewport);
                var shifted = new Node3D { Position = new(37, 81, -22) }; viewport.AddChild(shifted);
                var effect = GodotThugCombatRings.Create(bitsBytes); shifted.AddChild(effect);
                if (effect.SourceFrame != new SpriteAnimationFrame(-16, -16, 32, 32, 0xC11FEECC)) throw new InvalidDataException("Combat rings changed the original HITSPRIT animation8 frame0 packet.");
                using (Image image = effect.SourceTexture.GetImage())
                    if (image.GetWidth() != 32 || image.GetHeight() != 32 || Convert.ToHexString(SHA256.HashData(image.GetData())).ToLowerInvariant() != "215a2a569538279db015b7aa3b5d39abf3f13317f212f4805abb622982bfb6bb")
                        throw new InvalidDataException("Native HITSPRIT pixels differ from independently extracted original palettes.");
                var camera = new Camera3D { Current = true, KeepAspect = Camera3D.KeepAspectEnum.Height,
                    Fov = (float)OpenSpidey.Core.Cameras.CameraProjection.VerticalFieldOfView(OpenSpidey.Core.Cameras.CameraProjection.InitialZoom) };
                shifted.AddChild(camera); camera.GlobalPosition = PsxCoordinates.FixedWorldPosition(position) + new Vector3(0, 0, 10);
                camera.LookAt(PsxCoordinates.FixedWorldPosition(position)); camera.MakeCurrent();
                effect.Emit(304, position, 100, _ => 0);
                if (effect.GlobalTransform != Transform3D.Identity || effect.Count != 2 || effect.Rings[0].Scale != 192 || effect.Rings[1].Scale != 384 ||
                    effect.Rings[0].CodeRgb != 0x2E904848 || effect.Rings[1].CodeRgb != 0x2E6C1212)
                    throw new InvalidDataException("CThug impact rings lost constructor color/order, initial scales or world-space ownership.");
                var fixture = new Fixture(viewport, effect, camera, position); fixtures.Add(fixture);
                VerifyGeometry(fixture);
            }
            VerifyProportions(fixtures[0], fixtures[1]);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (fixtures.Any(fixture => fixture.Effect.Rings.Any(ring => ring.LastClock != 100) || fixture.Effect.Rings[1].Scale != 384))
                throw new InvalidDataException("Render frames grew source-clock combat rings.");
            for (uint update = 1; update <= 6; update++)
            {
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                foreach (Fixture fixture in fixtures)
                {
                    fixture.Effect.Advance(100 + update * 2);
                    if (update <= 5)
                    {
                        if (fixture.Effect.Count != 2 || fixture.Effect.Rings[0].Scale != Math.Min(896, 192 + 144 * update) ||
                            fixture.Effect.Rings[1].Scale != Math.Min(1792, 384 + 288 * update))
                            throw new InvalidDataException("Native combat rings changed source clock-scaled growth or early equality death.");
                        VerifyGeometry(fixture);
                    }
                    else if (fixture.Effect.Count != 0 || fixture.Effect.RenderMesh.GetSurfaceCount() != 0)
                        throw new InvalidDataException("The source invocation after maximum scale retained combat ring quads.");
                }
                if (update <= 5) VerifyProportions(fixtures[0], fixtures[1]);
            }
            foreach (Fixture fixture in fixtures)
            {
                fixture.Effect.Emit(312, position, 120, _ => 0);
                if (fixture.Effect.Rings[0].CodeRgb != 0x2E489090 || fixture.Effect.Rings[1].CodeRgb != 0x2E126C6C)
                    throw new InvalidDataException("Non304 CThug rings lost their actual alternate color bank.");
                fixture.Effect.Advance(140); // Skip source clocks; both rings clamp but stay alive for this Move.
                if (fixture.Effect.Count != 2) throw new InvalidDataException("A source clock jump killed rings during their clamp update.");
                fixture.Effect.Advance(140);
                if (fixture.Effect.Count != 0) throw new InvalidDataException("Equality death must occur on the next invocation even at the same source clock.");
            }
            GD.Print("Verified native combat rings: original HITSPRIT frame0/palette, source two color banks/order, anisotropic post-scale,4:3/16:9 proportions, source-clock growth, delayed max death and cleanup.");
        }
        finally { foreach (SubViewport viewport in viewports) viewport.Free(); }
    }

    private static void VerifyGeometry(Fixture fixture)
    {
        if (fixture.Effect.RenderMesh.GetSurfaceCount() != 2) throw new InvalidDataException("Valid combat rings lost visible native quads.");
        int[] order = [0, 1, 2, 1, 3, 2];
        Vector2 pixelScale = new(fixture.Viewport.Size.Y / 240f * 0.625f, fixture.Viewport.Size.Y / 240f);
        for (int surface = 0; surface < 2; surface++)
        {
            ThugCombatImpactRing ring = fixture.Effect.Rings[surface];
            Vector3 center = PsxCoordinates.FixedWorldPosition(ring.Position);
            Vector2 logical = fixture.Camera.UnprojectPosition(center) / pixelScale;
            var corners = ThugPingProjection.Project(fixture.Effect.SourceFrame, unchecked((ushort)ring.Scale), ring.Angle, (ring.Position >> 12).Z - (PsxCoordinates.OriginalWorldPosition(fixture.Camera.GlobalPosition) >> 12).Z, (int)logical.X, (int)logical.Y, ring.PostScale)!;
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(fixture.Effect.RenderMesh.GetRid(), surface);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Vector2[] uv = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            if (vertices.Length != 6 || uv.Length != 6 || fixture.Effect.RenderMesh.SurfaceGetMaterial(surface) is not StandardMaterial3D material ||
                material.AlbedoTexture != fixture.Effect.SourceTexture || material.BlendMode != BaseMaterial3D.BlendModeEnum.Add || material.DepthDrawMode != BaseMaterial3D.DepthDrawModeEnum.Disabled)
                throw new InvalidDataException("Combat rings lost original textured single-quad/additive/no-depth-write rendering.");
            for (int vertex = 0; vertex < 6; vertex++)
            {
                int corner = order[vertex];
                Vector2 pixel = (new Vector2(corners[corner].X, corners[corner].Y) + logical - new Vector2((int)logical.X, (int)logical.Y)) * pixelScale;
                Vector3 expected = fixture.Camera.ProjectPosition(pixel, 10);
                float coordinate = Math.Max(Math.Max(Math.Abs(expected.X), Math.Abs(expected.Y)), Math.Abs(expected.Z));
                float error = (MathF.BitIncrement(coordinate) - coordinate) * 4;
                if (vertices[vertex].DistanceTo(expected) > error || uv[vertex] != new Vector2((corner & 1) != 0 ? 0.99f : 0.01f, (corner & 2) != 0 ? 0.99f : 0.01f))
                    throw new InvalidDataException("Combat ring vertices changed source frame/post-scale projection or UV inset.");
            }
        }
    }

    private static void VerifyProportions(Fixture original, Fixture wide)
    {
        static Vector2 Dimensions(Fixture fixture)
        {
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(fixture.Effect.RenderMesh.GetRid(), 1);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Vector2 a = fixture.Camera.UnprojectPosition(vertices[0]), b = fixture.Camera.UnprojectPosition(vertices[1]), c = fixture.Camera.UnprojectPosition(vertices[2]);
            return new(b.X - a.X, c.Y - a.Y);
        }
        Vector2 first = Dimensions(original), second = Dimensions(wide);
        Vector3 source = PsxCoordinates.FixedWorldPosition(original.Position);
        float coordinate = Math.Max(Math.Max(Math.Abs(source.X), Math.Abs(source.Y)), Math.Abs(source.Z));
        float ulp = MathF.BitIncrement(coordinate) - coordinate;
        float error = ulp * 16 * (original.Viewport.Size.Y + wide.Viewport.Size.Y) / (20 * Mathf.Tan(Mathf.DegToRad(original.Camera.Fov) / 2));
        if (Math.Abs(second.X - first.X * 1.5f) > error || Math.Abs(second.Y - first.Y * 1.5f) > error)
            throw new InvalidDataException("16:9 stretched combat rings horizontally instead of retaining their source anisotropic shape.");
    }
}
