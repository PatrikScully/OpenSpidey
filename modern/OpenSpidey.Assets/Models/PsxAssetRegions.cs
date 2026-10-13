namespace OpenSpidey.Assets.Models;

/// <summary>Live decoded bank generations in the original forty-slot, first-free source region table.</summary>
public sealed class PsxAssetRegions : IDisposable
{
    public const int Capacity = 40;
    private readonly PsxAssetLibrary?[] banks = new PsxAssetLibrary?[Capacity];
    private readonly bool[] protectedRegions = new bool[Capacity];
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

    public int Count
    {
        get
        {
            Synchronize();
            return banks.Count(bank => bank is not null);
        }
    }

    public byte RequireRegion(PsxAssetLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        Synchronize();
        int index = Array.FindIndex(banks, bank => ReferenceEquals(bank, library));
        return index >= 0 ? checked((byte)index) :
            throw new InvalidOperationException($"Source bank {library.AssetPath} is not the current loaded generation.");
    }

    public PsxAssetLibrary Resolve(byte region)
    {
        Synchronize();
        return region < Capacity && banks[region] is { } bank ? bank :
            throw new InvalidDataException($"Source region {region} has no usable loaded bank.");
    }

    public void SetProtected(PsxAssetLibrary library, bool value = true) =>
        protectedRegions[RequireRegion(library)] = value;

    public bool IsProtected(PsxAssetLibrary library) => protectedRegions[RequireRegion(library)];

    /// <summary>Spool_ClearAllPSXs visits all forty slots in order and preserves each protected bank generation.</summary>
    public void ClearUnprotected(Action<PsxAssetLibrary> unload)
    {
        ArgumentNullException.ThrowIfNull(unload);
        Synchronize();
        for (int index = 0; index < banks.Length; index++)
        {
            if (banks[index] is not { } library || protectedRegions[index]) continue;
            unload(library);
            Synchronize();
            if (ReferenceEquals(banks[index], library))
                throw new InvalidOperationException($"Source library {library.AssetPath} did not release its region during cleanup.");
        }
    }

    public void Synchronize()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (loaded.Count > Capacity)
            throw new InvalidOperationException($"The source region table supports at most {Capacity} simultaneously loaded banks.");
        for (int index = 0; index < banks.Length; index++)
            if (banks[index] is { } previous && (!loaded.TryGetValue(previous.AssetPath, out PsxAssetLibrary? current) ||
                !ReferenceEquals(previous, current)))
            { banks[index] = null; protectedRegions[index] = false; }
        foreach (PsxAssetLibrary library in loaded.Values)
            Register(library);
    }

    private void Register(PsxAssetLibrary library)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (banks.Any(bank => ReferenceEquals(bank, library))) return;
        int index = Array.FindIndex(banks, bank => bank is null);
        if (index < 0) throw new InvalidOperationException("No source asset region is available.");
        banks[index] = library;
        protectedRegions[index] = false;
    }

    private void Remove(PsxAssetLibrary library)
    {
        int index = Array.FindIndex(banks, bank => ReferenceEquals(bank, library));
        if (index >= 0) { banks[index] = null; protectedRegions[index] = false; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        unsubscribe?.Invoke();
        Array.Clear(banks);
        Array.Clear(protectedRegions);
    }
}
