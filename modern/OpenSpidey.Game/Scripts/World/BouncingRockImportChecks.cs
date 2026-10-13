using System.Security.Cryptography;
using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Effects;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.World;

public static class BouncingRockImportChecks
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
            var effects = GodotBouncingRocks.Create(originalBits); shifted.AddChild(effects);
            try
            {
                int[] slots = [2, 5]; uint[] checksums = [0xF5A14AFF, 0x8826BCA4]; int[] widths = [16, 32];
                string[] hashes = ["1ccab95607395963df2c9f5bd707c1a5c7b997d341497b45e62445815c44cb2a", "452c6d00fbac59e64264daa09ad7fec53af1b76b34e4bb8cf3abcb9a34b641c5"];
                for (int i = 0; i < 2; i++)
                {
                    int slot = slots[i], width = widths[i];
                    if (effects.SourceFrame(slot) != new SpriteAnimationFrame((sbyte)(-width / 2), (sbyte)(-width / 2), (byte)width, (byte)width, checksums[i]))
                        throw new InvalidDataException("Rock must use the original Misc animation14 texture with a new centered full-texture frame.");
                    using Image image = effects.SourceTexture(slot).GetImage();
                    if (Convert.ToHexString(SHA256.HashData(image.GetData())).ToLowerInvariant() != hashes[i])
                        throw new InvalidDataException("Rock source pixels differ from independent original-PC palette gold.");
                    StandardMaterial3D material = effects.SourceMaterial(slot);
                    if (material.BlendMode != BaseMaterial3D.BlendModeEnum.Mix || material.Transparency != BaseMaterial3D.TransparencyEnum.AlphaScissor || material.AlbedoColor != Colors.White)
                        throw new InvalidDataException("Rock source code2C is opaque keyed geometry, with neutral RGB128 and no additive ring material.");
                }
                effects.Emit(default, 0, BouncingRockState.FireSelector, _ => 0);
                effects.Emit(default, 0, BouncingRockState.DebrisSelector, _ => 0);
                if (effects.GlobalTransform != Transform3D.Identity || effects.Count != 2 || effects.RenderMesh.GetSurfaceCount() != 2)
                    throw new InvalidDataException("Real rock quads must remain source world-space under a shifted owner.");
                for (int surface = 0; surface < 2; surface++)
                {
                    using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(effects.RenderMesh.GetRid(), surface);
                    if (arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length != 6 || arrays[(int)Mesh.ArrayType.Color].AsColorArray().Any(colour => !colour.IsEqualApprox(new(158 / 255f, 158 / 255f, 158 / 255f))))
                        throw new InvalidDataException("Rock birth must draw six textured vertices with source RGB128, PC half/bias79 and brightness158.");
                }
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                if (effects.Rocks.Any(rock => rock.Lifetime != 0)) throw new InvalidDataException("Native render frames must not advance source rocks.");
                var states = effects.Rocks.ToArray();
                for (int update = 1; effects.Count != 0 && update <= 61; update++) effects.Advance();
                if (effects.Count != 0 || effects.RenderMesh.GetSurfaceCount() != 0 || states.Any(rock => rock.Alive || rock.BounceCount != 5))
                    throw new InvalidDataException("Source five-bounce death must immediately release rock bit geometry without body zombie delays.");
                effects.Emit(default, int.MaxValue, BouncingRockState.FireSelector, _ => 0);
                for (int update = 0; update < 60; update++) effects.Advance();
                if (effects.Count != 1 || effects.Rocks[0].Lifetime != 60) throw new InvalidDataException("Rock must survive exact source lifetime60.");
                effects.Advance();
                if (effects.Count != 0) throw new InvalidDataException("Rock must die on lifetime61 without a global-clock dependency.");
            }
            finally { effects.Free(); }
            if (!effects.ResourcesDisposed) throw new InvalidDataException("Rock renderer retained its owned texture/material/frame carrier resources.");
            GD.Print("Verified original Misc rock texture slots2/5, original-PC palette hashes, keyed opaque quads, source spin/bounce/lifetime and native cleanup.");
        }
        finally { viewport.Free(); }
    }
}
