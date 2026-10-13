using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Effects;

/// <summary>Owns source explosion models in the bullet phase and their weak-handle carriers in the bit phase.</summary>
public partial class GodotGrenadeExplosions : Node3D
{
    private sealed class ModelEntry(GodotExplosionModel model)
    {
        public GodotExplosionModel Model { get; } = model;
        public ActorLifecycle Lifecycle { get; } = new(false, false);
        public bool Registered { get; set; } = true;
    }
    private sealed class CarrierHost(GodotGrenadeExplosions owner) : IGrenadeExplosionHost
    {
        public bool IsDead { get; private set; }
        public object CreateExplosionModel(ExplosionModelParameters parameters, Func<int, int> random) => owner.CreateModel(parameters, random);
        public bool IsExplosionModelHandleAlive(object handle) => handle is ModelEntry entry && entry.Registered && GodotObject.IsInstanceValid(entry.Model);
        public void ChangeExplosionCount(int delta) => owner.Count = unchecked(owner.Count + delta);
        public void MarkCarrierDead() => IsDead = true;
    }
    private sealed record CarrierEntry(GrenadeExplosionState State, CarrierHost Host);
    private readonly List<ModelEntry> models = [];
    private readonly List<CarrierEntry> carriers = [];
    private byte[] originalModelBytes = null!;
    private bool disposed;
    public int Count { get; private set; }
    public int ModelCount => models.Count;
    public int CarrierCount => carriers.Count;
    public IReadOnlyList<GodotExplosionModel> SourceModelOrder => models.Select(entry => entry.Model).ToArray();
    public event Action<GodotExplosionModel>? ModelAttached;
    public event Action<GodotExplosionModel>? ModelDetached;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    public event Action<object>? SourceBitRemoved;

    public static GodotGrenadeExplosions Create(byte[] originalModelBytes)
    {
        ArgumentNullException.ThrowIfNull(originalModelBytes);
        return new() { Name = "GrenadeExplosions", originalModelBytes = originalModelBytes };
    }

    public void Emit(FixedVector3 position, Func<int, int> random)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        int previousModels = models.Count;
        GrenadeExplosionState? state = null;
        CarrierEntry? carrier = null;
        try
        {
            var host = new CarrierHost(this);
            state = new(position, host, random);
            carrier = new CarrierEntry(state, host);
            carriers.Insert(0, carrier);
            CarrierEntry registered = carrier;
            SourceBitCreated?.Invoke(new(registered.State, _ => registered.State.Move(),
                () => !disposed && carriers.Contains(registered) && !registered.Host.IsDead,
                () => RemoveCarrier(registered)));
        }
        catch
        {
            if (carrier is not null) RemoveCarrier(carrier);
            else state?.Dispose();
            // A failed asset import must release models already constructed by this carrier.
            while (models.Count > previousModels) RemoveModel(models[0]);
            throw;
        }
    }

    private ModelEntry CreateModel(ExplosionModelParameters parameters, Func<int, int> random)
    {
        var model = GodotExplosionModel.Create(originalModelBytes, new(parameters, random));
        var entry = new ModelEntry(model);
        try
        {
            model.AddChild(new GodotNativeDestructionObserver { Name = "SourceBulletDestruction", Destroyed = () => DetachModel(entry) });
            AddChild(model); models.Insert(0, entry);
            ModelAttached?.Invoke(model);
            return entry;
        }
        catch { RemoveModel(entry); throw; }
    }

    public void AdvanceModels()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (GodotExplosionModel model in SourceModelOrder) AdvanceCapturedModel(model);
    }

    public void AdvanceCapturedModel(GodotExplosionModel model)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ModelEntry? entry = models.FirstOrDefault(entry => ReferenceEquals(entry.Model, model));
        if (entry is null || !entry.Registered) return;
        switch (entry.Lifecycle.PrepareUpdate(0, 0))
        {
            case ActorUpdateAction.RunBehavior:
                entry.Model.Advance();
                if (entry.Model.State.IsDead) entry.Lifecycle.Kill();
                break;
            case ActorUpdateAction.Delete: RemoveModel(entry); break;
        }
    }

    public void AdvanceCarriers()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (CarrierEntry carrier in carriers.ToArray())
        {
            carrier.State.Move();
            if (!carrier.Host.IsDead) continue;
            RemoveCarrier(carrier); // Standalone facade performs its own subsequent cleanup.
        }
    }

    private void RemoveModel(ModelEntry entry)
    {
        DetachModel(entry);
        if (GodotObject.IsInstanceValid(entry.Model)) entry.Model.Free();
    }

    private void DetachModel(ModelEntry entry)
    {
        bool attached = models.Remove(entry);
        entry.Registered = false;
        if (attached) ModelDetached?.Invoke(entry.Model);
    }

    private void RemoveCarrier(CarrierEntry carrier)
    {
        if (!carriers.Remove(carrier)) return;
        carrier.State.Dispose();
        SourceBitRemoved?.Invoke(carrier.State);
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        if (disposed) return;
        disposed = true;
        foreach (CarrierEntry carrier in carriers.ToArray()) RemoveCarrier(carrier);
        foreach (ModelEntry entry in models.ToArray()) RemoveModel(entry);
    }
}
