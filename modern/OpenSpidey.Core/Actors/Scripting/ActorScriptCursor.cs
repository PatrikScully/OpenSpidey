using System.Buffers.Binary;
using System.Text;

namespace OpenSpidey.Core.Actors.Scripting;

/// <summary>Bounded source-script addressing replaces native pointers, retaining absolute file alignment.</summary>
public sealed class ActorScriptCursor
{
    public ReadOnlyMemory<byte> Source { get; }
    public int SourceOffset { get; }
    public int Position { get; private set; }
    public int AbsolutePosition => checked(SourceOffset + Position);

    public ActorScriptCursor(ReadOnlyMemory<byte> source, int sourceOffset = 0, int position = 0)
    {
        if (sourceOffset < 0 || (sourceOffset & 1) != 0) throw new ArgumentOutOfRangeException(nameof(sourceOffset));
        Source = source;
        SourceOffset = sourceOffset;
        Seek(position);
    }

    public ActorScriptCursor Clone() => new(Source, SourceOffset, Position);

    public void Seek(int position)
    {
        if (position < 0 || position > Source.Length || (position & 1) != 0)
            throw new InvalidDataException("Actor script cursor is outside a word boundary.");
        Position = position;
    }

    public ushort PeekWord()
    {
        Require(2);
        return BinaryPrimitives.ReadUInt16LittleEndian(Source.Span[Position..]);
    }

    public ushort ReadWord()
    {
        ushort value = PeekWord();
        Position += 2;
        return value;
    }

    public short ReadScriptValue(Func<ushort, ActorScriptCursor, short> readVariable)
    {
        ushort value = ReadWord();
        return (value & 0xA000) == 0x2000 ? readVariable(value, this) : unchecked((short)value);
    }

    public uint ReadAlignedDword()
    {
        Align(4);
        Require(4);
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(Source.Span[Position..]);
        Position += 4;
        return value;
    }

    public string ReadString()
    {
        int length = Source.Span[Position..].IndexOf((byte)0);
        if (length < 0) throw new InvalidDataException("Actor script string has no terminator.");
        string text = Encoding.Latin1.GetString(Source.Span.Slice(Position, length));
        Position += length + 1;
        Align(2);
        return text;
    }

    private void Align(int alignment)
    {
        int padding = (alignment - AbsolutePosition % alignment) % alignment;
        Require(padding);
        Position += padding;
    }

    private void Require(int length)
    {
        if (length < 0 || length > Source.Length - Position)
            throw new InvalidDataException($"Actor script read extends beyond its source at 0x{AbsolutePosition:X}.");
    }
}
