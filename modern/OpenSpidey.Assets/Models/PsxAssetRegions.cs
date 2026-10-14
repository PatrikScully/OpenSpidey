namespace OpenSpidey.Assets.Models;

/// <summary>One original forty-slot, first-free table for loaded bank generations and privately owned dynamic regions.</summary>
public sealed class PsxAssetRegions : IDisposable
{
    public const int Capacity = 40;
    private abstract class Entry { public bool Protected { get; set; } }
    private sealed class BankEntry(PsxAssetLibrary library) : Entry { public PsxAssetLibrary Library { get; } = library; }
    private sealed class DynamicEntry(PsxDynamicRegionLease lease) : Entry { public PsxDynamicRegionLease Lease { get; } = lease; }
    private readonly Entry?[] slots = new Entry?[Capacity];
    private readonly IReadOnlyDictionary<string, PsxAssetLibrary> loaded;
    private readonly Action? unsubscribe;
    private bool disposed;

    public PsxAssetRegions(PsxLibraryRepository repository) : this(repository?.Loaded ??
        throw new ArgumentNullException(nameof(repository)))
    {
        repository.LibraryLoaded += Register;
        repository.LibraryUnloaded += Remove;
        unsubscribe = () =>
        {
            repository.LibraryLoaded -= Register;
            repository.LibraryUnloaded -= Remove;
        };
    }

    public PsxAssetRegions(IReadOnlyDictionary<string, PsxAssetLibrary> loaded)
    {
        this.loaded = loaded ?? throw new ArgumentNullException(nameof(loaded));
        Synchronize();
    }

    public int Count { get { Synchronize(); return slots.Count(entry => entry is not null); } }

    public byte RequireRegion(PsxAssetLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        Synchronize();
        int index = Array.FindIndex(slots, entry => entry is BankEntry bank && ReferenceEquals(bank.Library, library));
        return index >= 0 ? checked((byte)index) :
            throw new InvalidOperationException($"Source bank {library.AssetPath} is not the current loaded generation.");
    }

    public byte RequireRegion(PsxDynamicRegionLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        Synchronize();
        return Contains(lease) ? lease.Slot : throw new InvalidOperationException("The dynamic source region is not this table's current slot generation.");
    }

    public PsxAssetLibrary Resolve(byte region)
    {
        Synchronize();
        return region < Capacity && slots[region] is BankEntry bank ? bank.Library :
            throw new InvalidDataException($"Source region {region} has no usable loaded bank.");
    }

    public PsxDynamicRegionLease ResolveDynamic(byte region)
    {
        Synchronize();
        return region < Capacity && slots[region] is DynamicEntry dynamic ? dynamic.Lease :
            throw new InvalidDataException($"Source region {region} has no current dynamic generation.");
    }

    /// <summary>Reserve a real first-free slot before constructing its private geometry/palette. Dispose the lease on initialization failure.</summary>
    public PsxDynamicRegionLease AllocateDynamic(string name, Action releaseResources, bool protectedRegion = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(releaseResources);
        if (name.Length > 8 || name.Any(character => character is < (char)1 or > (char)127))
            throw new ArgumentException("A source region name must fit its eight-byte ASCII filename.", nameof(name));
        Synchronize();
        int index = FirstFreeSlot();
        var lease = new PsxDynamicRegionLease(this, checked((byte)index), name, releaseResources);
        slots[index] = new DynamicEntry(lease) { Protected = protectedRegion };
        return lease;
    }

    public void SetProtected(PsxAssetLibrary library, bool value = true) => slots[RequireRegion(library)]!.Protected = value;
    public bool IsProtected(PsxAssetLibrary library) => slots[RequireRegion(library)]!.Protected;
    public void SetProtected(PsxDynamicRegionLease lease, bool value = true) => slots[RequireRegion(lease)]!.Protected = value;
    public bool IsProtected(PsxDynamicRegionLease lease) => slots[RequireRegion(lease)]!.Protected;

    /// <summary>Spool_ClearAllPSXs visits all forty slots in order. Protected dynamic S/SW slots survive until their owning actor is destroyed.</summary>
    public void ClearUnprotected(Action<PsxAssetLibrary> unload)
    {
        ArgumentNullException.ThrowIfNull(unload);
        Synchronize();
        for (int index = 0; index < slots.Length; index++)
        {
            Entry? entry = slots[index];
            if (entry is null || entry.Protected) continue;
            if (entry is DynamicEntry dynamic) { dynamic.Lease.Dispose(); continue; }
            PsxAssetLibrary library = ((BankEntry)entry).Library;
            unload(library);
            Synchronize();
            if (slots[index] is BankEntry bank && ReferenceEquals(bank.Library, library))
                throw new InvalidOperationException($"Source library {library.AssetPath} did not release its region during cleanup.");
        }
    }

    public void Synchronize()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (loaded.Count > Capacity)
            throw new InvalidOperationException($"The source region table supports at most {Capacity} simultaneously loaded banks.");
        for (int index = 0; index < slots.Length; index++)
            if (slots[index] is BankEntry bank && (!loaded.TryGetValue(bank.Library.AssetPath, out PsxAssetLibrary? current) ||
                !ReferenceEquals(bank.Library, current))) slots[index] = null;
        foreach (PsxAssetLibrary library in loaded.Values) Register(library);
    }

    private int FirstFreeSlot()
    {
        int index = Array.FindIndex(slots, entry => entry is null);
        return index >= 0 ? index : throw new InvalidOperationException("No source asset region is available.");
    }

    private void Register(PsxAssetLibrary library)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (slots.Any(entry => entry is BankEntry bank && ReferenceEquals(bank.Library, library))) return;
        slots[FirstFreeSlot()] = new BankEntry(library);
    }

    private void Remove(PsxAssetLibrary library)
    {
        int index = Array.FindIndex(slots, entry => entry is BankEntry bank && ReferenceEquals(bank.Library, library));
        if (index >= 0) slots[index] = null;
    }

    internal bool Contains(PsxDynamicRegionLease lease) => lease.Slot < Capacity &&
        slots[lease.Slot] is DynamicEntry dynamic && ReferenceEquals(dynamic.Lease, lease);

    internal void Release(PsxDynamicRegionLease lease, Action releaseResources)
    {
        if (!Contains(lease)) return;
        // Original4836D0 clears Filename[0],Usable,Protected before the first geometry release callback.
        slots[lease.Slot] = null;
        releaseResources();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        unsubscribe?.Invoke();
        var failures = new List<Exception>();
        foreach (Entry? entry in slots)
            if (entry is DynamicEntry dynamic)
                try { dynamic.Lease.Dispose(); }
                catch (Exception error) { failures.Add(error); }
        Array.Clear(slots);
        if (failures.Count != 0) throw new AggregateException("Dynamic source region resource cleanup failed.", failures);
    }
}
