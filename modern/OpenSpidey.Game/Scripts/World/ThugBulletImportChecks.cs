using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

internal static class ThugBulletImportChecks
{
    public static async Task VerifyAsync(Node3D owner, TriggerFile triggers)
    {
        if (Engine.PhysicsTicksPerSecond != 60) throw new InvalidOperationException("Bullet bit checks require two60Hz physics frames per source update.");
        TriggerActorDefinition first = TriggerActorDefinition.Read(triggers.Nodes[9]);
        TriggerActorDefinition target = TriggerActorDefinition.Read(triggers.Nodes[10]);
        if (first.Position != new TriggerPosition(-18937, 1242, -19681) || target.Position != new TriggerPosition(-17199, -422, -14734))
            throw new InvalidDataException("Native bullet checks require the independent original L1A1 thug positions.");
        FixedVector3 start = new FixedVector3(first.Position.X, first.Position.Y, first.Position.Z) << 12;
        FixedVector3 end = new FixedVector3(target.Position.X, target.Position.Y, target.Position.Z) << 12;
        var views = new List<SubViewport>();
        var effects = new List<GodotThugBulletEffects>();
        var schedulers = new List<SourceParticleScheduler>();
        try
        {
            foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
            {
                var viewport = new SubViewport { Size = size, OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                views.Add(viewport); owner.AddChild(viewport);
                var shifted = new Node3D { Position = new(37, 81, -22) }; viewport.AddChild(shifted);
                var effect = new GodotThugBulletEffects(); shifted.AddChild(effect); effects.Add(effect);
                var scheduler = new SourceParticleScheduler(); schedulers.Add(scheduler);
                var sourceFamilies = new List<SourceParticleList>();
                effect.SourceBitCreated += (family, bit) => { sourceFamilies.Add(family); scheduler.Attach(family, bit); };
                effect.SourceBitRemoved += identity => scheduler.Detach(identity);
                var camera = new Camera3D { Current = true, KeepAspect = Camera3D.KeepAspectEnum.Height,
                    Fov = (float)OpenSpidey.Core.Cameras.CameraProjection.VerticalFieldOfView(OpenSpidey.Core.Cameras.CameraProjection.InitialZoom) };
                shifted.AddChild(camera);
                camera.GlobalPosition = PsxCoordinates.FixedWorldPosition(end) + new Vector3(0, 0, 10);
                camera.LookAt(PsxCoordinates.FixedWorldPosition(end)); camera.MakeCurrent();
                // This geometry test joins two original actor positions, without claiming it is a muzzle hook.
                effect.Emit(start, end, 100, 100, 255, range => range == 0 ? 0 : range - 1);
                if (effect.GlobalTransform != Transform3D.Identity || effect.Count != 1 || effect.Tracers[0].Age != 0)
                    throw new InvalidDataException("Native bullet geometry lost source world-space ownership or constructor age.");
                ThugBulletTracerState tracer = effect.Tracers[0];
                if (!sourceFamilies.SequenceEqual(new[] { SourceParticleList.NonRendered, SourceParticleList.Special, SourceParticleList.Special }) ||
                    !scheduler.Identities(SourceParticleList.Special).SequenceEqual(new object[] { tracer.WhitePoints, tracer.ColoredPoints }) || scheduler.Count != 3)
                    throw new InvalidDataException("Tracer hooks lost the real NonRendered controller and protected white/colored Special-list companions.");
                effect.Rebuild(); VerifyGeometry(effect);
                effect.SetBarrelLine(start, end);
                if (!effect.BarrelLine.Exists || effect.BarrelLine.StartCodeRgb != 0x52500000 || effect.BarrelLine.EndCodeRgb != 0x52000000)
                    throw new InvalidDataException("Source distant barrel line must retain blue80 at start and black at end.");
                effect.SetBarrelLine(start, end);
                if (scheduler.Count != 4 || scheduler.Identities(SourceParticleList.GPolyLine).Count != 1)
                    throw new InvalidDataException("Persistent actor-owned CGPolyLine was duplicated when its endpoints changed.");
                effect.DeleteBarrelLine();
                if (effect.BarrelLine.Exists || scheduler.Count != 3) throw new InvalidDataException("DeleteBarrelLine retained the actor-owned line or its shared source registration.");
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (effects.Any(effect => effect.Tracers[0].Age != 0)) throw new InvalidDataException("Render cadence advanced tracer bit age.");
            for (int update = 1; update <= 5; update++)
            {
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                for (int index = 0; index < effects.Count; index++)
                {
                    GodotThugBulletEffects effect = effects[index];
                    SourceParticleScheduler scheduler = schedulers[index];
                    scheduler.Move((uint)update * 2);
                    if (update == 5 && (effect.Count != 1 || effect.Tracers[0].Alive ||
                        scheduler.Identities(SourceParticleList.Special).Count != 2 || scheduler.Count != 3))
                        throw new InvalidDataException("Dead tracer deleted protected ribbons during Move before the source NonRendered destructor phase.");
                    scheduler.RemoveDead();
                    if (update < 5 && (effect.Count != 1 || effect.Tracers[0].Age != update || effect.Tracers[0].ColoredPoints.Take(update).Any(point => point.Red != 0 || point.Green != 0 || point.Blue != 0)))
                        throw new InvalidDataException("Native tracer changed source point-by-point RGB extinction.");
                    if (update == 5 && (effect.Count != 0 || effect.RenderMesh.GetSurfaceCount() != 0 || scheduler.Count != 0))
                        throw new InvalidDataException("The fifth source bit update retained a dead bullet ribbon.");
                }
            }
            GD.Print("Verified native thug ribbons: original L1A1 positions, real4:3/16:9 camera geometry, source colored/white fade strips, persistent barrel clear, render-independent five30Hz updates and cleanup.");
        }
        finally { foreach (SubViewport viewport in views) viewport.Free(); }
    }

    private static void VerifyGeometry(GodotThugBulletEffects effect)
    {
        if (effect.RenderMesh.GetSurfaceCount() == 0) throw new InvalidDataException("Valid original bullet endpoints emitted no native ribbon geometry.");
        for (int surface = 0; surface < effect.RenderMesh.GetSurfaceCount(); surface++)
        {
            using Godot.Collections.Array arrays = RenderingServer.MeshSurfaceGetArrays(effect.RenderMesh.GetRid(), surface);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Color[] colors = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
            if (vertices.Length != 12 || colors.Length != 12 || colors[0] != Colors.Black || colors[1] != Colors.Black ||
                vertices.Any(vertex => !vertex.IsFinite()) || vertices[0] != vertices[7] || vertices[1] != vertices[6] || vertices[2] != vertices[10] || vertices[4] != vertices[8])
                throw new InvalidDataException("The original ribbon fade lost its two additive source quad passes or black outer edge.");
            if (effect.RenderMesh.SurfaceGetMaterial(surface) is not StandardMaterial3D material ||
                material.BlendMode != BaseMaterial3D.BlendModeEnum.Add || material.DepthDrawMode != BaseMaterial3D.DepthDrawModeEnum.Disabled || material.AlbedoTexture is not null)
                throw new InvalidDataException("The textureless source ribbon gained a substitute texture or changed additive/no-depth-write rendering.");
        }
    }
}
