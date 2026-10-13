using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Effects;

/// <summary>Actual Exp_Big3DExplosion expgrnd1 models and CWibbling global count in the source bullet phase.</summary>
public partial class GodotBigExplosions : Node3D, IBigExplosionHost, IWibblingExplosionCounter
{
    private sealed record Entry(GodotExplosionModel Native, WibblingExplosionState State);
    private readonly List<Entry> models = [];
    private byte[] originalModelBytes = null!;
    private bool disposed;
    public int Count { get; private set; }
    public int ModelCount => models.Count;
    public event Action<GodotExplosionModel>? ModelAttached;
    public event Action<GodotExplosionModel>? ModelDetached;
    internal IReadOnlyList<GodotExplosionModel> SourceModelOrder => models.Select(entry => entry.Native).ToArray();
    internal IReadOnlyList<ActorLifePhase> SourcePhases => models.Select(entry => entry.State.Phase).ToArray();
    public static GodotBigExplosions Create(byte[] originalModelBytes)
    {
        ArgumentNullException.ThrowIfNull(originalModelBytes);
        return new() { Name = "OriginalBigExplosions", originalModelBytes = originalModelBytes };
    }
    public void Emit(FixedVector3 position, Func<int, int> random)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        BigExplosion.Create(position, this, random);
    }
    public void TryCreateWibbling(ExplosionModelParameters parameters, Func<int, int> random)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var state = new WibblingExplosionState(parameters, this, random);
        GodotExplosionModel? native = null;
        Entry? entry = null;
        try
        {
            native = GodotExplosionModel.Create(originalModelBytes, state.Model);
            entry = new(native, state);
            Entry registered = entry;
            native.AddChild(new GodotNativeDestructionObserver
                { Name = "SourceBulletDestruction", Destroyed = () => Detach(registered) });
            AddChild(native); models.Insert(0, entry);
            ModelAttached?.Invoke(native);
        }
        catch
        {
            if (entry is not null) Remove(entry);
            else { state.Dispose(); native?.Free(); }
            throw;
        }
    }
    public void ChangeCount(int delta) => Count = unchecked(Count + delta);
    public void AdvanceModels()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (GodotExplosionModel native in SourceModelOrder) AdvanceCapturedModel(native);
    }
    public void AdvanceCapturedModel(GodotExplosionModel native)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Entry? entry = models.FirstOrDefault(entry => ReferenceEquals(entry.Native, native));
        if (entry is null) return;
        if (entry.State.Advance() == ActorUpdateAction.Delete) Remove(entry);
        else entry.Native.Synchronize();
    }
    private void Remove(Entry entry)
    {
        Detach(entry);
        if (GodotObject.IsInstanceValid(entry.Native)) entry.Native.Free();
    }
    private void Detach(Entry entry)
    {
        if (!models.Remove(entry)) { entry.State.Dispose(); return; }
        entry.State.Dispose();
        ModelDetached?.Invoke(entry.Native);
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        if (disposed) return;
        disposed = true;
        foreach (Entry entry in models.ToArray()) Remove(entry);
    }
}
