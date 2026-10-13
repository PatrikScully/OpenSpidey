using System.Buffers.Binary;

namespace OpenSpidey.Assets.Scripting;

/// <summary>Decodes mission command boundaries without invoking the original runtime.</summary>
public static class TriggerCommandReader
{
    public static IReadOnlyList<TriggerInstruction> Read(TriggerNode node)
    {
        if (node.TypeCode is 2 or 9)
        {
            // Validate the real links/checksum header; these records contain no authored mission command list.
            TriggerEnvironmentReference.ReadChecksum(node);
            return OriginalContactCommandProgram.Instructions;
        }
        var cursor = new Cursor(node);
        switch ((TriggerNodeKind)node.TypeCode)
        {
            case TriggerNodeKind.Autoexec:
            case TriggerNodeKind.AlternativeAutoexec:
                cursor.Position = 2;
                break;
            case TriggerNodeKind.CommandPoint:
                cursor.Position = 2;
                int links = cursor.Word();
                cursor.Skip(links * 2);
                cursor.Align(4);
                cursor.Skip(4); // Command point checksum.
                break;
            case TriggerNodeKind.Restart:
                cursor.Position = RestartPoint.Read(node).CommandOffset;
                break;
            default:
                return [];
        }

        var instructions = new List<TriggerInstruction>();
        while (true)
        {
            int start = cursor.Position;
            ushort opcode = cursor.Word();
            if (opcode == ushort.MaxValue) break;
            int argumentStart = cursor.Position;
            SkipOperands(opcode, cursor);
            instructions.Add(new(opcode, node.SourceOffset + start,
                node.Data.Slice(argumentStart, cursor.Position - argumentStart)));
        }
        return instructions.AsReadOnly();
    }

    private static void SkipOperands(ushort opcode, Cursor cursor)
    {
        switch (opcode)
        {
            case 2: // SetCheatRestarts: strings followed by an empty word.
                while (cursor.Byte() != 0) cursor.String();
                cursor.Skip(2);
                break;
            case 3: case 4: case 5: case 10: case 11: case 12:
            case 102: case 103: case 110: case 121: case 122: case 129:
            case 136: case 137: case 149: case 150: case 152: case 162: case 173:
            case 158: case 175: case 205: case 215: case 216: case 218: case 219:
            case 220: case 300: case 301: case 304: case 305:
                break;
            case 13: case 105: case 106: case 131: case 132: case 134:
            case 138: case 147: case 148: case 151: case 153: case 154:
            case 155: case 156: case 157: case 160: case 163: case 164: case 165:
            case 166: case 168: case 169: case 170: case 172:
            case 177: case 185: case 186: case 187: case 190: case 195:
            case 197: case 203: case 204: case 206: case 207: case 211:
            case 213: case 217: case 222: case 223: case 302:
                cursor.Skip(2);
                break;
            case 130: case 135: case 143: case 144: case 145: case 146:
            case 167: case 174: case 188: case 196: case 200: case 202:
            case 210: case 212: case 221: case 303:
                cursor.Skip(4);
                break;
            case 104: case 139: case 182: case 183: case 184:
                cursor.Skip(6);
                break;
            case 171: // BackgroundCreate uses aligned dwords.
                cursor.Align(4);
                cursor.Skip(10);
                break;
            case 180: case 214:
                cursor.Skip(10);
                break;
            case 208:
                cursor.Skip(16);
                break;
            case 115: case 119: case 126: case 127: case 128: case 140:
            case 142: case 159: case 176: case 181: case 189:
                cursor.String();
                break;
            case 191: case 198:
                cursor.String();
                cursor.Skip(6);
                break;
            case 199: // Synthesized player input: zero-delimited records, then 0xFF.
                do { while (cursor.Word() != 0) { } } while (cursor.PeekWord() != 255);
                cursor.Skip(2);
                break;
            case 141: case 192: case 209:
                cursor.Skip(4);
                goto case 133;
            case 133:
                while (cursor.PeekWord() != 255)
                {
                    cursor.Align(4);
                    cursor.Skip(24);
                }
                cursor.Skip(2);
                break;
            case 193: case 194: // Missing from the original SkipCommand table; actual execution defines this layout.
                cursor.Skip(2); // Pushback value.
                int count = cursor.Word();
                cursor.Align(4);
                cursor.Skip(count * 4);
                break;
            default:
                throw cursor.Error($"Unknown command {opcode}; an operand layout must be implemented.");
        }
    }

    private sealed class Cursor(TriggerNode node)
    {
        public int Position { get; set; }

        public ushort PeekWord()
        {
            Require(2);
            return BinaryPrimitives.ReadUInt16LittleEndian(node.Data.Span[Position..]);
        }

        public ushort Word()
        {
            ushort word = PeekWord();
            Position += 2;
            return word;
        }

        public byte Byte() { Require(1); return node.Data.Span[Position]; }

        public void Skip(int count) { Require(count); Position += count; }

        public void Align(int alignment)
        {
            int absolute = checked(node.SourceOffset + Position);
            Skip((alignment - absolute % alignment) % alignment);
        }

        public void String()
        {
            while (Byte() != 0) Position++;
            Position++;
            Align(2);
        }

        private void Require(int length)
        {
            if (Position < 0 || length < 0 || Position > node.Data.Length || length > node.Data.Length - Position)
                throw Error("Command extends beyond the node.");
        }

        public InvalidDataException Error(string message) =>
            new($"Node {node.Id}, file offset 0x{node.SourceOffset + Position:X}: {message}");
    }
}
