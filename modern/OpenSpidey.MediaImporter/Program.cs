using OpenSpidey.Assets.Archives;
using OpenSpidey.Core.Cinematics;
using OpenSpidey.Core.Audio;
using OpenSpidey.Assets.Scripting;
using System.Buffers.Binary;
using OpenSpidey.MediaImporter;

if (args.Length is < 2 or > 4)
{
    Console.Error.WriteLine("Usage: OpenSpidey.MediaImporter ORIGINAL_GAME_FOLDER CACHE_FOLDER [CINEMA_ID|all|level:MISSION|voices|voice:BANK:SLOT] [FFMPEG]");
    return 2;
}
try
{
    if (!Environment.Is64BitProcess) throw new PlatformNotSupportedException("A 64-bit runtime is required.");
    var converter = new MediaConverter(Path.GetFullPath(args[1]), args.Length == 4 ? args[3] : "ffmpeg",
        reportCached: args.Length < 3 || args[2] != "voices");
    if (args.Length >= 3 && (args[2] == "voices" || args[2].StartsWith("voice:", StringComparison.Ordinal)))
    {
        IEnumerable<SpeechClip> voices;
        if (args[2] == "voices") voices = SpeechCatalog.Clips.Where(clip => clip.HasMedia).DistinctBy(clip => clip.ConvertedName);
        else
        {
            string[] parts = args[2].Split(':');
            if (parts.Length != 3) throw new ArgumentException("Voice selection must be voice:BANK:SLOT.");
            SpeechClip clip = SpeechCatalog.Get(int.Parse(parts[1]), int.Parse(parts[2]))
                ?? throw new ArgumentException("Voice bank/slot is outside the catalog.");
            voices = clip.HasMedia ? [clip] : [];
        }
        using var voiceData = new PkrArchive(Path.Combine(args[0], "data.pkr"));
        var present = voiceData.Entries.Select(entry => entry.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] missing = SpeechCatalog.Clips.Where(clip => clip.HasMedia && !present.Contains(clip.ArchivePath))
            .Select(clip => clip.ArchivePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string manifest = Path.Combine(Path.GetFullPath(args[1]), "voice", "missing.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
        File.WriteAllText(manifest, System.Text.Json.JsonSerializer.Serialize(missing));
        foreach (SpeechClip clip in voices)
        {
            if (!present.Contains(clip.ArchivePath)) { Console.WriteLine($"Source voice absent: {clip.ArchivePath}"); continue; }
            converter.Convert(voiceData, clip.ArchivePath, clip.ConvertedName, MediaKind.Voice);
        }
        Console.WriteLine($"Voice media ready in {Path.GetFullPath(args[1])}");
        return 0;
    }
    IEnumerable<MovieDefinition> movies;
    IEnumerable<string> covers;
    if (args.Length < 3 || args[2] == "all")
    {
        movies = CinemaCatalog.Movies;
        covers = CinemaCatalog.Cinemas.Values.Select(cinema => cinema.CoverName).OfType<string>().Distinct();
    }
    else if (args[2].StartsWith("level:", StringComparison.Ordinal))
    {
        string mission = args[2][6..];
        if (mission.Length is 0 or > 9 || !mission.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
            throw new ArgumentException("Level media selection requires an authored mission name.");
        using var levelData = new PkrArchive(Path.Combine(args[0], "data.pkr"));
        TriggerFile triggers = TriggerFile.Read(levelData.Read($"data/{mission}.trg"));
        ushort[] ids = triggers.Nodes.Where(node => (TriggerNodeKind)node.TypeCode is TriggerNodeKind.Autoexec or
                TriggerNodeKind.AlternativeAutoexec or TriggerNodeKind.CommandPoint or TriggerNodeKind.Restart)
            .SelectMany(TriggerCommandReader.Read).Where(instruction => instruction.Opcode == 190)
            .Select(instruction => BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span)).Distinct().ToArray();
        CinemaDefinition[] cinemas = ids.Select(id => CinemaCatalog.Cinemas.TryGetValue(id, out var definition)
            ? definition : throw new InvalidDataException($"Mission {mission} references unknown cinema{id}.")).ToArray();
        movies = cinemas.Select(cinema => CinemaCatalog.GetMovie(cinema.MovieTrack)).DistinctBy(movie => movie.ConvertedName).ToArray();
        covers = cinemas.Select(cinema => cinema.CoverName).OfType<string>().Distinct().ToArray();
    }
    else
    {
        ushort id = ushort.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
        CinemaDefinition cinema = CinemaCatalog.Cinemas.TryGetValue(id, out var found)
            ? found : throw new ArgumentException($"Cinema {id} does not exist.");
        movies = [CinemaCatalog.GetMovie(cinema.MovieTrack)];
        covers = cinema.CoverName is { } cover ? [cover] : [];
    }
    using var media = new PkrArchive(Path.Combine(args[0], "media.pkr"));
    foreach (MovieDefinition movie in movies) converter.Convert(media, movie.ArchivePath, movie.ConvertedName, MediaKind.Movie);
    using var data = new PkrArchive(Path.Combine(args[0], "data.pkr"));
    foreach (string cover in covers) converter.Convert(data, $"data/{cover}.bmp", cover + ".png", MediaKind.Cover);
    Console.WriteLine($"Media ready in {Path.GetFullPath(args[1])}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
