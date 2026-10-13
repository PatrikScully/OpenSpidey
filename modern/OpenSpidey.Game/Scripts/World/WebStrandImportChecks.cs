using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.World;

internal static class WebStrandImportChecks
{
    private sealed class Host(GodotWebStrands renderer) : IWebStrandOwnerHost
    {
        public object PlayerIdentity { get; } = new();
        public int PlayerWebShot => 1;
        public int Attached, Detached;
        public PlayerKickHandle StoreTarget(IWebStrandTarget? target)
        {
            if (target is not null) throw new InvalidOperationException("Native geometric strand fixture excludes trap target actors.");
            return default;
        }
        public IWebStrandTarget? RecoverTarget(ref PlayerKickHandle handle)
        {
            if (handle.Target is not null) throw new InvalidOperationException("Native strand fixture unexpectedly recovered an actor handle.");
            return null;
        }
        public void AttachOwner(WebStrandOwner owner) { Attached++; renderer.Attach(owner); }
        public void DetachOwner(WebStrandOwner owner) { Detached++; renderer.Detach(owner); }
        public void EmitKnottedSplat(FixedVector3 position, ActorAngles normal, int fireVariant, Func<int, int> random) =>
            throw new InvalidOperationException("Native fixture verifies no-target strands; actual splats are a separate service.");
        public void ReplaceYankContact(IWebStrandTarget target, int strandCount) =>
            throw new InvalidOperationException("Native fixture excludes unported trap actor contacts.");
    }
    private sealed record Fixture(SubViewport View, Camera3D Camera, GodotWebStrands Renderer, WebStrandOwner Owner, Host Host);
    public static async Task VerifyAsync(Node3D owner, byte[] bitsBytes)
    {
        var fixtures = new List<Fixture>();
        try
        {
            foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
            {
                var viewport = new SubViewport { Size = size, OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                owner.AddChild(viewport);
                var shifted = new Node3D { Position = new(37, 81, -22) }; viewport.AddChild(shifted);
                var renderer = GodotWebStrands.Create(bitsBytes); shifted.AddChild(renderer);
                var camera = new Camera3D { Current = true, Near = .01f, Far = 1000,
                    KeepAspect = Camera3D.KeepAspectEnum.Height,
                    Fov = (float)CameraProjection.VerticalFieldOfView(CameraProjection.InitialZoom) };
                shifted.AddChild(camera); camera.GlobalPosition = new(0, 0, 10); camera.LookAt(Vector3.Zero); camera.MakeCurrent();
                if (renderer.SourceFrame != new SpriteAnimationFrame(-12, -12, 24, 24, 0x65C9F516))
                    throw new InvalidDataException("Knotted strands lost the original WebKnot frame0 packet/texture index15.");
                var host = new Host(renderer); var state = new WebStrandOwner(host);
                var bounds = new List<int>(); int Random(int bound) { bounds.Add(bound); return 0; }
                state.Fire(new(120 << 12, 0, 0), new(-120 << 12, 0, 0), null, false, default, new(0, 0, -1000), Random);
                if (state.Strand?.SegmentCount != 3 || !bounds.SequenceEqual(Enumerable.Repeat(new[] { 100, 4096, 192, 19 }, 3).SelectMany(v => v)))
                    throw new InvalidDataException("Original distance/80 segmentation or constructor four-RNG-per-knot order changed.");
                renderer.AdvanceBits(2, Random, UnexpectedFire);
                if (!bounds.Skip(12).SequenceEqual(Enumerable.Repeat(new[] { 256, 19 }, 3).SelectMany(v => v)))
                    throw new InvalidDataException("Forward-shot knot flicker failed its original shared bit-phase RNG calls.");
                if (renderer.GlobalTransform != Transform3D.Identity || renderer.LineMesh.GetSurfaceCount() != 9 || renderer.KnotMesh.GetSurfaceCount() != 3)
                    throw new InvalidDataException("Knotted strand lost actual base3/inner6 line quads, three protected sprites or world-space ownership.");
                fixtures.Add(new(viewport, camera, renderer, state, host));
            }
            VerifyProportions(fixtures[0], fixtures[1]);
            foreach (Fixture fixture in fixtures) fixture.Owner.SwitchToBlob();
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (fixtures.Any(f => f.Owner.Age != 0 || f.Owner.Mode != 3))
                throw new InvalidDataException("Native render frames advanced CWeb blob contraction.");
            for (int update = 1; update <= 12; update++)
            {
                foreach (Fixture fixture in fixtures)
                {
                    fixture.Renderer.AdvanceActors((uint)(update * 2), new(0, 0, -1000));
                    fixture.Renderer.AdvanceBits((uint)(2 + update * 2), _ => 0, UnexpectedFire);
                    if (update <= 8 && (fixture.Owner.Strand is null || fixture.Owner.Age != update * 2))
                        throw new InvalidDataException("CWeb blob strand retired before the original strict >16 source tick boundary.");
                    if (update == 9 && (fixture.Owner.Mode != 4 || fixture.Owner.Strand is not null || fixture.Renderer.LineMesh.GetSurfaceCount() != 0 || fixture.Renderer.KnotMesh.GetSurfaceCount() != 0))
                        throw new InvalidDataException("CWeb contraction failed to delete all protected child visuals at source age18.");
                    if (update == 10 && fixture.Owner.Phase != ActorLifePhase.Dead)
                        throw new InvalidDataException("CWeb skipped its original mode4 death before the next player-list walk.");
                    if (update == 11 && (fixture.Renderer.Count != 0 || fixture.Host.Attached != 1 || fixture.Host.Detached != 1))
                        throw new InvalidDataException("CWeb destructor failed actual renderer/list detach on the next direct player-list walk after source Body40 was set.");
                }
            }
            GD.Print("PASS: genuine ordinary CWeb/CKnottedWeb lines, WebKnot frame, source RNG/phase retirement and proportional widescreen sprites.");
        }
        finally { foreach (Fixture fixture in fixtures) { owner.RemoveChild(fixture.View); fixture.View.Free(); } }
        static void UnexpectedFire(FixedVector3 position, int scale, Func<int, int> random) =>
            throw new InvalidOperationException("Ordinary strand fixture must not substitute fire-web relocation effects.");
    }
    private static void VerifyProportions(Fixture original, Fixture wide)
    {
        static Vector2 Dimensions(Fixture f)
        {
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(f.Renderer.KnotMesh.GetRid(), 0);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Vector2[] screen = vertices.Select(f.Camera.UnprojectPosition).ToArray();
            return new(screen.Max(p => p.X) - screen.Min(p => p.X), screen.Max(p => p.Y) - screen.Min(p => p.Y));
        }
        Vector2 first = Dimensions(original), second = Dimensions(wide);
        float magnitude = Math.Max(second.X, second.Y), error = (MathF.BitIncrement(magnitude) - magnitude) * 16;
        if (Math.Abs(second.X - first.X * 1.5f) > error || Math.Abs(second.Y - first.Y * 1.5f) > error)
            throw new InvalidDataException("Widescreen stretched original scale176 WebKnot sprites horizontally.");
    }
}
