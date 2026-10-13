using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public interface IBigExplosionHost
{
    /// <summary>Construct only after a successful allocation; positional RNG has already run.</summary>
    void TryCreateWibbling(ExplosionModelParameters parameters, Func<int, int> random);
}

public interface IWibblingExplosionCounter
{
    void ChangeCount(int delta);
}

/// <summary>Original Exp_Big3DExplosion's nine CWibbling3DExplosion constructions.</summary>
public static class BigExplosion
{
    private readonly record struct Emission(int Spread, int InitialScale, int ScaleStep, int GrowthTicks, int FadeRate, int VerticalSpeed, int VerticalSpeedRange);
    private static readonly Emission[] emissions =
    [
        new(5, 2048, 1750, 0, 15, 2, 2),
        new(5, 2048, 1750, 1, 27, 10, 10),
        new(5, 2048, 1500, 1, 20, 10, 10),
        new(40, 3500, 100, 1, 7, 5, 1),
        new(50, 2500, 100, 1, 7, 10, 5),
        new(55, 2000, 100, 1, 9, 15, 5),
        new(60, 2250, 100, 1, 8, 20, 5),
        new(65, 2500, 150, 1, 7, 25, 5),
        new(50, 3000, 200, 1, 7, 30, 2)
    ];
    public static void Create(FixedVector3 origin, IBigExplosionHost host, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(host); ArgumentNullException.ThrowIfNull(random);
        foreach (Emission emission in emissions)
        {
            int range = 2 * emission.Spread + 1;
            FixedVector3 position = origin + new FixedVector3(unchecked((random(range) - emission.Spread) << 12),
                unchecked((random(range) - emission.Spread) << 12), unchecked((random(range) - emission.Spread) << 12));
            host.TryCreateWibbling(new(position, "expgrnd", 1, 0, emission.InitialScale, emission.ScaleStep, 256,
                emission.GrowthTicks, emission.FadeRate, emission.VerticalSpeed, emission.VerticalSpeedRange), random);
        }
    }
}

/// <summary>CWibbling3DExplosion adds only its global constructor/destructor count to C3DExplosion.</summary>
public sealed class WibblingExplosionState : IDisposable
{
    private readonly IWibblingExplosionCounter counter;
    private readonly ActorLifecycle lifecycle = new(false, false);
    private bool disposed;
    public ExplosionModelState Model { get; }
    public ActorLifePhase Phase => lifecycle.Phase;
    public WibblingExplosionState(ExplosionModelParameters parameters, IWibblingExplosionCounter counter, Func<int, int> random)
    {
        this.counter = counter ?? throw new ArgumentNullException(nameof(counter));
        Model = new(parameters, random);
        counter.ChangeCount(1);
    }
    public ActorUpdateAction Advance()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ActorUpdateAction action = lifecycle.PrepareUpdate(0, 0);
        if (action == ActorUpdateAction.RunBehavior)
        {
            Model.Move();
            if (Model.IsDead) lifecycle.Kill();
        }
        else if (action == ActorUpdateAction.Delete) Dispose();
        return action;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; counter.ChangeCount(-1);
    }
}
