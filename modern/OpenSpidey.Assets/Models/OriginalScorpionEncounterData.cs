using OpenSpidey.Assets.Executables;

namespace OpenSpidey.Assets.Models;

/// <summary>The original Scorpion and Jonah light packets, retained as managed import data.</summary>
public sealed record OriginalScorpionEncounterData(SourceModelLight ScorpionLight, SourceModelLight JonahLight)
{
    public static OriginalScorpionEncounterData Read(ReadOnlyMemory<byte> image)
    {
        var data = new OriginalExecutableData(image);
        return new(SourceModelLight.Read(data.Slice(0x5523D0, 52).Span),
            SourceModelLight.Read(data.Slice(0x552010, 52).Span));
    }
}
