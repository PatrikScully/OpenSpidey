using System.Text.Json;

namespace OpenSpidey.Core.Audio;

public sealed record SpeechClip(int Index, string Name, bool Available)
{
    public int Bank => Index / 16;
    public int Slot => Index % 16;
    public bool HasMedia => !Name.Equals("blank.bik", StringComparison.OrdinalIgnoreCase);
    public string ArchivePath => "data/Voice/" + Name;
    public string ConvertedName => "voice/" + Path.GetFileNameWithoutExtension(Name).ToLowerInvariant() + ".wav";
}

/// <summary>The recovered PC filename table and independent XA availability mask.</summary>
public static class SpeechCatalog
{
    private sealed record CatalogData(string[] Names, bool[] Available);
    public static IReadOnlyList<SpeechClip> Clips { get; } = Read();
    public static SpeechClip? Get(int bank, int slot)
    {
        if (bank is < 0 or >= 79) return null;
        long index = bank * 16L + slot;
        // The PC routine indexes linearly; slots can address an adjacent bank when the result remains bounded.
        return index >= 0 && index < Clips.Count ? Clips[(int)index] : null;
    }

    private static IReadOnlyList<SpeechClip> Read()
    {
        using Stream stream = typeof(SpeechCatalog).Assembly.GetManifestResourceStream("OpenSpidey.Core.Audio.speech-catalog.json")!;
        CatalogData data = JsonSerializer.Deserialize<CatalogData>(stream) ?? throw new InvalidDataException("Speech catalog is missing.");
        if (data.Names.Length != 1264 || data.Available.Length != 1264)
            throw new InvalidDataException("Speech catalog must contain 79 banks of sixteen slots.");
        return Array.AsReadOnly(data.Names.Select((name, index) => new SpeechClip(index, name, data.Available[index])).ToArray());
    }
}
