using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.World;

internal static class GrenadeExplosionOwnershipImportChecks
{
    public static async Task VerifyAsync(Node3D owner, byte[] originalModels)
    {
        var explosions = GodotGrenadeExplosions.Create(originalModels);
        owner.AddChild(explosions);
        var calls = new List<int>();
        var particles = new SourceParticleScheduler();
        var bullets = new SourceActorList();
        var bulletOrder = new List<GodotExplosionModel>();
        var constructed = new List<int>();
        var deleted = new List<GodotExplosionModel>();
        explosions.ModelAttached += model =>
        {
            bullets.Attach(model, () => explosions.AdvanceCapturedModel(model));
            bulletOrder.Insert(0, model); constructed.Add(model.State.ModelIndex);
        };
        explosions.ModelDetached += model => { bullets.Detach(model); bulletOrder.Remove(model); deleted.Add(model); };
        explosions.SourceBitCreated += bit => particles.Attach(SourceParticleList.NonRendered, bit);
        explosions.SourceBitRemoved += identity => particles.Detach(identity);
        try
        {
            explosions.Emit(new(-17491 << 12, -1723 << 12, -14420 << 12), bound => { calls.Add(bound); return 0; });
            GodotExplosionModel[] order = explosions.SourceModelOrder.ToArray();
            GodotExplosionModel secondary = order[0], primary = order[1];
            if (explosions.Count != 1 || explosions.ModelCount != 2 || explosions.CarrierCount != 1 ||
                primary.State.ModelIndex != 0 || secondary.State.ModelIndex != 1 || !calls.SequenceEqual(new[] { 5, 0, 0 }))
                throw new InvalidDataException("Native grenade carrier changed source creation/RNG/model head order.");
            if (!constructed.SequenceEqual(new[] { 0, 1 }) || !bulletOrder.SequenceEqual(order) || bullets.Count != 2 || particles.Count != 1)
                throw new InvalidDataException("Native grenade hooks lost actual Bullet head insertion or NonRendered carrier identity.");
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (primary.State.Scale.X != 300 || secondary.State.Scale.X != 0 || primary.State.Tint != 0x808080)
                throw new InvalidDataException("Rendered frames advanced source explosion state without a bullet update.");
            explosions.RemoveChild(primary);
            if (primary.ResourcesDisposed || !GodotObject.IsInstanceValid(primary) || explosions.ModelCount != 2 || bullets.Count != 2 || deleted.Count != 0)
                throw new InvalidDataException("Native reparenting destroyed a live source explosion or detached its actual Bullet body.");
            explosions.AddChild(primary);
            for (int update = 1; update <= 22; update++)
            {
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
                bullets.Update();
                particles.Move((uint)update * 2);
                if (update == 13 && (explosions.CarrierCount != 1 || explosions.Count != 1 || !deleted.SequenceEqual(new[] { primary })))
                    throw new InvalidDataException("Weak-handle carrier died before its primary Bullet destructor, or was deleted during bit Move.");
                particles.RemoveDead();
                if (update is 11 or 12 && (explosions.Count != 1 || explosions.CarrierCount != 1 || explosions.ModelCount != 2 ||
                    !primary.State.IsDead || !primary.Visible || !GodotObject.IsInstanceValid(primary)))
                    throw new InvalidDataException("A source-dead explosion lost its visible mesh/weak handle before two-pass body deletion.");
                if (update == 13 && (explosions.Count != 0 || explosions.CarrierCount != 0 || explosions.ModelCount != 1 ||
                    GodotObject.IsInstanceValid(primary) || !GodotObject.IsInstanceValid(secondary) || secondary.State.Tint != 0x2C2C2C))
                    throw new InvalidDataException("Carrier lifetime followed tint/death instead of primary model deletion, or destroyed the independent secondary model.");
                if (update == 22 && (explosions.ModelCount != 0 || GodotObject.IsInstanceValid(secondary)))
                    throw new InvalidDataException("Secondary explosion model survived its original fade and two-update deletion.");
            }
            explosions.Emit(default, _ => 0);
            GodotExplosionModel[] cleanup = explosions.SourceModelOrder.ToArray();
            GodotExplosionModel externallyDeleted = cleanup.Single(model => model.State.ModelIndex == 0);
            explosions.RemoveChild(externallyDeleted);
            externallyDeleted.Free();
            if (!externallyDeleted.ResourcesDisposed || explosions.ModelCount != 1 || bullets.Count != 1 || explosions.CarrierCount != 1)
                throw new InvalidDataException("Unparented native Free did not detach the actual source explosion body through its destructor.");
            particles.Move(50); particles.RemoveDead();
            if (explosions.Count != 0 || explosions.CarrierCount != 0 || particles.Count != 0)
                throw new InvalidDataException("Source carrier retained an externally deleted primary native model handle.");
            explosions.Free();
            if (explosions.Count != 0 || cleanup.Any(GodotObject.IsInstanceValid) || bullets.Count != 0 || particles.Count != 0)
                throw new InvalidDataException("Scene exit retained a grenade counter or native model handle.");
            GD.Print("Verified native grenade ownership: original two-model/RNG order, render-independent30Hz updates, two-pass body deletion, weak-handle carrier/count and independent secondary lifetime.");
        }
        finally { if (GodotObject.IsInstanceValid(explosions)) explosions.Free(); }
    }
}
