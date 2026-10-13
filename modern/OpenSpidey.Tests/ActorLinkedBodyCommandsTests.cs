using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Rendering;

internal static class ActorLinkedBodyCommandsTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorLinkedBodyCommandsTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.actor-linked-bodies.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            Target[] bodies = row.GetProperty("bodies").EnumerateArray().Select(body => new Target(
                body[0].GetUInt16(), body[1].GetUInt16(), body[2].GetInt16(), body[3].GetInt16())).ToArray();
            ushort[] links = row.GetProperty("links").EnumerateArray().Select(value => value.GetUInt16()).ToArray();
            int[] active = row.GetProperty("active").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var host = new Host(links, active.Select(body => bodies[body]).ToArray());
            ushort opcode = row.GetProperty("opcode").GetUInt16(), value = row.GetProperty("value").GetUInt16();
            var cursor = Operand(value);
            bool result = ActorLinkedBodyCommands.TryExecute(opcode, cursor, 154, host);
            long[] actual = [(result ? 1 : 0), cursor.Position,
                ..bodies.SelectMany(body => new long[] { body.ItemFlags, body.SourceDepth.First, body.SourceDepth.Second })];
            long[] expected = row.GetProperty("expected").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(expected),
                $"Original linked-body case{index++}, opcode{opcode:X4}, raw{value:X4}: actual[{string.Join(',', actual)}], expected[{string.Join(',', expected)}].");
            assert(host.HeadReads == links.Length && host.RequestedNode == 154,
                "Each authored link scans the actual current active list head; empty links retain genuine zero-work behavior.");
            int[] expectedWrites = links.SelectMany(link => active.Where(body => bodies[body].SourceNodeId == link)).ToArray();
            if (opcode == 0x450F)
            {
                int[] observed = host.Writes.Select(body => Array.IndexOf(bodies, body)).ToArray();
                assert(observed.SequenceEqual(expectedWrites),
                    "Visibility visits every duplicate source-node identity in head order and repeats writes for duplicate authored links.");
            }
        }

        var retained = new SourceSuperItemDepth { First = -32768, Second = 32767 };
        var rendering = new SourcePolygonDepthState(new(300, -60, -123));
        rendering.BeginRender();
        assert(rendering.ItemScratch(true, retained) == new SourceOrderTablePushback(-32768, 32767, -123) &&
            rendering.ItemScratch(false, retained) == rendering.Globals && rendering.RenderScratch == rendering.Globals,
            "Only an animated superitem substitutes its actual signed100/102 words; scene slots and subsequent item scratch remain unchanged.");
        assert(new SourceSuperItemDepth().First == 0 && new SourceSuperItemDepth().Second == 0,
            "The zero-initialized allocated source CSuper begins with independent zero OT words.");
        foreach (bool low in new[] { false, true })
        {
            SourceOrderTablePushback scratch = rendering.ItemScratch(true, retained);
            ushort face = SourcePolygonDepthState.FaceBias(0x2000, low, scratch);
            int key = SourcePolygonDepthState.BatchSortKey(0, 0, face, low);
            assert(SourcePolygonDepthState.RasterDepthBias(key, low, 0.002f) == 0,
                "Original PC authored CSuper OT words add no raster bias in either normal or low-graphics quality.");
        }
        var untouched = Operand(0x212B);
        assert(!ActorLinkedBodyCommands.TryExecute(0x450E, untouched, 154, new Host([], [])) && untouched.Position == 0,
            "Unknown inherited commands leave operands untouched for their genuine next handler.");
        bool truncated = false;
        try { ActorLinkedBodyCommands.TryExecute(0x4511, new(ReadOnlyMemory<byte>.Empty), 154, new Host([], [])); }
        catch (InvalidDataException) { truncated = true; }
        assert(truncated, "Malformed linked-body words fail before an authored link or body mutation.");
    }

    private static ActorScriptCursor Operand(ushort value)
    {
        byte[] bytes = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, value); return new(bytes);
    }
    private sealed class Target(ushort nodeId, ushort initialFlags, short first, short second) : IActorLinkedBodyTarget
    {
        private ushort flags = initialFlags;
        public Action<Target>? Written { get; set; }
        public ushort SourceNodeId => nodeId;
        public ushort ItemFlags { get => flags; set { flags = value; Written?.Invoke(this); } }
        public SourceSuperItemDepth SourceDepth { get; } = new() { First = first, Second = second };
    }
    private sealed class Host : IActorLinkedBodyCommandHost
    {
        private readonly IReadOnlyList<ushort> links;
        private readonly Target[] active;
        public ushort RequestedNode { get; private set; }
        public int HeadReads { get; private set; }
        public List<Target> Writes { get; } = [];
        public Host(IReadOnlyList<ushort> links, Target[] active)
        {
            this.links = links; this.active = active;
            foreach (Target target in active) target.Written = Writes.Add;
        }
        public IReadOnlyList<ushort> Links(ushort nodeId) { RequestedNode = nodeId; return links; }
        public IEnumerable<IActorLinkedBodyTarget> ActiveBaddies() { HeadReads++; return active; }
    }
}
