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
        RunDynamicRegions(assert);

        void Reject<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { assert(true, $"Source bank ownership rejects {typeof(T).Name}."); return; }
            throw new Exception($"Expected {typeof(T).Name} for invalid source bank ownership.");
        }
    }

    private static void RunDynamicRegions(Action<bool, string> assert)
    {
        var loaded = new Dictionary<string, PsxAssetLibrary>(StringComparer.OrdinalIgnoreCase);
        var blank = new PsxScene(0x20004, [], [], []);
        using var regions = new PsxAssetRegions(loaded);
        var events = new List<string>();
        PsxDynamicRegionLease? primary = null, duringRelease = null;
        primary = regions.AllocateDynamic("S", () =>
        {
            events.Add("release:S");
            assert(!primary!.IsActive && regions.Count == 0,
                "Original4836D0 makes the primary region free and unusable before its first resource release callback.");
            duringRelease = regions.AllocateDynamic("NEW", () => events.Add("release:NEW"));
            assert(duringRelease.Slot == primary.Slot && duringRelease.IsActive,
                "A release callback can reserve the just-freed source slot as a distinct live generation.");
        });
        var bank = new PsxAssetLibrary("data/shared.psx", blank, [], null);
        loaded.Add(bank.AssetPath, bank);
        assert(primary.Slot == 0 && regions.RequireRegion(bank) == 1 && regions.Count == 2 && regions.IsProtected(primary),
            "A private protected S region and actual loaded bank share one first-free table and remain distinct typed occupants.");
        Reject<InvalidDataException>(() => regions.Resolve(primary.Slot));
        Reject<InvalidDataException>(() => regions.ResolveDynamic(1));
        using var foreign = new PsxAssetRegions(new Dictionary<string, PsxAssetLibrary>());
        Reject<InvalidOperationException>(() => foreign.RequireRegion(primary));
        regions.ClearUnprotected(library => { events.Add("unload:" + library.AssetPath); loaded.Remove(library.AssetPath); });
        assert(primary.IsActive && regions.Count == 1 && events.SequenceEqual(new[] { "unload:data/shared.psx" }),
            "Spool_ClearAllPSXs unloads bank generations while preserving protected private source regions.");
        primary.Dispose(); primary.Dispose();
        assert(duringRelease is { IsActive: true } && ReferenceEquals(regions.ResolveDynamic(0), duringRelease),
            "Repeated disposal of the stale primary lease cannot free a replacement reserved inside the destructor callback.");
        Reject<InvalidOperationException>(() => regions.RequireRegion(primary));
        regions.SetProtected(duringRelease!, false);
        regions.ClearUnprotected(_ => throw new Exception("A dynamic slot must not masquerade as an archive bank."));
        assert(duringRelease is { IsActive: false } && regions.Count == 0 && events.Last() == "release:NEW",
            "An explicitly unprotected private slot invokes its own resource owner and frees the same shared table slot.");

        PsxDynamicRegionLease? failed = null;
        int failureReleases = 0;
        try
        {
            failed = regions.AllocateDynamic("FAIL", () => failureReleases++);
            throw new InitializationFailure();
        }
        catch (InitializationFailure) { failed!.Dispose(); }
        assert(failureReleases == 1 && !failed!.IsActive && regions.Count == 0,
            "Failure after slot reservation releases its exact generation and partial resource callback once.");
        var slots = new List<PsxDynamicRegionLease>();
        for (int index = 0; index < PsxAssetRegions.Capacity; index++)
        {
            int captured = index;
            slots.Add(regions.AllocateDynamic("SW", () => events.Add("release:" + captured)));
            assert(slots[^1].Slot == index, "Dynamic geometry regions consume the same source slots0..39 without a disconnected allocator.");
        }
        int unacceptedReleases = 0;
        Reject<InvalidOperationException>(() => regions.AllocateDynamic("FULL", () => unacceptedReleases++));
        assert(regions.Count == 40 && unacceptedReleases == 0 && slots.All(lease => lease.IsActive),
            "Capacity exhaustion cannot steal an occupied dynamic generation or accept ownership of an unreserved resource callback.");
        slots[17].Dispose();
        var replacement = regions.AllocateDynamic("S", () => events.Add("replacement:17"));
        slots[17].Dispose();
        assert(replacement.Slot == 17 && replacement.IsActive && !slots[17].IsActive,
            "Reusing a dynamic source slot establishes a new exact lease identity, immune to stale disposal.");
        Reject<ArgumentException>(() => regions.AllocateDynamic("TOO-LONG9", () => { }));
        regions.Dispose();
        assert(slots.All(lease => !lease.IsActive) && !replacement.IsActive &&
            events.TakeLast(40).SequenceEqual(Enumerable.Range(0, 40).Select(index => index == 17 ? "replacement:17" : "release:" + index)),
            "Directory destruction releases all owned dynamic regions in actual slot order, including protected and replacement generations.");
        regions.Dispose(); replacement.Dispose();
        Reject<ObjectDisposedException>(() => regions.RequireRegion(replacement));

        var cleanup = new PsxAssetRegions(new Dictionary<string, PsxAssetLibrary>());
        var first = cleanup.AllocateDynamic("S", () => throw new InitializationFailure());
        bool finalReleased = false;
        var last = cleanup.AllocateDynamic("SW", () => finalReleased = true);
        Reject<AggregateException>(cleanup.Dispose);
        cleanup.Dispose();
        assert(!first.IsActive && !last.IsActive && finalReleased,
            "A failing native resource cleanup cannot strand later private slots or leave stale leases active during directory teardown.");

        void Reject<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { assert(true, $"Typed source slot ownership rejects {typeof(T).Name}."); return; }
            throw new Exception($"Expected {typeof(T).Name} for invalid dynamic region ownership.");
        }
    }

    private sealed class InitializationFailure : Exception { }

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
        if (archive.Contains("data/scorpion.psx"))
        {
            using var shared = new PsxAssetRegions(repository);
            int releases = 0;
            using PsxDynamicRegionLease tail = shared.AllocateDynamic("S", () => releases++);
            PsxAssetLibrary scorpion = repository.Load("scorpion");
            assert(shared.RequireRegion(scorpion) == tail.Slot + 1 && shared.RequireRegion(tail) == tail.Slot && shared.IsProtected(tail),
                "Actual source repository Load events skip the first-free slot already reserved by a protected dynamic tail generation.");
            repository.Unload("scorpion");
            PsxAssetLibrary fresh = repository.Load("scorpion");
            assert(shared.RequireRegion(fresh) == tail.Slot + 1 && !ReferenceEquals(fresh, scorpion) && tail.IsActive,
                "Actual source bank unload/reload reuses its released slot while preserving the independent dynamic tail generation.");
            tail.Dispose();
            assert(releases == 1 && shared.RequireRegion(fresh) == tail.Slot + 1 && ReferenceEquals(shared.Resolve((byte)(tail.Slot + 1)), fresh),
                "A real dynamic-tail release does not unload or relocate the separately owned genuine Scorpion library generation.");
            VerifyFailedPublication(archive, assert);
        }
    }

    private static void VerifyFailedPublication(PkrArchive archive, Action<bool, string> assert)
    {
        var repository = new PsxLibraryRepository(archive);
        PsxAssetLibrary? attempted = null;
        repository.LibraryLoaded += bank => attempted = bank;
        // The first table accepts the decoded generation before the full second table rejects it.
        using var accepting = new PsxAssetRegions(repository);
        using var full = new PsxAssetRegions(repository);
        PsxDynamicRegionLease[] reserved = Enumerable.Range(0, 40).Select(_ => full.AllocateDynamic("S", () => { })).ToArray();
        bool rejected = false;
        try { repository.Load("Scorpion"); }
        catch (InvalidOperationException) { rejected = true; }
        PsxAssetLibrary failed = attempted ?? throw new Exception("Failed publication did not decode its genuine source generation.");
        assert(rejected && repository.Loaded.Count == 0 && accepting.Count == 0 && full.Count == 40 && reserved.All(lease => lease.IsActive),
            "A bank-load rejection at a full shared table must remove its exact cached generation and roll back the earlier subscriber's already allocated region.");
        reserved[17].Dispose();
        PsxAssetLibrary retry = repository.Load("Scorpion");
        assert(!ReferenceEquals(failed, retry) && ReferenceEquals(repository.RequireLoaded("Scorpion"), retry) &&
            accepting.RequireRegion(retry) == 0 && full.RequireRegion(retry) == 17,
            "A failed bank publication must leave the repository and each subscriber usable for a fresh real generation in the released source slot.");

        var replacements = new PsxLibraryRepository(archive);
        using var replacementRegions = new PsxAssetRegions(replacements);
        bool replacing = false;
        PsxAssetLibrary? preserved = null;
        replacements.LibraryLoaded += bank =>
        {
            if (replacing) return;
            replacing = true;
            replacements.Unload(bank.AssetPath);
            preserved = replacements.Load(bank.AssetPath);
            throw new InitializationFailure();
        };
        bool interrupted = false;
        try { replacements.Load("Scorpion"); }
        catch (InitializationFailure) { interrupted = true; }
        assert(interrupted && preserved is not null && ReferenceEquals(replacements.RequireLoaded("Scorpion"), preserved) &&
            replacementRegions.Count == 1 && ReferenceEquals(replacementRegions.Resolve(0), preserved),
            "Failed outer publication cannot remove the replacement generation explicitly installed through a subscriber's source unload/reload callbacks.");

        var cleanup = new PsxLibraryRepository(archive);
        cleanup.LibraryUnloaded += _ => throw new InitializationFailure();
        using var cleanupRegions = new PsxAssetRegions(cleanup);
        cleanup.LibraryLoaded += _ => throw new InitializationFailure();
        bool aggregated = false;
        try { cleanup.Load("Scorpion"); }
        catch (AggregateException error) { aggregated = error.InnerExceptions.Count == 2; }
        assert(aggregated && cleanup.Loaded.Count == 0 && cleanupRegions.Count == 0,
            "Rollback invokes every subscriber even when one destructor callback fails, preserving both errors and clearing later subscribers' cached region state.");
    }
}
