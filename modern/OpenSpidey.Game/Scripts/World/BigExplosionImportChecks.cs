using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Actual nine expgrnd1 models with source order/RNG, unlit geometry and derived destruction count.</summary>
public static class BigExplosionImportChecks
{
    public static async Task VerifyAsync(Node3D owner, byte[] originalExpgrnd)
    {
        var viewport = new SubViewport { Size = new(640, 480), OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        GodotBigExplosions? effect = null;
        try
        {
            var shifted = new Node3D { Position = new(37, 81, -22) }; viewport.AddChild(shifted);
            var camera = new Camera3D { Current = true, KeepAspect = Camera3D.KeepAspectEnum.Height,
                Fov = (float)OpenSpidey.Core.Cameras.CameraProjection.VerticalFieldOfView(OpenSpidey.Core.Cameras.CameraProjection.InitialZoom) };
            viewport.AddChild(camera); camera.GlobalPosition = new(0, 0, 10); camera.LookAt(Vector3.Zero); camera.MakeCurrent();
            effect = GodotBigExplosions.Create(originalExpgrnd); shifted.AddChild(effect);
            var bullets = new SourceActorList();
            GodotBigExplosions sourceOwner = effect;
            effect.ModelAttached += model => bullets.Attach(model, () => sourceOwner.AdvanceCapturedModel(model));
            effect.ModelDetached += model => bullets.Detach(model);
            var random = new GameRandom(0x43CF90); var calls = new List<int>();
            int Next(int bound) { calls.Add(bound); return random.Next(bound); }
            effect.Emit(default, Next);
            int[] ranges = [11, 11, 11, 2, 11, 11, 11, 10, 11, 11, 11, 10, 81, 81, 81, 1,
                101, 101, 101, 5, 111, 111, 111, 5, 121, 121, 121, 5, 131, 131, 131, 5, 101, 101, 101, 2];
            if (!calls.SequenceEqual(ranges) || effect.Count != 9 || effect.ModelCount != 9)
                throw new InvalidDataException("Exp_Big3DExplosion must construct nine actual models with36ordered positional/constructor RNG calls and no grenade carrier.");
            PsxScene source = PsxSceneReader.Read(originalExpgrnd);
            if (source.Models.Count <= 1 || source.Models[1].Faces.Count == 0)
                throw new InvalidDataException("Original big explosion expgrnd1 geometry is missing.");
            bool hasTextures = source.Models[1].Faces.Any(face => face.TextureChecksum is not null);
            int[] sourceScales = [3000, 2500, 2250, 2000, 2500, 3500, 2048, 2048, 2048];
            int[] sourceSteps = [200, 150, 100, 100, 100, 100, 1500, 1750, 1750];
            var first = effect.SourceModelOrder.ToArray();
            for (int index = 0; index < first.Length; index++)
            {
                GodotExplosionModel model = first[index];
                if (model.State.ModelIndex != 1 || model.State.ModelName != "expgrnd" || model.State.Scale != new FixedVector3(sourceScales[index], sourceScales[index], sourceScales[index]) ||
                    model.State.ScaleStep != sourceSteps[index] || model.State.ItemFlags != 0x600 || !model.Visible || model.RenderMesh.GetSurfaceCount() == 0 ||
                    model.Materials.Count == 0 || (model.Textures.Count != 0) != hasTextures || model.GetChildren().OfType<MeshInstance3D>().Single().Mesh != model.RenderMesh ||
                    !model.GlobalTransform.Origin.IsEqualApprox(PsxCoordinates.FixedWorldPosition(model.State.Position)))
                    throw new InvalidDataException("Nine original expgrnd1 models lost source head order, parameter tuples, genuine textured mesh or world-space birth under shifted ownership.");
                using Godot.Collections.Array arrays = model.RenderMesh.SurfaceGetArrays(0);
                if (arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length == 0 || arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array().Length == 0)
                    throw new InvalidDataException("Actual big explosion model must contain source model vertices and texture UVs.");
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (first.Any(model => model.State.GrowthAge != 0) || effect.Count != 9)
                throw new InvalidDataException("Rendering advanced the source big explosion BulletList state or derived count.");
            effect.Emit(new(409600, 0, 0), Next);
            if (effect.Count != 18 || effect.ModelCount != 18 || !calls.SequenceEqual(ranges.Concat(ranges)) ||
                !effect.SourceModelOrder.Skip(9).SequenceEqual(first))
                throw new InvalidDataException("Second big explosion must prepend nine independent source models and preserve the session-global derived count.");
            int[] deleteFrames = [12, 8, 10, 22, 22, 18, 19, 22, 22];
            for (int frame = 1; frame <= 22; frame++)
            {
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                bullets.Update();
                int remaining = 2 * deleteFrames.Count(deletion => deletion > frame);
                if (effect.Count != remaining || effect.ModelCount != remaining)
                    throw new InvalidDataException($"CWibbling global count changed before actual source destructor at model frame{frame}.");
                if (frame == 6 && (!first[7].State.IsDead || effect.SourcePhases.Count(phase => phase == ActorLifePhase.Dead) != 2 || !first[7].Visible))
                    throw new InvalidDataException("First source RGB death must retain both model1 meshes/count through Dead until pending/deletion updates.");
                if (frame == 7 && effect.SourcePhases.Count(phase => phase == ActorLifePhase.PendingDeletion) != 2)
                    throw new InvalidDataException("Dead big explosion models must retain their derived count during the pending update.");
            }
            if (effect.Count != 0 || effect.ModelCount != 0 || first.Any(model => GodotObject.IsInstanceValid(model)))
                throw new InvalidDataException("Source big explosion destructor cleanup retained native model geometry or its derived global count.");
            effect.Emit(default, Next);
            var unload = effect.SourceModelOrder.ToArray();
            unload[0].Free();
            effect.AdvanceCapturedModel(unload[0]);
            if (effect.Count != 8 || effect.ModelCount != 8 || bullets.Count != 8)
                throw new InvalidDataException("External native model destruction must detach its exact BulletList entry and decrement the derived count once before a captured callback can run.");
            effect.Free();
            if (effect.Count != 0 || effect.ModelCount != 0 || unload.Any(model => GodotObject.IsInstanceValid(model)))
                throw new InvalidDataException("Session unload must destroy remaining CWibbling models and restore exact global count once.");
            if (bullets.Count != 0) throw new InvalidDataException("Session unload retained source BulletList entries for freed big-explosion models.");
            effect = null;
            GD.Print("Verified actual nine-model Exp_Big3DExplosion:36source RNG calls, expgrnd1 geometry/UVs, original tuples/head order, two simultaneous bursts, source bullet growth/fade, delayed derived-count destruction and unload cleanup.");
        }
        finally { effect?.Free(); viewport.Free(); }
    }
}
