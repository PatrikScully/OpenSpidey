using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Effects;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.World;

public static class PlayerCombatImpactImportChecks
{
    public static async Task VerifyAsync(Node3D owner, byte[] originalBits)
    {
        var viewport = new SubViewport { Size = new(640, 480), OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            var shifted = new Node3D { Position = new(37, 81, -22) }; viewport.AddChild(shifted);
            var camera = new Camera3D { Current = true, KeepAspect = Camera3D.KeepAspectEnum.Height,
                Fov = (float)CameraProjection.VerticalFieldOfView(CameraProjection.InitialZoom) };
            viewport.AddChild(camera); camera.GlobalPosition = new(0, 0, 10); camera.LookAt(Vector3.Zero); camera.MakeCurrent();
            for (int kind = 0; kind < 3; kind++)
            {
                uint clock = 100;
                var calls = new List<int>();
                var effects = GodotPlayerCombatImpacts.Create(originalBits, () => clock,
                    bound => { calls.Add(bound); return 0; },
                    _ => throw new InvalidOperationException("Inactive source FireWebbing must not query rock ground height."),
                    (_, _, _, _) => throw new InvalidOperationException("Inactive source FireWebbing must not invoke its required rock factory."));
                shifted.AddChild(effects);
                try
                {
                    effects.Emit(new(0, 5, 100), default, kind);
                    int particles = kind == 0 ? 6 : 12;
                    if (effects.Rings.SourceFrame != new SpriteAnimationFrame(-16, -16, 32, 32, 0xC11FEECC) ||
                        effects.Rings.SourceTexture.GetWidth() != 32 || effects.Rings.SourceTexture.GetHeight() != 32 ||
                        effects.Rings.GlobalTransform != Transform3D.Identity || effects.Lines.GlobalTransform != Transform3D.Identity)
                        throw new InvalidDataException("Player impacts must use actual HITSPRIT frame8 and world-space source geometry under a shifted native owner.");
                    int[] expectedCalls = [4096, 4096, ..Enumerable.Range(0, particles).SelectMany(_ => new[] { 3, 4096, 4096, 4096, 7 })];
                    if (!calls.SequenceEqual(expectedCalls) || effects.Rings.Count != 2 || effects.Lines.Count != particles)
                        throw new InvalidDataException("Native player impact changed ring/particle creation or its five exact source RNG boundaries.");
                    ThugCombatImpactRing small = effects.Rings.Rings[0], large = effects.Rings.Rings[1];
                    if (large.CodeRgb != (kind == 2 ? 0x2E19196Cu : 0x2E12126Cu) || small.CodeRgb != (kind == 2 ? 0x2E6565A2u : 0x2E484890u) ||
                        large.Scale != (kind == 2 ? 512 : 384) || small.Scale != (kind == 2 ? 256 : 192) ||
                        large.MaximumScale != (kind == 0 ? 1536 : 1792) || small.MaximumScale != (kind == 0 ? 768 : 896))
                        throw new InvalidDataException("Player source red ring banks/scales were substituted with thug parameters.");
                    if (effects.Lines.RenderMaterial.BlendMode != BaseMaterial3D.BlendModeEnum.Add ||
                        !effects.Lines.RenderMaterial.AlbedoColor.IsEqualApprox(new(1, 1, 1, 128 / 255f)) ||
                        effects.Lines.RenderMesh.GetSurfaceCount() != particles)
                        throw new InvalidDataException("Original additive128-alpha GLine birth dots must remain visible through zero-length PC quad fallback.");
                    VerifyLineMesh(effects.Lines, camera, particles, true);
                    await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                    await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (large.LastClock != 100 || effects.Lines.Motions.Any(motion => motion.LastClock != 100))
                        throw new InvalidDataException("Native render frames advanced source impact lifetimes.");
                    clock = 102;
                    effects.Advance(clock);
                    if (effects.Lines.Motions.Any(motion => motion.RemainingTicks != 10 || motion.Head != new OpenSpidey.Core.Mathematics.FixedVector3(-32768, -32768, -32768)))
                        throw new InvalidDataException("Native player line particle did not preserve source zero-gravity velocity and clock lifetime.");
                    VerifyLineMesh(effects.Lines, camera, particles, false);
                    clock = 112; effects.Advance(clock);
                    if (effects.Lines.Count != particles || effects.Lines.Motions.Any(motion => motion.RemainingTicks != 0))
                        throw new InvalidDataException("Player particles must survive their exact final source clock.");
                    clock = 114; effects.Advance(clock);
                    if (effects.Lines.Count != 0 || effects.Lines.RenderMesh.GetSurfaceCount() != 0 || effects.Rings.Count != 0)
                        throw new InvalidDataException("Player rings/line particles retained geometry after original equality/strict lifetime deletion.");
                }
                finally { effects.Free(); }
            }
            GD.Print("Verified actual player HITSPRIT red ring banks, exact RNG, additive projected2pixel caps/zero-length dots, PC endpoint colours, source clock and independent native cleanup.");
        }
        finally { viewport.Free(); }
    }

    private static void VerifyLineMesh(GodotPlayerImpactLines lines, Camera3D camera, int particles, bool birth)
    {
        if (lines.RenderMesh.GetSurfaceCount() != particles) throw new InvalidDataException("Native impact omitted a valid source GLine particle.");
        for (int surface = 0; surface < particles; surface++)
        {
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(lines.RenderMesh.GetRid(), surface);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Color[] colours = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
            if (vertices.Length != 6 || !colours[0].IsEqualApprox(new(190 / 255f, 30 / 255f, 30 / 255f)) ||
                !colours[1].IsEqualApprox(new(62 / 255f, 30 / 255f, 30 / 255f)))
                throw new InvalidDataException("PC source line quad lost its six vertices or gamma-prebias/brightness endpoint gradient.");
            if (birth)
            {
                Vector2[] projected = vertices.Select(camera.UnprojectPosition).ToArray();
                float width = projected.Max(point => point.X) - projected.Min(point => point.X);
                float height = projected.Max(point => point.Y) - projected.Min(point => point.Y);
                if (Math.Abs(width - 2) > .001f || Math.Abs(height - 2) > .001f)
                    throw new InvalidDataException("PCGfx_DrawLine's zero-length fallback must produce an actual2pixel square.");
            }
        }
    }
}
