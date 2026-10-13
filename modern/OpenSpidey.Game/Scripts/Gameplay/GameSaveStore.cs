using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using FileAccess = System.IO.FileAccess;

namespace OpenSpidey.Game.Gameplay;

/// <summary>Bounded private user save storage with flushed sibling-file replacement; never stores game assets.</summary>
public sealed class GameSaveStore
{
    private const int MaximumSaveBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public string UserPath { get; }
    public string NativePath { get; }

    public GameSaveStore(string userPath = "user://saves/progress.json")
    {
        ArgumentException.ThrowIfNullOrEmpty(userPath);
        if (!userPath.StartsWith("user://", StringComparison.Ordinal) ||
            userPath[7..].Split('/').Any(part => part.Length == 0 || part is "." or ".." || part.Contains(':') || part.Contains('\\')))
            throw new ArgumentException("A game save must have a private user:// path.", nameof(userPath));
        UserPath = userPath;
        NativePath = ProjectSettings.GlobalizePath(userPath);
    }

    public async Task<GameSaveDocument?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(NativePath)) return null;
        await using var file = new FileStream(NativePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length is <= 0 or > MaximumSaveBytes) throw new InvalidDataException("The private game save has an invalid bounded size.");
        byte[] bytes = new byte[checked((int)file.Length)];
        await file.ReadExactlyAsync(bytes, cancellationToken);
        try
        {
            GameSaveDocument document = JsonSerializer.Deserialize<GameSaveDocument>(bytes, JsonOptions)
                ?? throw new InvalidDataException("The private game save is empty.");
            document.Validate();
            return document;
        }
        catch (JsonException exception) { throw new InvalidDataException("The private game save is malformed or incomplete.", exception); }
    }

    public async Task SaveAsync(GameSaveDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        if (bytes.Length > MaximumSaveBytes) throw new InvalidDataException("The game save exceeds its bounded source fields.");
        cancellationToken.ThrowIfCancellationRequested();
        string directory = Path.GetDirectoryName(NativePath) ?? throw new InvalidDataException("The private game save has no directory.");
        Directory.CreateDirectory(directory);
        string temporary = NativePath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await file.WriteAsync(bytes, cancellationToken);
                await file.FlushAsync(cancellationToken);
                file.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, NativePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
