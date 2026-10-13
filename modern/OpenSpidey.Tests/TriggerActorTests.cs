using OpenSpidey.Assets.Scripting;

internal static class TriggerActorTests
{
    public static void Run(Action<bool, string> assert)
    {
        foreach (int sourceOffset in new[] { 100, 102 })
        foreach (ushort[] links in new ushort[][] { [], [7], [7, 11] })
        foreach (byte[] markers in new byte[][] { [1, 2, 4], [1], [0, 2, 5] })
        {
            TriggerNode node = Build(sourceOffset, links, markers);
            TriggerActorDefinition actor = TriggerActorDefinition.Read(node);
            assert(actor.NodeId == 3 && actor.ActorType == 312 && actor.Links.SequenceEqual(links) && actor.Markers.SequenceEqual(markers),
                "Actor subtype, links, markers and source identity survive import.");
            assert(actor.Position == new TriggerPosition(int.MinValue, 123, int.MaxValue) &&
                actor.Orientation == new TriggerOrientation(-1024, 2048, short.MinValue),
                "Actor placement preserves signed source coordinates and angles.");
            assert((actor.ObjectDataOffset + sourceOffset) % 4 == 0 && actor.BehaviorData.Span.SequenceEqual(new byte[] { 0, 65 }),
                "Factory data starts at absolute file alignment, with actor behavior bytes retained.");
            assert(actor.InitializeOnLoad == markers.Contains((byte)1) && actor.StartsSuspended == !markers.Contains((byte)2) &&
                actor.ClearsBodyFlag2 == markers.Contains((byte)4), "Actor factory marker effects preserve their distinct meanings.");
        }
        TriggerNode valid = Build(102, [7], [1, 2, 4]);
        var template = valid with { TypeCode = 7, Data = new byte[] { 7, 0 }.Concat(valid.Data.ToArray()[2..]).ToArray() };
        assert(!TriggerActorDefinition.Read(template).InitializeOnLoad, "Type-seven templates do not join type-one startup creation.");
        foreach (int length in new[] { 3, 8, 10, 12, valid.Data.Length - 3 })
            assert(Rejects(() => TriggerActorDefinition.Read(valid with { Data = valid.Data[..length] })),
                "Truncated links, markers and actor placements are rejected before spawning.");
        byte[] negativeCount = valid.Data.ToArray();
        negativeCount[6] = 255; negativeCount[7] = 255;
        assert(Rejects(() => TriggerActorDefinition.Read(valid with { Data = negativeCount })), "Negative signed factory link counts are rejected.");
    }

    private static bool Rejects(Action action)
    {
        try { action(); return false; }
        catch (InvalidDataException) { return true; }
    }

    private static TriggerNode Build(int sourceOffset, ushort[] links, byte[] markers)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)1); writer.Write((short)312); writer.Write((ushort)0x1000); writer.Write((short)links.Length);
        foreach (ushort link in links) writer.Write(link);
        writer.Write(markers); writer.Write((byte)255);
        while ((stream.Position + sourceOffset) % 4 != 0) writer.Write((byte)0);
        writer.Write(int.MinValue); writer.Write(123); writer.Write(int.MaxValue);
        writer.Write((short)-1024); writer.Write((short)2048); writer.Write(short.MinValue);
        writer.Write((ushort)0x4100);
        return new(3, 1, sourceOffset, stream.ToArray());
    }
}
