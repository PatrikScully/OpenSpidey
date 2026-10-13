using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenSpidey.Assets.Textures;

/// <summary>Names emitted in the original level .tex manifests, associated with the real packed PSX texture checksums.</summary>
public static class NamedTextureTable
{
    /// <summary>Spool_FindTextureEntry's first active manifest entry, including checksums printed without leading zeroes.</summary>
    public static uint FindFirst(ReadOnlySpan<byte> manifest, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        foreach (string line in Encoding.Latin1.GetString(manifest).Split('\n'))
        {
            Match entry = Regex.Match(line, @"^\s*0x([0-9a-fA-F]{1,8})\s+(.+?)\s*:\s*\d+x\d+\s+pixels");
            if (!entry.Success) continue;
            string path = entry.Groups[2].Value.Replace('\\', '/').TrimEnd();
            if (path[(path.LastIndexOf('/') + 1)..].Equals(name, StringComparison.OrdinalIgnoreCase))
                return uint.Parse(entry.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        throw new KeyNotFoundException($"Original texture {name} is absent from the selected level manifest.");
    }

    public static uint Find(ReadOnlySpan<byte> manifest, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        uint? found = null;
        foreach (string line in Encoding.Latin1.GetString(manifest).Split('\n'))
        {
            Match entry = Regex.Match(line, @"^\s*0x([0-9a-fA-F]{8})\s+(.+?)\s*:\s*\d+x\d+\s+pixels");
            if (!entry.Success) continue;
            string path = entry.Groups[2].Value.Replace('\\', '/').TrimEnd();
            if (!path[(path.LastIndexOf('/') + 1)..].Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            uint checksum = uint.Parse(entry.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (found is uint previous && previous != checksum) throw new InvalidDataException($"Original texture name {name} is ambiguous in this level manifest.");
            found = checksum;
        }
        return found ?? throw new KeyNotFoundException($"Original texture {name} is absent from the selected level manifest.");
    }
}
