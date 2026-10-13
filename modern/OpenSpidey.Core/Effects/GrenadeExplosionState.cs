using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public interface IGrenadeExplosionHost
{
    /// <summary>Attach a genuine expgrnd model effect and return its recoverable native handle.</summary>
    object CreateExplosionModel(ExplosionModelParameters parameters, Func<int, int> random);
    bool IsExplosionModelHandleAlive(object handle);
    void ChangeExplosionCount(int delta);
    void MarkCarrierDead();
}

/// <summary>Original CGrenadeExplosion carrier owns the first of two model effects.</summary>
public sealed class GrenadeExplosionState : IDisposable
{
    private readonly IGrenadeExplosionHost host;
    private readonly object primaryHandle;
    private bool disposed;
    public bool IsDead { get; private set; }

    public GrenadeExplosionState(FixedVector3 position, IGrenadeExplosionHost host, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(random);
        this.host = host;
        random(5);
        primaryHandle = host.CreateExplosionModel(new(position, "expgrnd", 0, 0, 300, 789, 256, 0, 13, 0, 0), random);
        host.CreateExplosionModel(new(position, "expgrnd", 1, 0, 0, 500, 256, 0, 7, 0, 0), random);
        host.ChangeExplosionCount(1);
    }

    public void Move()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!host.IsExplosionModelHandleAlive(primaryHandle))
        {
            IsDead = true;
            host.MarkCarrierDead();
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        host.ChangeExplosionCount(-1);
        disposed = true;
    }
}
