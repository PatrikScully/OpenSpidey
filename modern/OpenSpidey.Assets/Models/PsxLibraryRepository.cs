using OpenSpidey.Assets.Archives;

namespace OpenSpidey.Assets.Models;

/// <summary>Session-owned decoded libraries. The caller owns the source archive's lifetime.</summary>
public sealed class PsxLibraryRepository(PkrArchive archive)
{
    private readonly Dictionary<string, PsxAssetLibrary> loaded = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, PsxAssetLibrary> Loaded => loaded;
    public event Action<PsxAssetLibrary>? LibraryLoaded;
    public event Action<PsxAssetLibrary>? LibraryUnloaded;

    public bool Contains(string name) => archive.Contains(AssetPath(name));

    public PsxAssetLibrary Load(string name)
    {
        string path = AssetPath(name);
        if (loaded.TryGetValue(path, out PsxAssetLibrary? library)) return library;
        library = PsxAssetLibrary.Read(path, archive.Read(path));
        loaded.Add(path, library);
        try { LibraryLoaded?.Invoke(library); }
        catch (Exception publicationFailure)
        {
            // A subscriber may already have reserved its source region when a later subscriber rejects publication.
            // Roll back only this exact cached generation, preserving any replacement installed by a callback.
            if (!loaded.TryGetValue(path, out PsxAssetLibrary? current) || !ReferenceEquals(current, library)) throw;
            loaded.Remove(path);
            var cleanupFailures = new List<Exception>();
            foreach (Delegate callback in LibraryUnloaded?.GetInvocationList() ?? [])
                try { ((Action<PsxAssetLibrary>)callback)(library); }
                catch (Exception cleanupFailure) { cleanupFailures.Add(cleanupFailure); }
            if (cleanupFailures.Count != 0)
                throw new AggregateException("Source library publication and rollback failed.", new[] { publicationFailure }.Concat(cleanupFailures));
            throw;
        }
        return library;
    }

    public PsxAssetLibrary RequireLoaded(string name)
    {
        string path = AssetPath(name);
        return loaded.TryGetValue(path, out PsxAssetLibrary? library) ? library
            : throw new InvalidOperationException($"Library {path} must be loaded before it is selected.");
    }

    public void Unload(string name)
    {
        if (loaded.Remove(AssetPath(name), out PsxAssetLibrary? library)) LibraryUnloaded?.Invoke(library);
    }

    public static string AssetPath(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string path = name.Replace('\\', '/');
        if (!path.StartsWith("data/", StringComparison.OrdinalIgnoreCase)) path = "data/" + path;
        if (!path.EndsWith(".psx", StringComparison.OrdinalIgnoreCase)) path += ".psx";
        return path.ToLowerInvariant();
    }
}
