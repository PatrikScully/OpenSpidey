using System.Buffers.Binary;
using System.Text;

namespace OpenSpidey.Core.Saves;

public enum OriginalSaveSlotState { Valid, ReadError, Empty }

/// <summary>Original 0xBC-byte SSaveGame record (gSaveGame 0x682858), kept as exact bytes with typed field views.</summary>
public sealed class OriginalSaveGame
{
    public const int Size = 0xBC;
    public const int CompletionCountOffset = 0x56;
    public const int CompletionCountLength = 34;
    private readonly byte[] bytes;

    public OriginalSaveGame() => bytes = new byte[Size];

    public OriginalSaveGame(ReadOnlySpan<byte> record)
    {
        if (record.Length != Size) throw new ArgumentException("An original save record is exactly 0xBC bytes.", nameof(record));
        bytes = record.ToArray();
    }

    public ReadOnlySpan<byte> Bytes => bytes;
    public OriginalSaveGame Clone() => new(bytes);

    public uint Checksum { get => U32(0x00); set => SetU32(0x00, value); }
    public string LevelName { get => Text(0x04, 9); set => SetText(0x04, 9, value); }
    public string RestartPointName { get => Text(0x0D, 50); set => SetText(0x0D, 50, value); }
    /// <summary>Shell_InputName slot name shown by Shell_AddGameSlots 0x49F220.</summary>
    public string SaveName { get => Text(0x3F, 9); set => SetText(0x3F, 9, value); }
    public int Webbing { get => I32(0x48); set => SetI32(0x48, value); }
    public int Cartridges { get => I32(0x4C); set => SetI32(0x4C, value); }
    public int ArmourAmount { get => I32(0x50); set => SetI32(0x50, value); }
    public sbyte Difficulty { get => (sbyte)bytes[0x54]; set => bytes[0x54] = (byte)value; }
    public byte StoryboardUnlocked { get => bytes[0x55]; set => bytes[0x55] = value; }
    public Span<byte> CompletionCounts => bytes.AsSpan(CompletionCountOffset, CompletionCountLength);
    /// <summary>Set by New Game/Shell_Difficulty and the LEANEST cheat; gates main-menu Continue.</summary>
    public byte HasGame { get => bytes[0x78]; set => bytes[0x78] = value; }
    public byte ArmourUnlocked { get => bytes[0x79]; set => bytes[0x79] = value; }
    public byte ArmourOn { get => bytes[0x7A]; set => bytes[0x7A] = value; }
    public byte ForceExitLevel { get => bytes[0x7B]; set => bytes[0x7B] = value; }
    public byte LastUnlockedCostume { get => bytes[0x7C]; set => bytes[0x7C] = value; }
    public uint CostumeUnlockFlags { get => U32(0x80); set => SetU32(0x80, value); }
    public uint CharacterViewerUnlockFlags { get => U32(0x84); set => SetU32(0x84, value); }
    public uint MovieViewerUnlockFlags { get => U32(0x88); set => SetU32(0x88, value); }
    public uint CollectedComicMask { get => U32(0x8C); set => SetU32(0x8C, value); }
    public uint ComicCoverUnlockFlags { get => U32(0x90); set => SetU32(0x90, value); }
    /// <summary>gGameState[12], scaled 0..0x3FFF by Shell_SFXMusic "music and sfx level".</summary>
    public int MusicAndSfxLevel { get => I32(0x94); set => SetI32(0x94, value); }
    /// <summary>gGameState[11], 0..255 "movie level".</summary>
    public int MovieLevel { get => I32(0x98); set => SetI32(0x98, value); }
    /// <summary>gGameState[13], 0..255 "voice level" (XA volume).</summary>
    public int VoiceLevel { get => I32(0x9C); set => SetI32(0x9C, value); }
    /// <summary>DCSetBootROMSoundMode value; nonzero is mono.</summary>
    public byte BootRomSoundMode { get => bytes[0xA0]; set => bytes[0xA0] = value; }
    public short ScreenX { get => I16(0xA4); set => SetI16(0xA4, value); }
    public short ScreenY { get => I16(0xA8); set => SetI16(0xA8, value); }
    public short DigitalMapping(int index) => I16(0xAC + Index4(index) * 2);
    public void SetDigitalMapping(int index, short value) => SetI16(0xAC + Index4(index) * 2, value);
    public short AnalogueMapping(int index) => I16(0xB4 + Index4(index) * 2);
    public void SetAnalogueMapping(int index, short value) => SetI16(0xB4 + Index4(index) * 2, value);

    /// <summary>Shell_CalculateGameChecksum, inlined at 0x49F12D: 46 dwords from +4, rotate-left then add, low bit set.</summary>
    public static uint CalculateChecksum(ReadOnlySpan<byte> record)
    {
        if (record.Length != Size) throw new ArgumentException("An original save record is exactly 0xBC bytes.", nameof(record));
        uint checksum = 0;
        for (int offset = 4; offset < Size; offset += 4)
            checksum = unchecked(((checksum << 1) | (checksum >> 31)) + BinaryPrimitives.ReadUInt32LittleEndian(record[offset..]));
        return checksum | 1;
    }

    public uint CalculateChecksum() => CalculateChecksum(bytes);
    public void Seal() => Checksum = CalculateChecksum();

    /// <summary>Shell_AddGameSlots 0x49F220: zero is "---- empty ----", a mismatch is "read error".</summary>
    public OriginalSaveSlotState SlotState => Checksum == 0 ? OriginalSaveSlotState.Empty
        : Checksum == CalculateChecksum() ? OriginalSaveSlotState.Valid : OriginalSaveSlotState.ReadError;

    /// <summary>Shell_DoShell 0x4A1A80 New Game: names, carried resources and armour from the default save, cleared counts.</summary>
    public void BeginNewGame(OriginalSaveGame defaults, int difficulty)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        if (difficulty is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(difficulty));
        LevelName = "l1a1_t";
        RestartPointName = "Re_Start_death";
        SaveName = defaults.SaveName;
        Webbing = defaults.Webbing;
        Cartridges = defaults.Cartridges;
        ArmourAmount = defaults.ArmourAmount;
        ArmourUnlocked = defaults.ArmourUnlocked;
        ArmourOn = defaults.ArmourOn;
        CompletionCounts.Clear();
        Difficulty = (sbyte)difficulty;
        HasGame = 1;
    }

    private static int Index4(int index) => (uint)index < 4 ? index : throw new ArgumentOutOfRangeException(nameof(index));
    private uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
    private short I16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    private void SetU32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private void SetI32(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
    private void SetI16(int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);

    private string Text(int offset, int length)
    {
        ReadOnlySpan<byte> field = bytes.AsSpan(offset, length);
        int end = field.IndexOf((byte)0);
        return Encoding.Latin1.GetString(end < 0 ? field : field[..end]);
    }

    /// <summary>Utils_CopyString 0x4E65E0 semantics: bytes after the terminator stay untouched.</summary>
    private void SetText(int offset, int length, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length >= length || value.Any(character => character == 0 || character > 255))
            throw new ArgumentException($"The original field holds at most {length - 1} Latin1 characters.", nameof(value));
        Encoding.Latin1.GetBytes(value, bytes.AsSpan(offset));
        bytes[offset + value.Length] = 0;
    }
}
