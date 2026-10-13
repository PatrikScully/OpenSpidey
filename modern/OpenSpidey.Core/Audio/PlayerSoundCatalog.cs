using System.Text.Json;

namespace OpenSpidey.Core.Audio;

public static class PlayerSoundCatalog
{
    public static IReadOnlyList<string> Names { get; } = ReadNames();

    public static string ArchivePath(int assetIndex) => assetIndex >= 0 && assetIndex < Names.Count
        ? "data/audio/" + Names[assetIndex] + ".wav" : throw new InvalidDataException($"Player sound asset {assetIndex} is outside its PC WAV group.");

    public static float Decibels(int sourceVolume) => SoundVolume.Decibels(sourceVolume);

    private static IReadOnlyList<string> ReadNames() => Array.AsReadOnly(Read<string?[]>("player-sound-names.json")
        .TakeWhile(name => name is not null).OfType<string>().Where(name => name.Length != 0).ToArray());
    private static T Read<T>(string name)
    {
        using Stream stream = typeof(PlayerSoundCatalog).Assembly.GetManifestResourceStream("OpenSpidey.Core.Audio." + name)!;
        return JsonSerializer.Deserialize<T>(stream) ?? throw new InvalidDataException($"Player sound metadata {name} is missing.");
    }
}
