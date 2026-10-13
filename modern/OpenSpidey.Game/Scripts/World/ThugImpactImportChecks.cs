using System.Security.Cryptography;
using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

internal static class ThugImpactImportChecks
{
    private sealed class Host(ImportedAnimatedModel model, ActorBodyMotion body, int parts) : IThugPingAttachmentHost
    {
        public bool Valid = true;
        public int Queries;
        private readonly object handle = new();
        public object StoreImpactHandle(object actor)
        {
            if (!ReferenceEquals(actor, model)) throw new InvalidDataException("Attached impact captured another source actor.");
            return handle;
        }
        public object? RecoverImpactHandle(object stored) => ReferenceEquals(stored, handle) && Valid ? model : null;
        public FixedVector3 GetDynamicHookPosition(object actor, ActorHook hook)
        {
            if (!ReferenceEquals(actor, model)) throw new InvalidDataException("Recovered impact handle resolved another source actor.");
            Queries++;
            var pose = new FixedJointTransform[parts];
            for (int i = 0; i < parts; i++) pose[i] = model.GetSourcePartPose(i);
            FixedJointTransform rotation = FixedJointTransform.FromJoint(new(body.Angles.X, body.Angles.Y, body.Angles.Z, 0, 0, 0));
            return ActorHookPosition.Evaluate(body.Position, body.Position >> 12, rotation, pose, hook);
        }
    }
    private sealed record Fixture(SubViewport Viewport, ImportedAnimatedModel Model, GodotThugImpactEffects Effect,
        Camera3D Camera, Host Host, Host InvalidatedHost, ThugAttachedPingState Attached, ThugAttachedPingState Invalidated,
        ThugLaserPingState Laser, FixedVector3 FirstHook);

    public static async Task VerifyAsync(Node3D owner, byte[] bitsBytes, byte[] enemyBytes, TriggerFile triggers, int actorType)
    {
        if (Engine.PhysicsTicksPerSecond != 60) throw new InvalidOperationException("Impact checks require two60Hz physics frames per source update.");
        var definition = TriggerActorDefinition.Read(triggers.Nodes[9]);
        if (definition.Position != new TriggerPosition(-18937, 1242, -19681)) throw new InvalidDataException("Impact checks lost the original L1A1 actor position.");
        var body = new ActorBodyMotion { Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
            Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z) };
        PsxScene scene = PsxSceneReader.Read(enemyBytes);
        IAnimationPoseSource animation = PsxMatrixAnimationReader.Read(enemyBytes) ?? throw new InvalidDataException("Attached impacts require original enemy animation packets.");
        var fixtures = new List<Fixture>();
        var viewports = new List<SubViewport>();
        var hook = new ActorHook(11, -13, 17, 3);
        try
        {
            foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
            {
                var viewport = new SubViewport { Size = size, OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                viewports.Add(viewport); owner.AddChild(viewport);
                var shifted = new Node3D { Position = new(37, 81, -22) }; viewport.AddChild(shifted);
                var model = ImportedAnimatedModel.Build(scene, PsxTextureReader.Read(enemyBytes), animation, false);
                shifted.AddChild(model); model.AdvanceAutomatically = false;
                model.InitializeSourceJoints(OpenSpidey.Game.Actors.OriginalThugModelPackets.PoseLinks(actorType));
                // SetAnimMode0 movement bytes select loop1 for thug and loop5 for henchman.
                model.CycleClip(actorType == 304 ? 1 : 5);
                model.GlobalPosition = PsxCoordinates.FixedWorldPosition(body.Position);
                model.Basis = PsxCoordinates.CharacterTransform(FixedJointTransform.FromJoint(new(body.Angles.X, body.Angles.Y, body.Angles.Z, 0, 0, 0))).Basis;
                var effect = GodotThugImpactEffects.Create(bitsBytes); shifted.AddChild(effect);
                if (effect.SourceFrame != new SpriteAnimationFrame(-12, -12, 24, 24, 0x7D3FEC27)) throw new InvalidDataException("HitPing frame1 differs from original animation10 packet.");
                using (Image image = effect.SourceTexture.GetImage())
                    if (image.GetWidth() != 24 || image.GetHeight() != 24 || Convert.ToHexString(SHA256.HashData(image.GetData())).ToLowerInvariant() != "4fff75acb5e02087cd35876447c18c79e919bf7bddf6de633c6ee32fcdda2400")
                        throw new InvalidDataException("The native impact texture differs from independently extracted original palette pixels.");
                var host = new Host(model, body, animation.Parents.Count);
                var invalidatedHost = new Host(model, body, animation.Parents.Count);
                var attached = new ThugAttachedPingState(model, hook, host, range => range == 1024 ? 512 : 0);
                var invalidated = new ThugAttachedPingState(model, hook, invalidatedHost, _ => 0);
                effect.EmitAttachedPing(attached); effect.EmitAttachedPing(invalidated);
                var laser = new ThugLaserPingState(attached.Position, new(0, 4096, 0), new(4096, 0, 0), 100, 100, 255);
                effect.EmitLaserPing(laser);
                var camera = new Camera3D { Current = true, KeepAspect = Camera3D.KeepAspectEnum.Height,
                    Fov = (float)OpenSpidey.Core.Cameras.CameraProjection.VerticalFieldOfView(OpenSpidey.Core.Cameras.CameraProjection.InitialZoom) };
                shifted.AddChild(camera); camera.GlobalPosition = PsxCoordinates.FixedWorldPosition(attached.Position) + new Vector3(0, 0, 10);
                camera.LookAt(PsxCoordinates.FixedWorldPosition(attached.Position)); camera.MakeCurrent(); effect.Rebuild();
                if (effect.GlobalTransform != Transform3D.Identity || effect.AttachedCount != 2 || effect.LaserCount != 1 || effect.RenderMesh.GetSurfaceCount() != 2)
                    throw new InvalidDataException("Attached impact placement, world-space ownership or initial flat quads changed.");
                VerifyMeshes(effect, 0, false);
                fixtures.Add(new(viewport, model, effect, camera, host, invalidatedHost, attached, invalidated, laser, attached.Position));
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (fixtures.Any(fixture => fixture.Attached.CodeRgb != 0x2E808080 || fixture.Host.Queries != 1 || fixture.Laser.Spread != 0))
                throw new InvalidDataException("Render frames advanced source impact state or hook queries.");
            bool hookMoved = false;
            for (int update = 1; update <= 19; update++)
            {
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                foreach (Fixture fixture in fixtures)
                {
                    fixture.Model.AdvanceTicks(2); // Original animation advances before the source effect update.
                    if (update == 7) fixture.InvalidatedHost.Valid = false;
                    fixture.Effect.Advance();
                    if (update < 19 && fixture.Attached.Position != fixture.Host.GetDynamicHookPosition(fixture.Model, hook))
                        throw new InvalidDataException("Attached impact failed to follow the actual animated original part hook.");
                    hookMoved |= fixture.Attached.Position != fixture.FirstHook;
                    if (update == 7 && (fixture.Invalidated.Alive || fixture.Invalidated.CodeRgb != 0x2E4F4F4F))
                        throw new InvalidDataException("An invalid source handle must die while still completing this update's RGB reduction.");
                    if (fixture.Effect.LaserCount != (update < 5 ? 1 : 0) || fixture.Effect.AttachedCount != (update < 7 ? 2 : update < 19 ? 1 : 0))
                        throw new InvalidDataException("Native impacts changed their source strict quad lifetime or19-update RGB expiry.");
                    if (update < 19) VerifyMeshes(fixture.Effect, update, update < 5);
                    else if (fixture.Effect.RenderMesh.GetSurfaceCount() != 0) throw new InvalidDataException("Dead source impacts retained native geometry.");
                }
            }
            if (!hookMoved) throw new InvalidDataException("Impact checks sampled no actual animated-hook movement.");
            GD.Print("Verified native thug impacts: original HitPing frame1/pixels,4:3/16:9 source textured quads, actual imported animated attachment, invalid handle ordering, five-update ricochet and19-update fade/cleanup.");
        }
        finally { foreach (SubViewport viewport in viewports) viewport.Free(); }
    }

    private static void VerifyMeshes(GodotThugImpactEffects effect, int update, bool laser)
    {
        int count = effect.RenderMesh.GetSurfaceCount();
        if (count != effect.AttachedCount + (laser ? 1 : 0)) throw new InvalidDataException("Source impacts lost visible native quads at a valid depth.");
        for (int surface = 0; surface < count; surface++)
        {
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(effect.RenderMesh.GetRid(), surface);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Vector2[] uv = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            int expected = laser && surface == 0 ? 12 : 6;
            if (vertices.Length != expected || uv.Length != expected || vertices.Any(vertex => !vertex.IsFinite()) ||
                uv.Any(value => value.X != 0.01f && value.X != 0.99f || value.Y != 0.01f && value.Y != 0.99f))
                throw new InvalidDataException("Original impact quads lost source front/back draw count or texture UV inset.");
            if (effect.RenderMesh.SurfaceGetMaterial(surface) is not StandardMaterial3D material || material.AlbedoTexture != effect.SourceTexture ||
                material.BlendMode != BaseMaterial3D.BlendModeEnum.Add || material.DepthDrawMode != BaseMaterial3D.DepthDrawModeEnum.Disabled)
                throw new InvalidDataException("Native impacts replaced their actual texture or additive/no-depth-write contract.");
        }
    }
}
