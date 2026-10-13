using OpenSpidey.Core.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Constructor/destructor events bind real grenade and explosion bodies to the scene's single BulletList.</summary>
public sealed class GodotGrenadeWorldListBinding : IDisposable
{
    private readonly GodotGrenadeSceneServices grenades;
    private readonly GodotGrenadeExplosions explosions;
    private readonly SourceActorList bullets;
    private readonly HashSet<object> identities = new(ReferenceEqualityComparer.Instance);
    private bool disposed;
    public int Count => identities.Count;

    public GodotGrenadeWorldListBinding(GodotActorRegistry registry, GodotGrenadeSceneServices grenades,
        GodotGrenadeExplosions explosions)
    {
        ArgumentNullException.ThrowIfNull(registry);
        this.grenades = grenades ?? throw new ArgumentNullException(nameof(grenades));
        this.explosions = explosions ?? throw new ArgumentNullException(nameof(explosions));
        if (!ReferenceEquals(grenades.Registry, registry))
            throw new ArgumentException("Grenade BulletList requires the actor scene that owns its genuine blast targets.", nameof(registry));
        if (grenades.SourceProjectileOrder.Count != 0 || explosions.ModelCount != 0)
            throw new InvalidOperationException("Bind the source BulletList before its first constructor; separate typed lists cannot reconstruct mixed creation order.");
        bullets = registry.SourceList(ActorUpdateGroup.Bullet);
        grenades.ProjectileAttached += AttachProjectile;
        grenades.ProjectileDetached += DetachProjectile;
        explosions.ModelAttached += AttachModel;
        explosions.ModelDetached += DetachModel;
    }

    private void AttachProjectile(GodotGrenadeProjectile projectile) =>
        Attach(projectile, () => grenades.AdvanceCapturedProjectile(projectile));
    private void AttachModel(GodotExplosionModel model) =>
        Attach(model, () => explosions.AdvanceCapturedModel(model));
    private void Attach(object identity, Action update)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        bullets.Attach(identity, update);
        identities.Add(identity);
    }
    private void DetachProjectile(GodotGrenadeProjectile projectile) => Detach(projectile);
    private void DetachModel(GodotExplosionModel model) => Detach(model);
    private void Detach(object identity)
    {
        if (identities.Remove(identity)) bullets.Detach(identity);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        grenades.ProjectileAttached -= AttachProjectile;
        grenades.ProjectileDetached -= DetachProjectile;
        explosions.ModelAttached -= AttachModel;
        explosions.ModelDetached -= DetachModel;
        foreach (object identity in identities) bullets.Detach(identity);
        identities.Clear();
    }
}
