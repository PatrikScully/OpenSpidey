using System.Collections.Frozen;
using System.IO.Compression;
using System.Text;

namespace OpenSpidey.Assets.Archives;

/// <summary>Reads original PKR3 archives as data, without pointer relocation or executable loading.</summary>
public sealed class PkrArchive : IDisposable
{
    private const int MaximumEntrySize = 256 * 1024 * 1024;
    private readonly FileStream stream;
    private readonly FrozenDictionary<string, PkrEntry> entries;
    private readonly object readLock = new();

    public IReadOnlyCollection<PkrEntry> Entries => entries.Values;

    public PkrArchive(string path)
    {
        stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try { entries = ReadIndex(); }
        catch { stream.Dispose(); throw; }
    }

    public byte[] Read(string path)
    {
        if (!entries.TryGetValue(NormalizePath(path), out PkrEntry? entry))
            throw new FileNotFoundException($"Archive entry not found: {path}", path);

        byte[] stored = new byte[entry.StoredSize];
        lock (readLock)
        {
            stream.Position = entry.Offset;
            stream.ReadExactly(stored);
        }

        byte[] decoded;
        switch (entry.Compression)
        {
            case -2:
                decoded = stored;
                break;
            case 2:
                decoded = new byte[entry.DecodedSize];
                using (var source = new MemoryStream(stored, writable: false))
                using (var inflater = new ZLibStream(source, CompressionMode.Decompress))
                {
                    inflater.ReadExactly(decoded);
                    if (inflater.ReadByte() != -1)
                        throw new InvalidDataException($"Entry exceeds declared size: {entry.Path}");
                }
                break;
            default:
                throw new NotSupportedException($"PKR compression {entry.Compression}: {entry.Path}");
        }

        if (decoded.Length != entry.DecodedSize || Crc32.Compute(decoded) != entry.Checksum)
            throw new InvalidDataException($"Entry size or CRC mismatch: {entry.Path}");
        return decoded;
    }

    public bool Contains(string path) => entries.ContainsKey(NormalizePath(path));

    private FrozenDictionary<string, PkrEntry> ReadIndex()
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        if (reader.ReadUInt32() != 0x33524b50)
            throw new InvalidDataException("Expected a PKR3 archive.");
        uint directoryOffset = reader.ReadUInt32();
        ValidateRange(directoryOffset, 12);
        stream.Position = directoryOffset;
        reader.ReadUInt32(); // Original archive alignment, irrelevant to read-only access.
        uint directoryCount = reader.ReadUInt32();
        uint fileCount = reader.ReadUInt32();
        ValidateRange((long)directoryOffset + 12, (long)directoryCount * 40 + (long)fileCount * 52);
        if (directoryCount > 65_536 || fileCount > 1_000_000)
            throw new InvalidDataException("Unreasonable archive directory size.");

        var directories = new List<(string Path, uint First, uint Count)>();
        for (uint index = 0; index < directoryCount; index++)
        {
            string directory = ReadName(reader);
            uint first = reader.ReadUInt32();
            uint count = reader.ReadUInt32();
            if ((ulong)first + count > fileCount)
                throw new InvalidDataException($"Invalid file range: {directory}");
            directories.Add((directory, first, count));
        }

        var files = new PkrEntry[fileCount];
        for (int index = 0; index < files.Length; index++)
        {
            string name = ReadName(reader);
            uint checksum = reader.ReadUInt32();
            int compression = reader.ReadInt32();
            uint offset = reader.ReadUInt32();
            uint decodedSize = reader.ReadUInt32();
            uint storedSize = reader.ReadUInt32();
            if (decodedSize > MaximumEntrySize || storedSize > MaximumEntrySize)
                throw new InvalidDataException($"Archive entry too large: {name}");
            ValidateRange(offset, storedSize);
            files[index] = new(name, checksum, compression, offset, (int)decodedSize, (int)storedSize);
        }

        var result = new Dictionary<string, PkrEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in directories)
        {
            for (uint index = directory.First; index < directory.First + directory.Count; index++)
            {
                PkrEntry file = files[index];
                string path = NormalizePath(directory.Path + "/" + file.Path);
                if (!result.TryAdd(path, file with { Path = path }))
                    throw new InvalidDataException($"Duplicate archive entry: {path}");
            }
        }
        if (result.Count != files.Length)
            throw new InvalidDataException("Archive directory ranges overlap or omit entries.");
        return result.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static string ReadName(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(32);
        if (bytes.Length != 32) throw new EndOfStreamException();
        int terminator = Array.IndexOf(bytes, (byte)0);
        return Encoding.ASCII.GetString(bytes, 0, terminator < 0 ? bytes.Length : terminator);
    }

    private void ValidateRange(long offset, long length)
    {
        if (offset < 0 || length < 0 || offset > stream.Length || length > stream.Length - offset)
            throw new InvalidDataException("Archive range extends beyond the file.");
    }

    private static string NormalizePath(string path)
    {
        string[] parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part is "." or ".." || part.Contains(':')))
            throw new InvalidDataException($"Invalid archive path: {path}");
        return string.Join('/', parts);
    }

    public void Dispose() { lock (readLock) stream.Dispose(); }
}
