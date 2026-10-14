using System.Buffers.Binary;
using System.Text;

namespace OpenSpidey.Core.Saves;

public enum OriginalCardLoadResult { Loaded = 0, Missing = 1, Error = 2 }

/// <summary>Original SPIDRMAN.DAT image: u32 0xA24 then Head (0x60FFB0): SCardHead, 8 SSaveGame slots, SRecords, 2 pad bytes.</summary>
public sealed class OriginalSaveFile
{
    public const string FileName = "SPIDRMAN.DAT";
    public const int HeadSize = 0xA24;
    public const int HeaderSize = 0x200;
    public const int SlotCount = 8;
    public const int SlotOffset = 0x200;
    public const int RecordsOffset = 0x7E0;
    public const int BlockSize = 0x200;
    /// <summary>buCalcBackupFileSize 0x504990 rounds 4 + 0xA24 bytes up to six 0x200-byte blocks.</summary>
    public const int FileSize = (4 + HeadSize + BlockSize - 1) / BlockSize * BlockSize;
    private readonly byte[] head;
    private readonly byte[] clut;
    private readonly byte[] icon;

    public OriginalSaveFile(ReadOnlySpan<byte> cardClut, ReadOnlySpan<byte> cardIcon) : this(new byte[HeadSize], cardClut, cardIcon) { }

    /// <summary>Wraps an exact in-memory Head image (for example the state left by an earlier session).</summary>
    public static OriginalSaveFile FromHead(ReadOnlySpan<byte> head, ReadOnlySpan<byte> cardClut, ReadOnlySpan<byte> cardIcon) =>
        head.Length == HeadSize ? new(head.ToArray(), cardClut, cardIcon) : throw new ArgumentException("Head is 0xA24 bytes.", nameof(head));

    private OriginalSaveFile(byte[] head, ReadOnlySpan<byte> cardClut, ReadOnlySpan<byte> cardIcon)
    {
        if (cardClut.Length != 32 || cardIcon.Length != 128)
            throw new ArgumentException("The original card header uses a 32-byte gCardClut and a 128-byte gCardIcon.");
        this.head = head;
        clut = cardClut.ToArray();
        icon = cardIcon.ToArray();
    }

    public ReadOnlySpan<byte> Head => head;

    public OriginalSaveGame Slot(int index) => new(SlotBytes(index));
    public void SetSlot(int index, OriginalSaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);
        save.Bytes.CopyTo(SlotBytes(index));
    }

    public OriginalHighScores Records
    {
        get => new(head.AsSpan(RecordsOffset, OriginalHighScores.Size));
        set { ArgumentNullException.ThrowIfNull(value); value.Bytes.CopyTo(head.AsSpan(RecordsOffset)); }
    }

    /// <summary>Card_SetHeader 0x46C450: magic, type 0x11, one block, title, zeroed reserve, clut and Icon[0] only.</summary>
    public void SetHeader()
    {
        head[0] = (byte)'S';
        head[1] = (byte)'C';
        head[2] = 0x11;
        head[3] = 1;
        head.AsSpan(4, 64 + 28).Clear();
        Encoding.ASCII.GetBytes(FileName, head.AsSpan(4));
        clut.CopyTo(head.AsSpan(0x60));
        icon.CopyTo(head.AsSpan(0x80));
    }

    /// <summary>Card_Write 0x46C500 body: header refresh, then the six-block buMakeBackupFileImage layout.</summary>
    public byte[] Write()
    {
        SetHeader();
        var file = new byte[FileSize];
        BinaryPrimitives.WriteUInt32LittleEndian(file, HeadSize);
        head.CopyTo(file, 4);
        return file;
    }

    /// <summary>Card_Load 0x46C7F0: only a leading size of 0xA24 is accepted; Head then takes the file and Card_SetHeader runs.</summary>
    public OriginalCardLoadResult Load(ReadOnlySpan<byte> file, bool exists)
    {
        if (!exists) return OriginalCardLoadResult.Missing;
        ReadOnlySpan<byte> size = stackalloc byte[] { 0x24, 0x0A, 0, 0 };
        if (file.Length < 4)
        {
            if (!file.SequenceEqual(size[..file.Length])) return OriginalCardLoadResult.Error;
            throw new InvalidDataException("The original Card_Load would read the size word from uninitialized buffer bytes.");
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(file) != HeadSize) return OriginalCardLoadResult.Error;
        if (file.Length < 4 + HeadSize)
            throw new InvalidDataException("The original Card_Load would copy uninitialized or unallocated bytes into Head.");
        file.Slice(4, HeadSize).CopyTo(head);
        SetHeader();
        return OriginalCardLoadResult.Loaded;
    }

    private Span<byte> SlotBytes(int index) => (uint)index < SlotCount
        ? head.AsSpan(SlotOffset + index * OriginalSaveGame.Size, OriginalSaveGame.Size)
        : throw new ArgumentOutOfRangeException(nameof(index));
}
