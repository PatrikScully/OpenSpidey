using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.World;

internal static class EnvironmentChunkProducerTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(EnvironmentChunkProducerTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.environment-chunk-producer.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.GetProperty("boundaries").EnumerateArray())
        {
            var random = new GameRandom(test.GetProperty("seed").GetInt32());
            ChunkItem[] items = test.GetProperty("items").EnumerateArray().Select((input, index) =>
            {
                int[] position = Read(input.GetProperty("position")).Select(value => checked((int)value)).ToArray();
                int[] angles = Read(input.GetProperty("angles")).Select(value => checked((int)value)).ToArray();
                return new ChunkItem { Checksum = 0x102A9u + (uint)index * 256, ItemFlags = input.GetProperty("flags").GetUInt16(),
                    Position = new(position[0], position[1], position[2]), Angles = new((short)angles[0], (short)angles[1], (short)angles[2]),
                    SourceModel = new() { Flags = input.GetProperty("modelFlags").GetUInt16(), RendererFlags = input.GetProperty("rendererFlags").GetUInt32(),
                        SourceFaces = input.GetProperty("faces").EnumerateArray().Select(face =>
                        { long[] words = Read(face).ToArray(); return new ChunkFace { Flags = (uint)words[0], CollisionWord = (uint)words[1], RendererFlags = (ushort)words[2] }; }).ToArray() } };
            }).ToArray();
            var file = new TriggerFile(Enumerable.Range(0, 173).Select(id => id >= 170 ? ContactNode((ushort)id, items[0].Checksum) :
                new TriggerNode((ushort)id, 0, 0, new byte[2])).ToArray());
            var mission = new MissionRuntime(file, new NoActorFactory(), []);
            mission.TriggerCommandPoint(items[0].Checksum); // Reconstruct the PC's initially executed first bucket entry.
            mission.ClearPendingEvents(); mission.BeginContactFrame();
            var queued = new List<ushort>();
            mission.Trace += trace => { if (trace.Kind == MissionTraceKind.Queued) queued.Add(trace.NodeId); };
            var host = new ChunkHost(items, MakePacket(test, items), mission, random);
            for (int repeat = 0; repeat < test.GetProperty("repeats").GetInt32(); repeat++)
                EnvironmentChunkDestruction.ChunkByChecksum(items[0].Checksum, host);
            JsonElement expected = test.GetProperty("expected");
            string context = $"Original whole Chunk_ChunkItemByChecksum seed{test.GetProperty("seed")} static{test.GetProperty("stationary")} moving{test.GetProperty("moving")} missing{test.GetProperty("missing")} repeats{test.GetProperty("repeats")}";
            JsonElement[] expectedItems = expected.GetProperty("items").EnumerateArray().ToArray();
            for (int index = 0; index < items.Length; index++)
            {
                ChunkItem item = items[index]; JsonElement result = expectedItems[index];
                assert(item.ItemFlags == result.GetProperty("flags").GetUInt16() && item.SourceModel.Flags == result.GetProperty("modelFlags").GetUInt16() &&
                    item.SourceModel.RendererFlags == result.GetProperty("rendererFlags").GetUInt32(), context + $" item{index} preserves distinct original item/model/DC renderer masks.");
                JsonElement[] expectedFaces = result.GetProperty("faces").EnumerateArray().ToArray();
                for (int face = 0; face < item.SourceModel.SourceFaces.Length; face++)
                {
                    ChunkFace actual = item.SourceModel.SourceFaces[face];
                    assert(new long[] { actual.Flags, actual.CollisionWord, actual.RendererFlags }.SequenceEqual(Read(expectedFaces[face])),
                        context + $" item{index} face{face} preserves variable source strides, conditional DCFace draw bit and static-only collision-word edits.");
                }
            }
            JsonElement[] expectedControls = expected.GetProperty("controls").EnumerateArray().ToArray();
            assert(host.Controls.Count == expectedControls.Length, context + " retains the original head insertion and creates no control for a missing or purely stationary record.");
            for (int index = 0; index < host.Controls.Count; index++)
            {
                EnvironmentChunkControl control = host.Controls[index]; JsonElement result = expectedControls[index];
                assert(control.BodyFlags == result.GetProperty("bodyFlags").GetUInt16() && control.Capacity == result.GetProperty("capacity").GetUInt16() &&
                    control.Entries.Count == result.GetProperty("added").GetUInt16() && result.GetProperty("node").GetUInt16() == ushort.MaxValue,
                    context + $" control{index} retains real CBody defaults, capacity, ordered entries and original unscripted nodeFFFF.");
                JsonElement[] entries = result.GetProperty("entries").EnumerateArray().ToArray();
                for (int entryIndex = 0; entryIndex < control.Entries.Count; entryIndex++)
                {
                    EnvironmentChunkEntry entry = control.Entries[entryIndex];
                    long[] actual = [Array.IndexOf(items, entry.Item), entry.Velocity.X, entry.Velocity.Y, entry.Velocity.Z,
                        entry.AngularVelocity.X, entry.AngularVelocity.Y, entry.AngularVelocity.Z, entry.Lifetime];
                    assert(actual.SequenceEqual(Read(entries[entryIndex])), context + $" control{index} entry{entryIndex} adds genuine item identities before revealing moving rubble and consumes original seven-call random order.");
                }
            }
            JsonElement[] allocations = expected.GetProperty("allocationBoundaries").EnumerateArray().ToArray();
            assert(host.AllocationBoundaries.Count == allocations.Length, context + " preserves every original entry-array allocation boundary.");
            for (int index = 0; index < allocations.Length; index++)
                assert(host.AllocationBoundaries[index].SequenceEqual(Read(allocations[index])), context + $" constructor{index} attaches a real source body after hiding the source and queuing its command point, before any piece is revealed.");
            JsonElement[] points = expected.GetProperty("points").EnumerateArray().ToArray();
            for (ushort index = 0; index < 3; index++)
            {
                CommandPointState point = mission.CommandPoints[(ushort)(172 - index)];
                assert(new long[] { point.ContactThisFrame ? 1 : 0, point.ExecutedForContact ? 1 : 0 }.SequenceEqual(Read(points[index])),
                    context + $" point{point.NodeId} matches actual first-unexecuted checksum traversal and visits prior executed entries.");
            }
            assert(queued.Select(value => (long)value).SequenceEqual(Read(expected.GetProperty("pending"))) &&
                new long[] { random.State.Value, random.State.Multiplier, random.State.Increment }.SequenceEqual(Read(expected.GetProperty("random"))),
                context + " retains exact pending mission order and complete original random state, including missing-record pre-hide/queue side effects.");
            assert(host.Synchronized.First() == items[0] && (items[0].ItemFlags & 0x21) == 0x21,
                context + " publishes original source-item disappearance before checking the Chnk record.");
        }
    }

    private static IEnumerable<long> Read(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64());

    private static PsxChunkTable MakePacket(JsonElement test, ChunkItem[] items)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        int stationary = test.GetProperty("stationary").GetInt32(), moving = test.GetProperty("moving").GetInt32();
        writer.Write(1u); writer.Write(items[0].Checksum + (test.GetProperty("missing").GetBoolean() ? 1u : 0u)); writer.Write(3u);
        writer.Write((uint)(stationary | moving << 16));
        for (int index = 0; index < stationary; index++) writer.Write(items[index + 1].Checksum);
        for (int index = 0; index < moving; index++) writer.Write(items[index + 3].Checksum);
        return PsxChunkTable.ReadPacket(stream.ToArray());
    }

    private static TriggerNode ContactNode(ushort id, uint checksum)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write((ushort)2); writer.Write((ushort)0); writer.Write(checksum);
        return new(id, 2, 0, stream.ToArray());
    }

    private sealed class ChunkHost(ChunkItem[] items, PsxChunkTable table, MissionRuntime mission, GameRandom random) : IEnvironmentChunkHost
    {
        public List<EnvironmentChunkControl> Controls { get; } = [];
        public List<long[]> AllocationBoundaries { get; } = [];
        public List<ChunkItem> Synchronized { get; } = [];
        public void TriggerCommandPoint(uint checksum) => mission.TriggerCommandPoint(checksum);
        public IEnvironmentChunkSourceItem FindEnvironmentItem(uint checksum) => items.First(item => item.Checksum == checksum);
        public PsxChunkTable RequireChunkTable(IEnvironmentChunkSourceItem item) => table;
        public EnvironmentChunkControl CreateControl(FixedVector3 position, ushort capacity)
        {
            if ((items[0].ItemFlags & 0x21) != 0x21 || Synchronized.LastOrDefault() != items[0] || mission.PendingCount == 0)
                throw new InvalidOperationException("Source disappearance/contact queue must precede construction.");
            var control = new EnvironmentChunkControl(position, capacity);
            Controls.Insert(0, control);
            AllocationBoundaries.Add([capacity * 32u, control.BodyFlags, control.Capacity, control.Entries.Count,
                unchecked((uint)position.X), unchecked((uint)position.Y), unchecked((uint)position.Z), mission.PendingCount > 0 ? 1 : 0]);
            return control;
        }
        public void Synchronize(IEnvironmentChunkSourceItem item) => Synchronized.Add((ChunkItem)item);
        public int Random(int range) => random.Next(range);
    }

    private sealed class ChunkItem : IEnvironmentChunkSourceItem
    {
        public uint Checksum { get; init; }
        public ushort ItemFlags { get; set; }
        public FixedVector3 Position { get; set; }
        public ActorAngles Angles { get; set; }
        public required ChunkModel SourceModel { get; init; }
        public IEnvironmentChunkModel Model => SourceModel;
    }
    private sealed class ChunkModel : IEnvironmentChunkModel
    {
        public ushort Flags { get; set; }
        public uint RendererFlags { get; set; }
        public ChunkFace[] SourceFaces { get; init; } = [];
        public IReadOnlyList<IEnvironmentChunkFace> Faces => SourceFaces;
    }
    private sealed class ChunkFace : IEnvironmentChunkFace
    {
        public uint Flags { get; set; }
        public uint CollisionWord { get; set; }
        public ushort RendererFlags { get; set; }
    }
    private sealed class NoActorFactory : IMissionActorFactory
    {
        public void Spawn(TriggerNode node) => throw new InvalidOperationException("The original chunk producer queues static command points without spawning trigger actors.");
    }
}
