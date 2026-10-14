using System.Buffers.Binary;
using System.Text;
using OpenSpidey.Assets.Executables;
using OpenSpidey.Core.Saves;

namespace OpenSpidey.Core.Frontend;

/// <summary>One Shell_MainMenu table 0x552AA8 entry: design position, label and EShellResult type (stride 40).</summary>
public sealed record OriginalMainMenuEntry(int X, int Y, string Text, int Type);

/// <summary>Frontend tables and defaults read from the original executable's stored .data bytes.</summary>
public sealed class OriginalFrontendData
{
    public required OriginalSaveGame DefaultSave { get; init; }
    public required OriginalHighScores DefaultRecords { get; init; }
    public required IReadOnlyList<OriginalChallenge> Challenges { get; init; }
    public required IReadOnlyList<OriginalCheat> Cheats { get; init; }
    public required IReadOnlyList<OriginalMainMenuEntry> MainMenu { get; init; }
    public required byte[] CardClut { get; init; }
    public required byte[] CardIcon { get; init; }
    public required OriginalLevelCatalog Levels { get; init; }
    public required IReadOnlyDictionary<uint, string> Text { get; init; }

    /// <summary>Localized char* slots used by the shell, front-end pause menu and slot list.</summary>
    public static readonly uint[] TextSlots =
    [
        0x54B7AC, 0x54B7EC, 0x54B750, 0x54B754, 0x54B760, 0x54B774, 0x54B77C, 0x54B780, 0x54B784, 0x54B74C, 0x54BBC8
    ];

    public const uint ReadErrorText = 0x54B7AC, EmptySlotText = 0x54B7EC, ContinueText = 0x54B750, RestartLevelText = 0x54B754,
        QuitText = 0x54B760, SkipToRestartText = 0x54B774, YesText = 0x54B77C, NoText = 0x54B780, ReallyQuitText = 0x54B784,
        PausedText = 0x54B74C;

    public static OriginalFrontendData Read(ReadOnlyMemory<byte> image)
    {
        var data = new OriginalExecutableData(image);
        var challenges = new OriginalChallenge[OriginalHighScores.ChallengeCount];
        for (int index = 0; index < challenges.Length; index++)
        {
            ReadOnlySpan<byte> record = data.Slice(0x551118u + (uint)index * 16, 16).Span;
            challenges[index] = new(index, String(data, BinaryPrimitives.ReadUInt32LittleEndian(record)),
                BinaryPrimitives.ReadInt16LittleEndian(record[4..]), (sbyte)record[7], (sbyte)record[0xB], record[0xC] != 0);
        }
        var cheats = new OriginalCheat[OriginalCheats.Count];
        for (int index = 0; index < cheats.Length; index++)
        {
            uint address = 0x5513E0u + (uint)index * 8;
            cheats[index] = new(index, String(data, data.ReadPointer(address)), String(data, data.ReadPointer(address + 4)));
        }
        var menu = new OriginalMainMenuEntry[8];
        for (int index = 0; index < menu.Length; index++)
        {
            ReadOnlySpan<byte> record = data.Slice(0x552AA8u + (uint)index * 40, 16).Span;
            menu[index] = new(BinaryPrimitives.ReadInt32LittleEndian(record), BinaryPrimitives.ReadInt32LittleEndian(record[4..]),
                String(data, BinaryPrimitives.ReadUInt32LittleEndian(record[8..])), BinaryPrimitives.ReadInt32LittleEndian(record[12..]));
        }
        return new()
        {
            DefaultSave = new(data.Slice(0x550E10, OriginalSaveGame.Size).Span),
            DefaultRecords = new(data.Slice(0x550ED0, OriginalHighScores.Size).Span),
            Challenges = challenges, Cheats = cheats, MainMenu = menu,
            CardClut = data.Slice(0x53BC7C, 32).ToArray(), CardIcon = data.Slice(0x53BC9C, 128).ToArray(),
            Levels = OriginalLevelCatalog.Read(image),
            Text = TextSlots.ToDictionary(slot => slot, slot => String(data, data.ReadPointer(slot)))
        };
    }

    public OriginalSaveFile CreateSaveFile() => new(CardClut, CardIcon);

    private static string String(OriginalExecutableData data, uint address) => Encoding.Latin1.GetString(data.ReadString(address, 256).Span);
}
