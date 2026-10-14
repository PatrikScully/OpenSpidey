using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

public readonly record struct CameraHiddenItemSnapshot(byte Region, ushort Model, ushort Flags);
public sealed record CameraItemTrace<TItem>(TItem Item, FixedVector3 Position, int Length) where TItem : class;

/// <summary>The source camera's eight held item pointers and captured region/model generations, shared with boss-AI cleanup.</summary>
public sealed class CameraHiddenItems<TItem>(Func<TItem, CameraHiddenItemSnapshot> readItem, Action<TItem, ushort> writeFlags) where TItem : class
{
    public const int Capacity = 8;
    private readonly Func<TItem, CameraHiddenItemSnapshot> readItem = readItem ?? throw new ArgumentNullException(nameof(readItem));
    private readonly Action<TItem, ushort> writeFlags = writeFlags ?? throw new ArgumentNullException(nameof(writeFlags));
    private readonly TItem?[] items = new TItem?[Capacity];
    private readonly byte[] regions = new byte[Capacity];
    private readonly ushort[] models = new ushort[Capacity];
    public TItem? GetItem(int index) => items[index];
    public void ClearItem(int index) => items[index] = null;

    /// <summary>Run only when source F9 is nonzero, after mode placement. These ordinary mode1 rays do not set CameraCollisionCheck.</summary>
    public void Update(FixedVector3 cameraPosition, FixedVector3 tripodPosition, Func<FixedQuaternion> displayRotation,
        Func<FixedVector3, FixedVector3, CameraItemTrace<TItem>?> trace, Func<TItem?> heldItem, Action? overflowDiagnostic = null)
    {
        ArgumentNullException.ThrowIfNull(displayRotation);
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(heldItem);
        for (int index = 0; index < Capacity; index++)
        {
            if (items[index] is not { } item) break;
            CameraHiddenItemSnapshot current = readItem(item);
            if (current.Region == regions[index] && current.Model == models[index])
            {
                if (ReferenceEquals(heldItem(), item)) continue;
                writeFlags(item, unchecked((ushort)(current.Flags & ~0x800)));
            }
            items[index] = null;
        }
        CameraItemTrace<TItem>? contact = trace(cameraPosition, tripodPosition);
        if (contact is null) return;
        FixedVector3 step = FixedRotation.Transform(displayRotation().ToMatrix(), new(0, 0, 4096));
        int count = 0;
        do
        {
            TItem item = contact.Item;
            CameraHiddenItemSnapshot current = readItem(item);
            writeFlags(item, unchecked((ushort)(current.Flags | 0x800)));
            regions[count] = current.Region;
            models[count] = current.Model;
            items[count] = item;
            count++;
            if (count >= Capacity) { overflowDiagnostic?.Invoke(); break; }
            if (contact.Length <= 4) break;
            contact = trace(contact.Position + step * 2, tripodPosition);
        } while (contact is not null);
    }
}
