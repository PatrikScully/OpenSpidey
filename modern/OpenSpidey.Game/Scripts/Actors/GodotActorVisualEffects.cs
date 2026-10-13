using Godot;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.Actors;

/// <summary>Actor effects emit into the same real GlowList and expgrnd BulletList owners used by other source effects.</summary>
public sealed class GodotActorVisualEffects : IActorVisualEffectHost
{
    private readonly Func<GodotGlowFlash> currentGlows;
    private readonly GodotBigExplosions explosions;
    private readonly Func<int, int> random;
    public GodotActorVisualEffects(Func<GodotGlowFlash> currentGlows, GodotBigExplosions explosions, Func<int, int> random)
    {
        this.currentGlows = currentGlows ?? throw new ArgumentNullException(nameof(currentGlows));
        this.explosions = explosions ?? throw new ArgumentNullException(nameof(explosions));
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }
    public int Random(int bound) => random(bound);
    public void CreateLaserFlash(LaserFlashParameters parameters)
    {
        GodotGlowFlash glows = currentGlows();
        if (!GodotObject.IsInstanceValid(glows)) throw new InvalidOperationException("Original actor laser flash requires its live shared GlowList owner.");
        glows.Emit(LaserFlash.Create(parameters, random));
    }
    public void TryCreateWibbling(ExplosionModelParameters parameters)
    {
        if (!GodotObject.IsInstanceValid(explosions)) throw new InvalidOperationException("Original actor explosion requires its live shared BulletList/model owner.");
        explosions.TryCreateWibbling(parameters, random);
    }
}
