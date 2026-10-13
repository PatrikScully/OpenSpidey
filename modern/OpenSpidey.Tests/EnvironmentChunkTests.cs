using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

internal static class EnvironmentChunkTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(EnvironmentChunkTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.environment-chunk.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.GetProperty("controls").EnumerateArray())
        {
            var random = new GameRandom(test.GetProperty("seed").GetInt32());
            var control = new EnvironmentChunkControl(default, test.GetProperty("capacity").GetUInt16());
            ChunkItem[] items = test.GetProperty("items").EnumerateArray().Select(values =>
            {
                int[] data = values.EnumerateArray().Select(value => value.GetInt32()).ToArray();
                return new ChunkItem { ItemFlags = (ushort)data[0], Position = new(data[1], data[2], data[3]),
                    Angles = new((short)data[4], (short)data[5], (short)data[6]) };
            }).ToArray();
            for (int index = 0; index < test.GetProperty("requested").GetInt32(); index++) control.AddChunk(items[index], random.Next);
            JsonElement[] snapshots = test.GetProperty("expected").EnumerateArray().ToArray();
            Verify(snapshots[0], 0);
            int frame = 1;
            foreach (JsonElement ticks in test.GetProperty("ticks").EnumerateArray())
            { control.Update(ticks.GetUInt32()); Verify(snapshots[frame], frame); frame++; }

            void Verify(JsonElement expected, int frameIndex)
            {
                string context = $"Original CChunkControl seed{test.GetProperty("seed").GetInt32()} capacity{control.Capacity} snapshot{frameIndex}";
                assert(control.BodyFlags == expected.GetProperty("bodyFlags").GetInt32() &&
                    control.Entries.Count == expected.GetProperty("added").GetInt32(),
                    context + " preserves source capacity, no-RNG overflow rejection and all-retired CBody death.");
                JsonElement[] expectedEntries = expected.GetProperty("entries").EnumerateArray().ToArray();
                for (int index = 0; index < control.Entries.Count; index++)
                {
                    EnvironmentChunkEntry entry = control.Entries[index];
                    long[] actual = [entry.Item is null ? 0 : 1, entry.Velocity.X, entry.Velocity.Y, entry.Velocity.Z,
                        entry.AngularVelocity.X, entry.AngularVelocity.Y, entry.AngularVelocity.Z, entry.Lifetime];
                    assert(actual.SequenceEqual(Read(expectedEntries[index])), context + $" entry{index} retains actual velocity/spin, unsigned lifetime, gravity and retired-item pointer.");
                }
                JsonElement[] expectedItems = expected.GetProperty("items").EnumerateArray().ToArray();
                for (int index = 0; index < items.Length; index++)
                {
                    ChunkItem item = items[index];
                    long[] actual = [item.ItemFlags, item.Position.X, item.Position.Y, item.Position.Z,
                        item.Angles.X, item.Angles.Y, item.Angles.Z];
                    assert(actual.SequenceEqual(Read(expectedItems[index])), context + $" item{index} uses original additive position and signed-short angle wrap, rather than recovered source assignments.");
                }
                assert(new long[] { random.State.Value, random.State.Multiplier, random.State.Increment }
                    .SequenceEqual(Read(expected.GetProperty("random"))), context + " preserves the complete original random state and seven-call AddChunk order.");
            }
        }

        int records = 0, banks = 0;
        foreach (JsonElement packet in document.RootElement.GetProperty("packets").EnumerateArray())
        {
            PsxChunkTable table = PsxChunkTable.ReadPacket(Convert.FromHexString(packet.GetProperty("packet").GetString()!));
            JsonElement[] expected = packet.GetProperty("records").EnumerateArray().ToArray();
            assert(table.Records.Count == expected.Length, "Actual " + packet.GetProperty("asset").GetString() + " retains its complete Chnk entry count.");
            for (int index = 0; index < expected.Length; index++)
            {
                PsxChunkRecord record = table.Records[index]; JsonElement source = expected[index];
                assert(record.Checksum == source.GetProperty("checksum").GetUInt32() && record.SourceByteOffset == source.GetProperty("offset").GetUInt32() &&
                    record.StaticRuinModels.Select(value => (long)value).SequenceEqual(Read(source.GetProperty("stationary"))) &&
                    record.MovingRuinChunks.Select(value => (long)value).SequenceEqual(Read(source.GetProperty("moving"))) && ReferenceEquals(table.Find(record.Checksum), record),
                    "Actual " + packet.GetProperty("asset").GetString() + $" Chnk record{index} preserves source checksums, relative offsets and static-before-moving piece order.");
                records++;
            }
            banks++;
        }
        assert(records == 51 && banks == 7, "Every original Chnk record is retained across the seven actual campaign G banks.");
        try { PsxChunkTable.ReadPacket(new byte[] { 255, 255, 255, 127 }); throw new Exception("Truncated Chnk entries were accepted."); }
        catch (Exception error) when (error is InvalidDataException or OverflowException)
        { assert(true, "Chnk packet bounds reject overflowing or truncated source entry tables before reading piece data."); }
    }

    private static IEnumerable<long> Read(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64());
    private sealed class ChunkItem : IEnvironmentChunkItem
    {
        public ushort ItemFlags { get; set; }
        public FixedVector3 Position { get; set; }
        public ActorAngles Angles { get; set; }
    }
}
