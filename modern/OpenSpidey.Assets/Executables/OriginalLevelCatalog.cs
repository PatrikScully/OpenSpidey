using System.Buffers.Binary;
using System.Text;

namespace OpenSpidey.Assets.Executables;

public sealed record OriginalLevelDefinition(int Index, string DisplayName, string MissionName,
    byte FlowFlags, uint ComicCoverUnlockMask, uint CharacterViewerUnlockMask);

/// <summary>Original campaign table imported as bounded managed metadata; no executable code or pointers survive.</summary>
public sealed class OriginalLevelCatalog
{
    public const int CampaignLevelCount = 34;
    public IReadOnlyList<OriginalLevelDefinition> Levels { get; }

    private OriginalLevelCatalog(OriginalLevelDefinition[] levels) => Levels = Array.AsReadOnly(levels);

    public static OriginalLevelCatalog Read(ReadOnlyMemory<byte> image)
    {
        var data = new OriginalExecutableData(image);
        var levels = new List<OriginalLevelDefinition>();
        // Source SLevel stride20 and capacity41; the original lookup stops at the first empty name.
        for (int index = 0; index < 41; index++)
        {
            uint address = checked(0x54A518u + (uint)index * 20);
            ReadOnlySpan<byte> record = data.Slice(address, 20).Span;
            string mission = Encoding.Latin1.GetString(data.ReadString(
                BinaryPrimitives.ReadUInt32LittleEndian(record[4..]), 10).Span);
            if (mission.Length == 0)
            {
                if (levels.Count != CampaignLevelCount)
                    throw new InvalidDataException($"Original campaign table contains {levels.Count} levels, expected {CampaignLevelCount}.");
                return new(levels.ToArray());
            }
            if (mission.Length < 4) throw new InvalidDataException($"Original level {index} has no source chapter/area key.");
            string title = Encoding.Latin1.GetString(data.ReadString(BinaryPrimitives.ReadUInt32LittleEndian(record), 256).Span);
            levels.Add(new(index, title, mission, record[8],
                BinaryPrimitives.ReadUInt32LittleEndian(record[12..]), BinaryPrimitives.ReadUInt32LittleEndian(record[16..])));
        }
        throw new InvalidDataException("Original campaign table has no bounded empty-name terminator.");
    }

    /// <summary>Front_FindLevel compares only byte1 and byte3, including aliases such as l1a2a_t.</summary>
    public OriginalLevelDefinition? Find(string missionName)
    {
        ArgumentNullException.ThrowIfNull(missionName);
        if (missionName.Length < 4 || missionName[1] > 255 || missionName[3] > 255) return null;
        return Levels.FirstOrDefault(level => level.MissionName[1] == missionName[1] && level.MissionName[3] == missionName[3]);
    }

    public int FindIndex(string missionName) => Find(missionName)?.Index ?? -1;
}
