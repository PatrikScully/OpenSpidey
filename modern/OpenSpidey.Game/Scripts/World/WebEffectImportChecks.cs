using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

internal static class WebEffectImportChecks
{
    private sealed record Fixture(SubViewport View, Camera3D Camera, GodotWebImpactRings Rings,
        GodotWebSurfaceSplats Splats, GodotWebFragments Fragments);

    public static async Task VerifyAsync(Node3D owner, byte[] bitsBytes, byte[] shellPsxBytes)
    {
        var fixtures = new List<Fixture>();
        try
        {
            foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
            {
                var viewport = new SubViewport { Name = $"OriginalWebEffects{size.X}x{size.Y}", Size = size,
                    OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                owner.AddChild(viewport);
                var shifted = new Node3D { Position = new(37, 81, -22) }; viewport.AddChild(shifted);
                var rings = GodotWebImpactRings.Create(bitsBytes); shifted.AddChild(rings);
                var splats = GodotWebSurfaceSplats.Create(shellPsxBytes); shifted.AddChild(splats);
                var fragments = new GodotWebFragments(); shifted.AddChild(fragments);
                var camera = new Camera3D { Current = true, Near = .01f, Far = 1000,
                    KeepAspect = Camera3D.KeepAspectEnum.Height,
                    Fov = (float)CameraProjection.VerticalFieldOfView(CameraProjection.InitialZoom) };
                shifted.AddChild(camera); camera.GlobalPosition = new(0, 0, 10); camera.LookAt(Vector3.Zero); camera.MakeCurrent();
                var fixture = new Fixture(viewport, camera, rings, splats, fragments); fixtures.Add(fixture);
                if (rings.SourceFrame != new SpriteAnimationFrame(-16, -16, 32, 32, 0xDADEBF0D))
                    throw new InvalidDataException("Impact ring lost original Impactwb frame1 rectangle/texture index25.");
                VerifyPixels(rings.SourceTexture, PsxTextureReader.Read(bitsBytes).Single(t => t.Checksum == 0xDADEBF0D));
                VerifyPixels(splats.SourceTexture, PsxTextureReader.Read(shellPsxBytes).Single(t => t.Checksum == 0x8C21C2C7));
                rings.Emit(new(default, 200, 210, 255, 3216, 316));
                splats.Emit(default, new(0, 0, -4096));
                fragments.Emit(new(0, default, new(0, -40960, 0), new(40960, -40960, 0), default, 25, 1), _ => 0);
                if (rings.GlobalTransform != Transform3D.Identity || splats.GlobalTransform != Transform3D.Identity || fragments.GlobalTransform != Transform3D.Identity)
                    throw new InvalidDataException("Web effects moved with a shifted owning actor instead of retaining source world-space ownership.");
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (fixtures.Any(f => f.Rings.Rings[0].Scale != 0 || f.Splats.Splats[0].Mode != 0 || f.Fragments.Fragments[0].CodeRgb0 != 0x52808080))
                throw new InvalidDataException("Rendering advanced source Bit_Move web-effect lifetimes.");
            for (int update = 1; update <= 40; update++)
            {
                foreach (Fixture f in fixtures)
                {
                    f.Rings.AdvanceBits(); f.Splats.AdvanceBits(); f.Fragments.AdvanceBits();
                    if (update <= 10 && (f.Rings.Count != 1 || f.Rings.Rings[0].Scale != update * 316))
                        throw new InvalidDataException("Impact rings changed original one-Move signed-short growth.");
                    if (update == 11 && (f.Rings.Count != 0 || f.Rings.RenderMesh.GetSurfaceCount() != 0))
                        throw new InvalidDataException("Impact ring survived the source strict greater-than target scale death.");
                    if (update < 40 && f.Splats.Count != 1 || update == 40 && f.Splats.Count != 0)
                        throw new InvalidDataException("CSplat changed its source transition/31-age/eight-fade lifetime.");
                    if (update == 1)
                    {
                        VerifyGeometry(f);
                        if (f.Fragments.RenderMesh.GetSurfaceCount() != 2 || f.Fragments.Fragments[0].ColourDecay != 1)
                            throw new InvalidDataException("Actual CWebFrag failed to draw both owner/companion lines from source decay RNG.");
                    }
                }
                if (update == 1) VerifyProportions(fixtures[0], fixtures[1]);
            }
            // Decay1 requires 128 source bit updates; companion must retire with its owner.
            for (int update = 41; update <= 128; update++) foreach (Fixture f in fixtures) f.Fragments.AdvanceBits();
            if (fixtures.Any(f => f.Fragments.Count != 0 || f.Fragments.RenderMesh.GetSurfaceCount() != 0))
                throw new InvalidDataException("Dead web fragment retained its protected companion line.");
            GD.Print("PASS: original Impactwb/CSplat/CWebFrag texture upload, 4:3/16:9 geometry, bit timing and paired-line cleanup.");
        }
        finally
        {
            foreach (Fixture fixture in fixtures) { owner.RemoveChild(fixture.View); fixture.View.Free(); }
        }
    }

    private static void VerifyPixels(ImageTexture uploaded, DecodedTexture original)
    {
        using Image image = uploaded.GetImage();
        if (image.GetWidth() != original.Width || image.GetHeight() != original.Height || !image.GetData().SequenceEqual(original.RgbaPixels))
            throw new InvalidDataException("Web effects did not upload their actual decoded original texture pixels.");
    }
    private static void VerifyGeometry(Fixture f)
    {
        using Godot.Collections.Array ringArrays = RenderingServer.MeshSurfaceGetArrays(f.Rings.RenderMesh.GetRid(), 0);
        Vector3[] ringVertices = ringArrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        Vector2[] ringUv = ringArrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
        if (ringVertices.Length != 6 || ringUv[0] != new Vector2(.01f, .01f) || ringUv[1] != new Vector2(.99f, .01f))
            throw new InvalidDataException("Impact ring lost its original textured quad/UV inset.");
        using Godot.Collections.Array splatArrays = RenderingServer.MeshSurfaceGetArrays(f.Splats.RenderMesh.GetRid(), 0);
        Vector3[] vertices = splatArrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        int[] order = [0, 1, 2, 1, 3, 2];
        for (int i = 0; i < vertices.Length; i++)
            if (vertices[i] != PsxCoordinates.FixedWorldPosition(f.Splats.Splats[0].Corners[order[i]]))
                throw new InvalidDataException("CSplat changed actual source fifty-unit surface orientation.");
        if (f.Splats.RenderMaterial.BlendMode != BaseMaterial3D.BlendModeEnum.Mix ||
            f.Splats.RenderMaterial.DepthDrawMode != BaseMaterial3D.DepthDrawModeEnum.Disabled)
            throw new InvalidDataException("PC CSplat blend1 was replaced by guessed subtractive rendering/depth writes.");
        Color[] colours = splatArrays[(int)Mesh.ArrayType.Color].AsColorArray();
        if (colours.Any(colour => colour != new Color(52 / 255f, 52 / 255f, 52 / 255f)))
            throw new InvalidDataException("CSplat raw22 tint changed its source DrawQPoly halve/bias/brightness transform.");
    }
    private static void VerifyProportions(Fixture first, Fixture second)
    {
        static Vector2 Dimensions(Fixture f, ImmediateMesh mesh)
        {
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(mesh.GetRid(), 0);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Vector2[] projected = vertices.Select(f.Camera.UnprojectPosition).ToArray();
            return new(projected.Max(p => p.X) - projected.Min(p => p.X), projected.Max(p => p.Y) - projected.Min(p => p.Y));
        }
        foreach (bool ring in new[] { true, false })
        {
            Vector2 a = Dimensions(first, ring ? first.Rings.RenderMesh : first.Splats.RenderMesh);
            Vector2 b = Dimensions(second, ring ? second.Rings.RenderMesh : second.Splats.RenderMesh);
            float error = MathF.BitIncrement(Math.Max(b.X, b.Y)) - Math.Max(b.X, b.Y);
            if (Math.Abs(b.X - a.X * 1.5f) > error * 16 || Math.Abs(b.Y - a.Y * 1.5f) > error * 16)
                throw new InvalidDataException("Widescreen stretched the source web quad instead of preserving height-derived proportions.");
        }
    }
}
