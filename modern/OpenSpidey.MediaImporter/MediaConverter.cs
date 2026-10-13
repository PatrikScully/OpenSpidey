using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using OpenSpidey.Assets.Archives;

namespace OpenSpidey.MediaImporter;

internal enum MediaKind { Movie, Cover, Voice }

/// <summary>Converts owner-supplied archive media into a private, integrity-checked Godot cache.</summary>
internal sealed class MediaConverter(string cacheDirectory, string ffmpeg, bool reportCached = true)
{
    private const int RecipeVersion = 1;
    private sealed record ConversionStamp(int Recipe, uint SourceChecksum, string OutputSha256);

    public void Convert(PkrArchive archive, string source, string destinationName, MediaKind kind)
    {
        // Read validates decoded length and CRC even when the conversion is already cached.
        byte[] bytes = archive.Read(source);
        uint checksum = archive.Entries.Single(entry => entry.Path.Equals(source, StringComparison.OrdinalIgnoreCase)).Checksum;
        Directory.CreateDirectory(cacheDirectory);
        string destination = Path.Combine(cacheDirectory, destinationName);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string stampPath = destination + ".json";
        if (IsCurrent(destination, stampPath, checksum))
        {
            if (reportCached) Console.WriteLine($"Verified cached media: {destinationName}");
            return;
        }
        string temporary = Path.Combine(cacheDirectory, ".convert-" + Guid.NewGuid());
        Directory.CreateDirectory(temporary);
        try
        {
            string input = Path.Combine(temporary, Path.GetFileName(source));
            string output = Path.Combine(temporary, destinationName);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllBytes(input, bytes);
            var start = new ProcessStartInfo(ffmpeg) { UseShellExecute = false };
            foreach (string argument in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-i", input })
                start.ArgumentList.Add(argument);
            string[] options = kind switch
            {
                MediaKind.Movie => ["-map", "0:v:0", "-map", "0:a:0?", "-c:v", "libtheora", "-q:v", "10", "-pix_fmt", "yuv420p",
                    "-c:a", "libvorbis", "-q:a", "6", "-f", "ogg"],
                MediaKind.Cover => ["-frames:v", "1", "-update", "1"],
                MediaKind.Voice => ["-map", "0:a:0", "-vn", "-c:a", "pcm_s16le", "-f", "wav"],
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            foreach (string argument in options) start.ArgumentList.Add(argument);
            start.ArgumentList.Add(output);
            Console.WriteLine($"Converting {source} to {destinationName}");
            using Process process = Process.Start(start) ?? throw new IOException("Could not start FFmpeg.");
            process.WaitForExit();
            if (process.ExitCode != 0 || !File.Exists(output) || new FileInfo(output).Length == 0)
                throw new IOException($"FFmpeg failed for {source} (exit {process.ExitCode}).");
            string outputHash = HashFile(output);
            string stamp = Path.Combine(temporary, "stamp.json");
            File.WriteAllText(stamp, JsonSerializer.Serialize(new ConversionStamp(RecipeVersion, checksum, outputHash)));
            File.Move(output, destination, overwrite: true);
            File.Move(stamp, stampPath, overwrite: true);
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }

    private static bool IsCurrent(string destination, string stampPath, uint checksum)
    {
        if (!File.Exists(destination) || !File.Exists(stampPath)) return false;
        ConversionStamp? stamp;
        try { stamp = JsonSerializer.Deserialize<ConversionStamp>(File.ReadAllText(stampPath)); }
        catch (JsonException) { return false; }
        return stamp is { Recipe: RecipeVersion } && stamp.SourceChecksum == checksum && stamp.OutputSha256 == HashFile(destination);
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return System.Convert.ToHexString(SHA256.HashData(stream));
    }
}
