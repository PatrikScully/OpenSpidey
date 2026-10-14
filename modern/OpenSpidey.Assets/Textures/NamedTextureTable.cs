using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenSpidey.Assets.Textures;

/// <summary>Names emitted in the original level .tex manifests, associated with the real packed PSX texture checksums.</summary>
public static class NamedTextureTable
{
    /// <summary>texLoadChecksums' %lx %s token, lowercase basename after its last backslash, and first active name match.</summary>
    public static uint FindFirstSourceName(ReadOnlySpan<byte> manifest, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length >= 32) throw new NotSupportedException("Original texture names of 32 bytes or more have no bounded terminator in the source table.");
        string text = Encoding.Latin1.GetString(manifest);
        int terminator = text.IndexOf('\0');
        if (terminator >= 0) text = text[..terminator];
        for (int cursor = 0; cursor >= 0; cursor = NextLine(cursor))
        {
            // sscanf sees the remaining buffer, so its ASCII whitespace can cross a line boundary.
            Match entry = Regex.Match(text[cursor..], @"^[\x09-\x0D ]*(?:0[xX])?([0-9a-fA-F]{1,8})[\x09-\x0D ]+([^\x00\x09-\x0D ]+)");
            if (!entry.Success) continue;
            string token = entry.Groups[2].Value;
            int separator = token.LastIndexOf('\\');
            if (separator < 0) throw new InvalidDataException("The original texture-name loader requires a Windows path with a backslash.");
            string basename = token[(separator + 1)..];
            if (basename.Length >= 32) continue;
            if (basename.Equals(name, StringComparison.OrdinalIgnoreCase))
                return uint.Parse(entry.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        throw new KeyNotFoundException($"Original source texture name {name} is absent from the selected level manifest.");

        int NextLine(int cursor)
        {
            // GetNextLine searches CR and optionally consumes the following LF; lone LF does not begin another entry.
            int carriageReturn = text.IndexOf('\r', cursor);
            if (carriageReturn < 0) return -1;
            int next = carriageReturn + 1;
            return next < text.Length && text[next] == '\n' ? next + 1 : next;
        }
    }

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
