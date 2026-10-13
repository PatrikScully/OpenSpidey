using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;

internal static class TriggerPlacementTests
{
    public static void Run(Action<bool, string> assert)
    {
        ushort[] kinds = [1, 7, 5, 20, 500, 501, 3, 8, 10, 12, 13, 1000, 1001, 1002];
        foreach (ushort kind in kinds)
        foreach (int sourceOffset in new[] { 100, 102 })
        foreach (ushort linkCount in new ushort[] { 0, 1, 2 })
        {
            TriggerNode node = Build(kind, sourceOffset, linkCount);
            TriggerPlacement placement = TriggerPlacement.Read(node);
            assert(placement.Position == new TriggerPosition(int.MinValue, -123, int.MaxValue) &&
                (sourceOffset + placement.PositionOffset) % 4 == 0,
                "All recovered node layouts preserve signed positions and absolute file alignment.");
            assert(placement.ReadOrientation(node) == new TriggerOrientation(-1024, 2048, short.MinValue),
                "Node angles follow the shared position without additional alignment.");
            TriggerScriptFragment fragment = placement.ReadScriptAfterAngles(node);
            assert(fragment.SourceOffset == sourceOffset + placement.AfterPositionOffset + 6 &&
                BinaryPrimitives.ReadUInt16LittleEndian(fragment.Data.Span) == 0x4100,
                "Script fragments retain their file address after position and angles.");
            assert(Rejects(() => TriggerPlacement.Read(node with { Data = node.Data[..(placement.AfterPositionOffset - 1)] })),
                "Truncated coordinates fail before a script can use them.");
            TriggerNode positionOnly = node with { Data = node.Data[..placement.AfterPositionOffset] };
            assert(TriggerPlacement.Read(positionOnly) == placement && Rejects(() => placement.ReadOrientation(positionOnly)),
                "Position-only records remain usable, while missing angles fail explicitly.");
            assert(Rejects(() => placement.ReadScriptAfterAngles(node with { Data = node.Data[..(placement.AfterPositionOffset + 6)] })),
                "Missing script words fail before execution.");
        }
        TriggerNode valid = Build(3, 100, 0);
        assert(!TriggerPlacement.IsSupported(4) && Rejects(() => TriggerPlacement.Read(valid with { TypeCode = 4 })),
            "Command-only records cannot be interpreted as placements.");
        byte[] wrongHeader = valid.Data.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(wrongHeader, 8);
        assert(Rejects(() => TriggerPlacement.Read(valid with { Data = wrongHeader })), "Mismatched node headers are rejected.");
    }

    private static TriggerNode Build(ushort kind, int sourceOffset, ushort linkCount)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(kind);
        if (kind is 1 or 7)
        {
            writer.Write((short)203); writer.Write((ushort)0); writer.Write(linkCount);
        }
        else if (kind is 5 or 20 or 1002)
        {
            writer.Write((ushort)0); writer.Write(linkCount);
        }
        else if (kind is not (500 or 501)) writer.Write(linkCount);
        if (kind is not (500 or 501))
            for (int index = 0; index < linkCount; index++) writer.Write((ushort)index);
        if (kind is 1 or 7)
        {
            writer.Write(new byte[] { 1, 2, 4, 255 });
        }
        while ((stream.Position + sourceOffset) % 4 != 0) writer.Write((byte)0);
        writer.Write(int.MinValue); writer.Write(-123); writer.Write(int.MaxValue);
        writer.Write((short)-1024); writer.Write((short)2048); writer.Write(short.MinValue);
        writer.Write((ushort)0x4100);
        return new(3, kind, sourceOffset, stream.ToArray());
    }

    private static bool Rejects(Action action)
    {
        try { action(); return false; }
        catch (InvalidDataException) { return true; }
    }
}
