using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.World;

internal static class ThugVisualEffectsImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository libraries,
        SoundChannelController sounds, Func<SoundListener> listener)
    {
        var random = new GameRandom(0x12A6CC58);
        var effects = GodotThugVisualEffects.Create(archive, libraries, sounds, listener, random.Next, () => 0);
        owner.AddChild(effects);
        var particles = new SourceParticleScheduler();
        effects.SourceBitCreated += (family, bit) => particles.Attach(family, bit);
        effects.SourceBitRemoved += identity => particles.Detach(identity);
        try
        {
            foreach ((string name, int parts) in new[] { ("thug", 16), ("henchman", 15), ("henchngt", 15) })
            {
                var model = effects.Resources.CreateModel(name);
                effects.AddChild(model);
                model.AdvanceAutomatically = false;
                try
                {
                    if (effects.Resources.CollisionBoxes(name).Count != parts)
                        throw new InvalidDataException($"{name} resource factory changed its original near-part collision count.");
                    model.VerifyCurrentPose();
                    model.AdvanceTicks(2);
                    model.VerifyCurrentPose();
                }
                finally { model.Free(); }
            }
            var shadow = effects.CreateProjectileShadow();
            effects.AddChild(shadow);
            shadow.State.Update(8, default, 32, 0, 300);
            shadow.Rebuild();
            if (!shadow.State.Exists || shadow.State.CodeBgr != 0x1C0341 || shadow.State.PcBlendMode != 1)
                throw new InvalidDataException("Visual effect composition lost the genuine protected Shadow2 projectile shadow.");
            shadow.Free();
            effects.CombatRings.Emit(312, default, 0, random.Next);
            effects.DrawGlowFlash(default, new[] { 5, 255, 255, 255, 1, 128, 100, 0, 1, 60, 0, 1, 100, 120, 80, 100, 10, 4 });
            if (effects.CombatRings.Count != 2 || effects.Glows.Count != 1 || particles.Count != 3 ||
                particles.Identities(SourceParticleList.Flat).Count != 2 || particles.Identities(SourceParticleList.Glow).Count != 1)
                throw new InvalidDataException("The scene effect owner lost the actual ring/flash constructor requests.");
            object[] identities = particles.Identities(SourceParticleList.Flat).Concat(particles.Identities(SourceParticleList.Glow)).ToArray();
            Rid glowMesh = effects.Glows.RenderMesh.GetRid(), tracerMesh = effects.Tracers.RenderMesh.GetRid(), spitMesh = effects.SpitParticles.RenderMesh.GetRid();
            var reparent = new Node3D { Name = "ActualThugEffectsReparentProof" }; owner.AddChild(reparent);
            owner.RemoveChild(effects); reparent.AddChild(effects);
            if (effects.Glows.ResourcesDisposed || effects.Glows.RenderMesh.GetRid() != glowMesh ||
                effects.Tracers.RenderMesh.GetRid() != tracerMesh || effects.SpitParticles.RenderMesh.GetRid() != spitMesh ||
                !particles.Identities(SourceParticleList.Flat).Concat(particles.Identities(SourceParticleList.Glow)).SequenceEqual(identities))
                throw new InvalidDataException("Reparenting a real visual owner discarded original bit identities or disposed retained native mesh resources.");
            effects.Glows.Rebuild(); effects.Tracers.Rebuild();
            reparent.RemoveChild(effects); owner.AddChild(effects); reparent.Free();
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            if (effects.CombatRings.Count != 2 || effects.Glows.Count != 1)
                throw new InvalidDataException("Scene render cadence consumed owned source effect lifetimes.");
            for (uint update = 1; update <= 6; update++) { particles.Move(update * 2); particles.RemoveDead(); }
            if (effects.CombatRings.Count != 0 || effects.Glows.Count != 0 || particles.Count != 0)
                throw new InvalidDataException("Composed source bit phases did not finish the original ring/flash lifetimes.");
            GD.Print("Verified native thug visual ownership: all three original model/collision families, genuine Shadow2, composed ring/glow source updates and cleanup.");
        }
        finally { effects.Free(); }
    }
}
