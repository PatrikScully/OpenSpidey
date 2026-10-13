using System.Text;
using OpenSpidey.Assets.Scripting;

internal static class RestartPointTests
{
    public static void Run(Action<bool, string> assert)
    {
        foreach (int sourceOffset in new[] { 100, 102 })
        foreach (ushort[] links in new ushort[][] { [], [7], [7, 11] })
        foreach (string name in new[] { "re_start", "checkpoint", "caf\u00e9" })
        {
            TriggerNode node = Build(sourceOffset, links, name);
            RestartPoint point = RestartPoint.Read(node);
            assert(point.NodeId == 3 && point.Name == name, "Restart names preserve original bytes and node identity.");
            assert(point.Position == new TriggerPosition(int.MinValue, 12345, int.MaxValue),
                "Restart positions preserve signed whole world units without float conversion or Q12 overflow.");
            assert(point.Orientation == new TriggerOrientation(-1024, 2048, short.MinValue),
                "Restart orientation preserves signed source angles.");
            assert(point.Links.SequenceEqual(links), "Restart pulse links preserve their file order.");
            IReadOnlyList<TriggerInstruction> commands = TriggerCommandReader.Read(node);
            assert(commands.Count == 1 && commands[0].Opcode == 115 &&
                commands[0].SourceOffset == sourceOffset + point.CommandOffset,
                "Restart command decoding begins after the aligned spawn metadata.");
        }

        TriggerNode valid = Build(102, [7], "checkpoint");
        assert(Rejects(() => RestartPoint.Read(valid with { TypeCode = 4 })), "Non-restart nodes are rejected.");
        assert(Rejects(() => RestartPoint.Read(valid with { Data = valid.Data[..3] })), "Truncated link headers are rejected.");
        assert(Rejects(() => RestartPoint.Read(valid with { Data = valid.Data[..5] })), "Truncated restart links are rejected.");
        assert(Rejects(() => RestartPoint.Read(valid with { Data = valid.Data[..20] })), "Truncated spawn coordinates are rejected.");
        byte[] unterminated = valid.Data.ToArray();
        Array.Fill(unterminated, (byte)65, 24, unterminated.Length - 24);
        assert(Rejects(() => RestartPoint.Read(valid with { Data = unterminated })), "Unterminated checkpoint names are rejected.");
        RestartPoint restart = RestartPoint.Read(valid);
        assert(Rejects(() => RestartPoint.Read(valid with { Data = valid.Data[..restart.CommandOffset] })),
            "A restart point must retain its command-list terminator.");
    }

    private static bool Rejects(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return true; }
        return false;
    }

    private static TriggerNode Build(int sourceOffset, ushort[] links, string name)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true);
        writer.Write((ushort)8);
        writer.Write((ushort)links.Length);
        foreach (ushort link in links) writer.Write(link);
        Align(4);
        writer.Write(int.MinValue);
        writer.Write(12345);
        writer.Write(int.MaxValue);
        writer.Write((short)-1024);
        writer.Write((short)2048);
        writer.Write(short.MinValue);
        WriteString(name);
        writer.Write((ushort)115);
        WriteString("re_start");
        writer.Write(ushort.MaxValue);
        return new(3, 8, sourceOffset, stream.ToArray());

        void WriteString(string text)
        {
            writer.Write(Encoding.Latin1.GetBytes(text));
            writer.Write((byte)0);
            Align(2);
        }

        void Align(int alignment)
        {
            while ((sourceOffset + stream.Position) % alignment != 0) writer.Write((byte)0);
        }
    }
}
