using System.Collections.Frozen;

namespace OpenSpidey.Core.Cinematics;

public sealed record MovieDefinition(byte Track, string Name, int Width, int Height, int EndFrame)
{
    public string ArchivePath => $"data/movie/{Name}.bik";
    public string ConvertedName => Name + ".ogv";
}

public sealed record CinemaDefinition(ushort Id, byte MovieTrack, string? CoverName);

/// <summary>Recovered movie table and Cinema_ComicBookRun dispatch, independent of a video backend.</summary>
public static class CinemaCatalog
{
    public static IReadOnlyList<MovieDefinition> Movies { get; } = Array.AsReadOnly(new MovieDefinition[]
    {
        new(0, "ATVILOGO", 320, 240, 208), new(1, "NEVERSOFT", 320, 192, 239),
        new(2, "TREYARCH", 320, 240, 119), new(3, "GRAYMATT", 320, 192, 239),
        new(4, "L1M1", 320, 192, 2787), new(5, "L1M2", 320, 192, 604),
        new(6, "L2M1", 320, 192, 218), new(7, "L2M2", 320, 192, 432),
        new(8, "L2M3", 320, 192, 1009), new(9, "L3M1", 320, 192, 1950),
        new(10, "L4M1", 320, 192, 334), new(11, "L4M2", 320, 192, 685),
        new(12, "L5M1", 320, 192, 670), new(13, "L5M2", 320, 192, 182),
        new(14, "L5M3", 320, 192, 226), new(15, "L5M4", 320, 192, 185),
        new(16, "L6M1", 320, 192, 1629), new(17, "L7M1", 320, 192, 502),
        new(18, "L7M2", 320, 192, 560), new(19, "L7M3", 320, 192, 935),
        new(20, "L8M1", 320, 192, 611), new(21, "L8M2", 320, 192, 942),
        new(22, "L8M3", 320, 192, 395), new(23, "L8M4", 320, 192, 369),
        new(24, "L8M5", 320, 192, 2378)
    });

    public static IReadOnlyDictionary<ushort, CinemaDefinition> Cinemas { get; } = new CinemaDefinition[]
    {
        new(11, 4, "L1Cov"), new(12, 5, null), new(21, 6, "L2Cov"), new(22, 7, null),
        new(23, 8, null), new(31, 9, null), new(41, 10, "L4Cov"), new(42, 11, null),
        new(51, 12, "L5Cov"), new(52, 13, null), new(53, 14, null), new(54, 15, null),
        new(61, 16, null), new(71, 17, "L7Cov"), new(72, 18, null), new(73, 19, null),
        new(81, 20, "L8Cov"), new(82, 21, null), new(83, 22, null), new(84, 23, null), new(85, 24, null)
    }.ToFrozenDictionary(cinema => cinema.Id);

    // SOFDEC and LEGAL are zero-sized table placeholders, absent from the PC media archive.
    public static MovieDefinition GetMovie(byte track) => track < Movies.Count
        ? Movies[track] : throw new ArgumentOutOfRangeException(nameof(track), track, "No PC movie exists for this track.");
}
