using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;

internal static class PsxAssetRegionTests
{
    public static void Run(Action<bool, string> assert)
    {
        var loaded = new Dictionary<string, PsxAssetLibrary>(StringComparer.OrdinalIgnoreCase);
        using var regions = new PsxAssetRegions(loaded);
        var blankScene = new PsxScene(0x20004, [], [], []);
        for (int index = 0; index < PsxAssetRegions.Capacity; index++)
        {
            var bank = new PsxAssetLibrary($"data/bank{index}.psx", blankScene, [], null);
            loaded.Add(bank.AssetPath, bank);
            assert(regions.RequireRegion(bank) == index && ReferenceEquals(regions.Resolve((byte)index), bank),
                "The original first-free region sequence retains each actual loaded bank identity through all forty slots.");
        }
        PsxAssetLibrary old = regions.Resolve(7);
        loaded.Remove(old.AssetPath);
        regions.Synchronize();
        Reject<InvalidDataException>(() => regions.Resolve(7));
        var replacement = new PsxAssetLibrary(old.AssetPath, blankScene, [], null);
        loaded.Add(replacement.AssetPath, replacement);
        assert(regions.RequireRegion(replacement) == 7 && !ReferenceEquals(old, regions.Resolve(7)),
            "A reloaded bank occupies the released source slot while its old generation remains invalid.");
        Reject<InvalidOperationException>(() => regions.RequireRegion(old));
        var excess = new PsxAssetLibrary("data/excess.psx", blankScene, [], null);
        loaded.Add(excess.AssetPath, excess);
        Reject<InvalidOperationException>(() => regions.Synchronize());
        loaded.Remove(excess.AssetPath);
        var removed = new List<PsxAssetLibrary>();
        PsxAssetLibrary protectedBank = regions.Resolve(3);
        regions.SetProtected(protectedBank);
        PsxAssetLibrary[] expectedClear = Enumerable.Range(0, PsxAssetRegions.Capacity).Where(index => index != 3)
            .Select(index => regions.Resolve((byte)index)).ToArray();
        regions.ClearUnprotected(bank => { removed.Add(bank); loaded.Remove(bank.AssetPath); });
        assert(removed.SequenceEqual(expectedClear) && regions.Count == 1 &&
            ReferenceEquals(regions.Resolve(3), protectedBank) && regions.IsProtected(protectedBank),
            "ClearAllPSXs preserves protected generations and unloads every other source region in slot order, including a reused slot.");
        regions.ClearUnprotected(bank => throw new InvalidOperationException("Protected bank was selected for unload."));
        assert(regions.Count == 1, "Repeated source cleanup retains protected banks without another destructor.");
        loaded.Remove(protectedBank.AssetPath);
        var newGeneration = protectedBank with { };
        loaded.Add(newGeneration.AssetPath, newGeneration);
        assert(regions.RequireRegion(newGeneration) == 0 && !regions.IsProtected(newGeneration),
            "Protection belongs to the actual region generation and does not leak into a reloaded bank.");
        regions.Dispose();
        Reject<ObjectDisposedException>(() => regions.Resolve(0));

        void Reject<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { assert(true, $"Source bank ownership rejects {typeof(T).Name}."); return; }
            throw new Exception($"Expected {typeof(T).Name} for invalid source bank ownership.");
        }
    }

    public static void RunOriginalAssets(PkrArchive archive, Action<bool, string> assert)
    {
        if (!archive.Contains("data/spidey.psx")) return;
        var repository = new PsxLibraryRepository(archive);
        var loadedEvents = new List<PsxAssetLibrary>();
        var unloadedEvents = new List<PsxAssetLibrary>();
        repository.LibraryLoaded += loadedEvents.Add;
        repository.LibraryUnloaded += unloadedEvents.Add;
        using var regions = new PsxAssetRegions(repository);
        string[] names = ["spidey", "bits", "items", "expgrnd", "henchman", "thug", "hostage", "hostagef", "L1A1_O", "L1A1_L"];
        PsxAssetLibrary[] originals = names.Select(repository.Load).ToArray();
        for (int index = 0; index < originals.Length; index++)
            assert(regions.RequireRegion(originals[index]) == index,
                "Actual original decoded banks receive source slots in the observed Load mutation order.");
        assert(loadedEvents.SequenceEqual(originals) && ReferenceEquals(repository.Load("DATA/SPIDEY.PSX"), originals[0]) &&
            loadedEvents.Count == names.Length,
            "Cached and case-normalized requests retain the original source generation without extra construction notifications.");
        repository.Unload(names[0]);
        repository.Unload(names[3]);
        PsxAssetLibrary ring = repository.Load("ring");
        PsxAssetLibrary shell = repository.Load("shell_dc");
        // Do not inspect between the loads: event order must beat Dictionary's reused bucket order.
        assert(regions.RequireRegion(ring) == 0 && regions.RequireRegion(shell) == 3 &&
            unloadedEvents.SequenceEqual(new[] { originals[0], originals[3] }),
            "Typed real Load/Unload events preserve first-free assignment across multiple mutations before the next region lookup.");
        repository.Unload("ring");
        PsxAssetLibrary reloaded = repository.Load("spidey");
        assert(regions.RequireRegion(reloaded) == 0 && !ReferenceEquals(reloaded, originals[0]),
            "An actual original bank reload receives a new generation in the first released slot.");
        try { regions.RequireRegion(originals[0]); throw new Exception("Stale original bank generation resolved."); }
        catch (InvalidOperationException) { assert(true, "Old decoded player bank generation cannot resolve after native source unload/reload."); }
        regions.Dispose();
        repository.Load("costarm");
        assert(loadedEvents.Last().AssetPath == "data/costarm.psx",
            "Disposing the region view unsubscribes its actual mutation callbacks without owning or stopping the repository.");
    }
}
