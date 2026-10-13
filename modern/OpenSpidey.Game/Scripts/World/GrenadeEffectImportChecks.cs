using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.World;

internal static class GrenadeEffectImportChecks
{
    public static async Task VerifyAsync(Node3D owner, byte[] originalExpgrndBytes)
    {
        if (Convert.ToHexStringLower(SHA256.HashData(originalExpgrndBytes)) != "6a07961ac2c73a8e30e1d9bb90b64e12799bcc77b3355a5dbcf9240d9b26ce24")
            throw new InvalidDataException("Grenade effects require the original expgrnd.psx geometry and textures.");
        PsxScene scene = PsxSceneReader.Read(originalExpgrndBytes);
        if (scene.Models.Count != 3 || scene.Models.Take(2).Any(model => model.Vertices.Count != 32 || model.Faces.Count != 36 || model.Flags != 8))
            throw new InvalidDataException("Original expgrnd model frames changed.");
        VerifyProjectionGold();
        Camera3D? previous = owner.GetViewport().GetCamera3D();
        var camera = new Camera3D { Name = "GrenadeEffectCheckCamera", TopLevel = true, Current = true, Near = 0.01f, Far = 1000,
            KeepAspect = Camera3D.KeepAspectEnum.Height, Fov = (float)CameraProjection.VerticalFieldOfView(CameraProjection.InitialZoom) };
        owner.AddChild(camera);
        camera.GlobalPosition = new(0, 0, 10); camera.LookAt(Vector3.Zero); camera.MakeCurrent();
        var shifted = new Node3D { Position = new(37, 81, -22) };
        owner.AddChild(shifted);
        var glow = new GodotGlowFlash();
        shifted.AddChild(glow);
        var models = new List<GodotExplosionModel>();
        try
        {
            foreach (int index in new[] { 0, 1 })
            {
                var state = new ExplosionModelState(new(default, "expgrnd", index, 0, index == 0 ? 300 : 0,
                    index == 0 ? 789 : 500, 256, 0, index == 0 ? 13 : 7, 0, 0), _ => 0);
                GodotExplosionModel model = GodotExplosionModel.Create(originalExpgrndBytes, state);
                models.Add(model); shifted.AddChild(model);
                if (model.GlobalPosition != Vector3.Zero) throw new InvalidDataException("Source explosion inherited its scene parent's offset.");
                VerifyModel(model, scene, index, PsxTextureReader.Read(originalExpgrndBytes));
            }
            var flash = glow.Emit(GlowFlashParameters.FromSourceArguments(default,
                new[] { 5, 255, 255, 255, 1, 128, 100, 0, 1, 60, 0, 1, 100, 120, 80, 100, 10, 4 }), _ => 0);
            glow.Rebuild();
            if (glow.GlobalTransform != Transform3D.Identity || glow.RenderMesh.GetSurfaceCount() != 1 ||
                glow.RenderMaterial.BlendMode != BaseMaterial3D.BlendModeEnum.Add || Math.Abs(glow.RenderMaterial.AlbedoColor.A - 160 / 255f) > 0.000001f)
                throw new InvalidDataException("Source glow lost its independent projected geometry or additive0xA0 blend.");
            VerifyNativeProjection(glow, flash, camera);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (flash.Age != 0 || models.Any(model => model.State.GrowthAge != 0))
                throw new InvalidDataException("Native render frames advanced source grenade effect timing.");
            for (int tick = 0; tick < 5; tick++) glow.Advance();
            if (glow.Count != 0 || glow.RenderMesh.GetSurfaceCount() != 0) throw new InvalidDataException("Original lifetime4 flash did not retire at source age5.");
            var persistent = new GlowState(4, 2) { Position = default, SkipTriangles = true, Protected = true };
            persistent.SetFringeWidth(0, 16); persistent.SetFringeWidth(1, 16);
            persistent.SetFringeRgb(0, 100, 50, 0); persistent.SetFringeRgb(1, 0, 0, 0);
            glow.Emit(persistent); glow.Advance();
            if (glow.Count != 1 || glow.RenderMesh.GetSurfaceCount() != 1 || glow.RenderMesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length != 96)
                throw new InvalidDataException("Persistent grenade glow lost its two exact fringe rows or advanced as a flash.");
            glow.Remove(persistent);
            if (glow.Count != 0) throw new InvalidDataException("Grenade glow destructor did not remove its actual shape.");

            // CBody death is not a render-hide operation. Source owner cleanup
            // first skips the dead model, then deletes it on the following update.
            GodotExplosionModel primary = models[0];
            var lifecycle = new ActorLifecycle(false, false);
            while (!primary.State.IsDead) primary.Advance();
            lifecycle.Kill();
            if (!primary.Visible || primary.ResourcesDisposed || !GodotObject.IsInstanceValid(primary))
                throw new InvalidDataException("Source explosion model disappeared before its two-update body deletion.");
            if (lifecycle.PrepareUpdate(0, 0) != ActorUpdateAction.Skip || !primary.Visible ||
                lifecycle.PrepareUpdate(0, 0) != ActorUpdateAction.Delete)
                throw new InvalidDataException("Source explosion death cleanup phase changed.");
            primary.Free();
            if (!primary.ResourcesDisposed || GodotObject.IsInstanceValid(primary))
                throw new InvalidDataException("Deleted source explosion retained GPU resources or a recoverable native object.");
            GD.Print("Verified original expgrnd frames, additive128 model blend, projected glow190 source gold cases, proportional4:3 sizing and exact effect/body lifetimes.");
        }
        finally
        {
            foreach (GodotExplosionModel model in models) if (GodotObject.IsInstanceValid(model)) model.Free();
            glow.Free(); shifted.Free(); camera.Free(); previous?.MakeCurrent();
        }
        if (!glow.ResourcesDisposed) throw new InvalidDataException("Freed source glow retained its mesh/material resources.");
    }

    private static void VerifyProjectionGold()
    {
        string path = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "OpenSpidey.Tests", "Fixtures", "glow-projection.json"));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => unchecked((int)value.GetInt64())).ToArray();
            var shape = new GoldShape(input, test);
            var actual = GodotGlowFlash.Project(shape, input[5], new(input[6], input[7]));
            JsonElement[] expected = test.GetProperty("vertices").EnumerateArray().ToArray();
            if (actual.Count != expected.Length) throw new InvalidDataException($"Source glow projection {index}: triangle vertex count differs ({actual.Count}/{expected.Length}).");
            for (int vertex = 0; vertex < actual.Count; vertex++)
            {
                long[] gold = expected[vertex].EnumerateArray().Select(value => value.GetInt64()).ToArray();
                uint packed = (uint)gold[2];
                Color colour = new(((packed >> 16) & 255) / 255f, ((packed >> 8) & 255) / 255f, (packed & 255) / 255f);
                if (actual[vertex].LogicalPosition != new Vector2(gold[0], gold[1]) || !actual[vertex].Colour.IsEqualApprox(colour) || (packed >> 24) != 160)
                    throw new InvalidDataException($"Source glow projection {index}, vertex{vertex}: original radial point/quad split/fade/PCcolour bias differs.");
            }
            index++;
        }
        Vector2 normal = GodotGlowFlash.ToViewport(new(300, 120), new(640, 480));
        Vector2 wide = GodotGlowFlash.ToViewport(new(300, 120), new(1280, 720));
        if (Math.Abs((wide.X - 640) / (normal.X - 320) - 1.5f) > 0.00001f || wide.Y != 360)
            throw new InvalidDataException("Widescreen stretched source glow sizing rather than scaling the4:3 baseline with viewport height.");
    }

    private static void VerifyNativeProjection(GodotGlowFlash effect, IGlowShape shape, Camera3D camera)
    {
        Vector2 viewport = camera.GetViewport().GetVisibleRect().Size;
        var projected = GodotGlowFlash.Project(shape, 1000, new(256, 120));
        Godot.Collections.Array arrays = effect.RenderMesh.SurfaceGetArrays(0);
        Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        Color[] colours = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
        if (vertices.Length != projected.Count) throw new InvalidDataException("Native glow mesh lost a source cap/fringe triangle.");
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector2 expected = GodotGlowFlash.ToViewport(projected[i].LogicalPosition, viewport);
            if (camera.UnprojectPosition(vertices[i]).DistanceTo(expected) > 0.002f || !colours[i].IsEqualApprox(projected[i].Colour))
                throw new InvalidDataException("Native glow geometry/colour differs from source screen projection.");
        }
    }

    private static void VerifyModel(GodotExplosionModel effect, PsxScene scene, int index, IReadOnlyList<DecodedTexture> decoded)
    {
        PsxModel model = scene.Models[index];
        var groups = model.Faces.GroupBy(face => (face.TextureChecksum, Mode: (face.Flags & 0x40) == 0 ? 0 : (face.Flags & 0x80) != 0 ? 2 : 1)).ToArray();
        if (effect.RenderMesh.GetSurfaceCount() != groups.Length) throw new InvalidDataException("Source explosion face/texture batches differ.");
        for (int surface = 0; surface < groups.Length; surface++)
        {
            var material = effect.Materials[surface];
            if (groups[surface].Key.Mode != 2 || material.BlendMode != BaseMaterial3D.BlendModeEnum.Add ||
                material.ShadingMode != BaseMaterial3D.ShadingModeEnum.Unshaded || Math.Abs(material.AlbedoColor.A - 128 / 255f) > 0.000001f)
                throw new InvalidDataException("Source explosion lost its ordinary DC unlit additive128 material.");
            var expected = new List<Vector3>();
            foreach (PsxFace face in groups[surface])
                foreach (int corner in face.IsTriangle ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 2, 1, 3 })
                {
                    PsxVertex vertex = model.Vertices[face.VertexIndices[corner]];
                    float divisor = (vertex.Flags & 0x10) != 0 ? 800 : 100;
                    expected.Add(new(vertex.Position.X / divisor, -vertex.Position.Y / divisor, -vertex.Position.Z / divisor));
                }
            Vector3[] actual = effect.RenderMesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            if (actual.Length != expected.Count || actual.Where((point, corner) => !point.IsEqualApprox(expected[corner])).Any())
                throw new InvalidDataException($"Source expgrnd model{index} substituted or reordered original PSX polygon corners.");
            if (groups[surface].Key.TextureChecksum is uint checksum)
            {
                DecodedTexture source = decoded.Single(texture => texture.Checksum == checksum);
                using Image image = material.AlbedoTexture!.GetImage();
                if (image.GetWidth() != source.Width || image.GetHeight() != source.Height || !image.GetData().SequenceEqual(source.RgbaPixels))
                    throw new InvalidDataException("Native explosion texture differs from its original decoded PSX pixels.");
            }
        }
    }

    private sealed class GoldShape : IGlowShape
    {
        public FixedVector3 Position => default;
        public IReadOnlyList<GlowSection> Sections { get; }
        public IReadOnlyList<GlowFringe> Fringes { get; }
        public uint CentreCodeBgr { get; }
        public short StepAngle { get; }
        public ushort Angle { get; }
        public uint Mask { get; }
        public bool SkipTriangles { get; }
        public int FringeCount { get; }
        public GoldShape(int[] input, JsonElement test)
        {
            FringeCount = input[1]; Angle = (ushort)input[2]; Mask = (uint)input[3]; SkipTriangles = input[4] != 0;
            CentreCodeBgr = (uint)input[8]; StepAngle = (short)(4096 / (input[0] * 2));
            Sections = test.GetProperty("sections").EnumerateArray().Select(row => { uint[] values = row.EnumerateArray().Select(value => value.GetUInt32()).ToArray(); return new GlowSection(values[0], values[1]); }).ToArray();
            Fringes = test.GetProperty("fringes").EnumerateArray().Select(row => { uint[] values = row.EnumerateArray().Select(value => value.GetUInt32()).ToArray(); return new GlowFringe(values[0], values[1]); }).ToArray();
        }
    }
}
