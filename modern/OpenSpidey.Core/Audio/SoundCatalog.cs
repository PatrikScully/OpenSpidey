using System.Text.Json;

namespace OpenSpidey.Core.Audio;

public sealed record SoundGroup(string Name, int Group, string?[] Names)
{
    public IReadOnlyList<string> Assets { get; } = Array.AsReadOnly(Names.TakeWhile(name => name is not null)
        .OfType<string>().Where(name => name.Length != 0).ToArray());
    public string ArchivePath(int assetIndex) => assetIndex >= 0 && assetIndex < Assets.Count
        ? "data/audio/" + Assets[assetIndex] + ".wav" : throw new InvalidDataException($"Sound asset {assetIndex} exceeds group {Name}.");
}

/// <summary>Recovered PC WAV groups, separate from the level's sound-definition and alias table.</summary>
public static class SoundCatalog
{
    public static IReadOnlyList<SoundGroup> Groups { get; } = Read();
    public static SoundGroup Find(string name)
    {
        string key = Path.GetFileNameWithoutExtension(name.Replace('\\', '/')).ToLowerInvariant();
        if (key.Length == 4 && key.StartsWith("dem") && key[3] is >= '1' and <= '4') key = "lea" + key[3];
        if (key.Length == 4 && key.StartsWith("l4a")) key = "l4a1";
        return Groups.FirstOrDefault(group => group.Name == key)
            ?? throw new NotSupportedException($"PC sound group {name} has no recovered group selector.");
    }
    private static IReadOnlyList<SoundGroup> Read()
    {
        using Stream stream = typeof(SoundCatalog).Assembly.GetManifestResourceStream("OpenSpidey.Core.Audio.sound-groups.json")!;
        return Array.AsReadOnly(JsonSerializer.Deserialize<SoundGroup[]>(stream) ?? throw new InvalidDataException("Sound groups are missing."));
    }
}
