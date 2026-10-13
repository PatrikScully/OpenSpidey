using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Player;

/// <summary>Actual additional-render-list plain CBody identity; tree movement preserves its source handle and model resources.</summary>
public partial class GodotPlayerAdditionalBody : Node3D
{
    private bool disposed;
    internal GodotSourceRigidModel Model { get; private set; } = null!;
    public PlayerAdditionalBody State { get; private set; } = null!;
    internal event Action<GodotPlayerAdditionalBody>? Destroyed;
    internal static GodotPlayerAdditionalBody Create(PsxAssetLibrary items, PlayerAdditionalBody state)
    {
        if (state.Type is not (504 or 505) || state.ModelIndex != (state.Type == 504 ? 4 : 8))
            throw new InvalidDataException("Additional body requires the actual source504/model4 or505/model8 selection.");
        var body = new GodotPlayerAdditionalBody { Name = $"OriginalAdditionalBody{state.Type}", State = state };
        try
        {
            body.Model = GodotSourceRigidModel.Create(items, state.ModelIndex);
            body.AddChild(body.Model); body.Synchronize(); return body;
        }
        catch { body.Free(); throw; }
    }
    public void Synchronize()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Model.Synchronize(State.Position, State.Scale, State.ItemFlags, State.Angles);
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true; Destroyed?.Invoke(this); Destroyed = null;
    }
}

/// <summary>Real SpideyAdditionalBodyPartsList ownership and generation-checked ED4/5B8 handles, outside actor AI/baddy accounting.</summary>
public partial class GodotPlayerAdditionalBodies : Node3D
{
    private sealed record Handle(WeakReference<GodotPlayerAdditionalBody> Body, uint Validation);
    private readonly List<GodotPlayerAdditionalBody> bodies = [];
    private readonly Dictionary<GodotPlayerAdditionalBody, uint> generations = [];
    private PsxAssetLibrary items = null!;
    private uint validation;
    private bool disposed;
    public PlayerAdditionalEffectState State { get; private set; } = null!;
    public int Count => bodies.Count;
    public IReadOnlyList<GodotPlayerAdditionalBody> SourceOrder => bodies;
    public event Action<GodotPlayerAdditionalBody>? BodyCreated;
    public event Action<GodotPlayerAdditionalBody>? BodyRemoved;

    public static GodotPlayerAdditionalBodies Create(PsxAssetLibrary items, PlayerAdditionalEffectState state)
    {
        ArgumentNullException.ThrowIfNull(items); ArgumentNullException.ThrowIfNull(state);
        if (!items.AssetPath.Equals("data/items.psx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Source additional bodies require their original items region.");
        return new() { Name = "OriginalSpideyAdditionalBodyPartsList", items = items, State = state };
    }
    public PlayerKickHandle CreateSense(PlayerAdditionalBodyCheats cheats)
    {
        PlayerKickHandle handle = Attach(PlayerAdditionalBodyRules.CreateSense(cheats));
        State.SenseBody = handle; return handle;
    }
    public PlayerKickHandle CreateFist(int slot, PlayerAdditionalBodyCheats cheats)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        if (slot >= State.BonusFists.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        PlayerKickHandle handle = Attach(PlayerAdditionalBodyRules.CreateFist(cheats));
        State.BonusFists[slot] = handle; return handle;
    }
    private PlayerKickHandle Attach(PlayerAdditionalBody state)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (++validation == 0) throw new InvalidOperationException("Source additional-body handle generation exhausted.");
        GodotPlayerAdditionalBody body = GodotPlayerAdditionalBody.Create(items, state);
        try
        {
            body.Destroyed += Retire;
            generations.Add(body, validation); bodies.Insert(0, body);
            AddChild(body); BodyCreated?.Invoke(body);
            return new(new Handle(new(body), validation), validation);
        }
        catch { body.Free(); throw; }
    }
    public GodotPlayerAdditionalBody? Recover(ref PlayerKickHandle handle)
    {
        if (handle.Target is Handle token && token.Validation == handle.Validation && token.Body.TryGetTarget(out GodotPlayerAdditionalBody? body) &&
            GodotObject.IsInstanceValid(body) && generations.TryGetValue(body, out uint actual) && actual == token.Validation) return body;
        handle = handle with { Target = null }; return null;
    }
    public void Delete(GodotPlayerAdditionalBody body)
    {
        if (!generations.ContainsKey(body)) throw new InvalidOperationException("Additional-body destructor requires its live source-list identity.");
        // Source callers unlink before invoking the scalar deleting destructor.
        Retire(body); body.Free();
    }
    private void Retire(GodotPlayerAdditionalBody body)
    {
        if (!generations.Remove(body)) return;
        bodies.Remove(body); BodyRemoved?.Invoke(body);
    }
    public void DeletePlayerOwnedParts()
    {
        PlayerKickHandle handle = State.SenseBody;
        if (Recover(ref handle) is { } sense) Delete(sense);
        for (int slot = 0; slot < State.BonusFists.Length; slot++)
        {
            handle = State.BonusFists[slot];
            if (Recover(ref handle) is { } fist) Delete(fist);
        }
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        DeletePlayerOwnedParts();
        foreach (GodotPlayerAdditionalBody body in bodies.ToArray()) Delete(body);
    }
}
