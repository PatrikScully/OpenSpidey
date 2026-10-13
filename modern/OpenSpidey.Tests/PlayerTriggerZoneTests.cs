using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.World;

internal static class PlayerTriggerZoneTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(PlayerTriggerZoneTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.player-trigger-zone.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            string context = $"Original trigger {test.GetProperty("kind").GetString()} [{string.Join(',', input)}]";
            if (test.GetProperty("kind").GetString() == "sweep")
            {
                var state = new PlayerTriggerZoneSweep();
                state.RestoreDirection(Vector(input, 7));
                var host = new SweepHost();
                PlayerTriggerZoneRay? ray = state.Sweep(Vector(input, 4), Vector(input, 1), unchecked((uint)input[0]), host);
                int[] direction = expected.GetProperty("direction").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                assert(state.LastMoveDirection == Vector(direction, 0), context + " retains exact source movement direction, including subunit signed shifts/death.");
                int[] events = expected.GetProperty("events").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                int[] actual = ray is { } value ? [1, 2, 1, value.Start.X, value.Start.Y, value.Start.Z, value.End.X, value.End.Y, value.End.Z] : [];
                assert(actual.SequenceEqual(events) && host.Ray == ray, context + " preserves initialized mode1 recording and actual source backtrack.");
                assert(expected.GetProperty("triggerGlobal").GetInt32() == (input[0] == 0x80 ? 7 : 0), context + " restores the source trigger global after the host call.");
            }
            else
            {
                var items = Enumerable.Range(0, input[4] * input[5]).Select(index =>
                    (IReadOnlyList<int>)Array.AsReadOnly(new[] { index % 7, index + 100, index % 7 })).ToArray();
                var grid = new PsxZoneGrid(input[0], input[1], input[2], input[3], (short)input[4], (short)input[5], items);
                FixedVector3 start = new(input[6], 0, input[7]), end = new(input[8], 0, input[9]);
                int[] cells = OriginalZoneTraversal.Cells(grid, start, end).SelectMany(cell => new[] { cell.X, cell.Z }).ToArray();
                int[] actual = [-1, .. cells];
                assert(actual.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt32())),
                    context + " preserves all rectangle clipping, maximum-edge correction and map cell traversal order.");
                var unique = new List<int>();
                for (int index = 0; index < cells.Length; index += 2)
                    foreach (int instance in grid.Cell(cells[index], cells[index + 1]))
                        if (!unique.Contains(instance)) unique.Add(instance);
                assert(OriginalZoneTraversal.Instances(grid, start, end).SequenceEqual(unique),
                    context + " preserves source inquiry's first encounter order across repeated cells/items.");
            }
        }
        VerifyPackets(assert);
        VerifyFacePolicy(assert);
    }

    private static void VerifyFacePolicy(Action<bool, string> assert)
    {
        using Stream resource = typeof(PlayerTriggerZoneTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.trigger-face-policy.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            int[] expected = test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            uint packed = unchecked((uint)input[0]);
            bool records = OriginalTriggerZoneContacts.Records(packed, input[1] != 0);
            assert(records == (expected[0] == 7), $"Original coarse face [{string.Join(',', input)}] records its model token only under the actual player/zone policy.");
            if (records)
                assert(expected[1] == input[2] && expected[2] == 0 && expected[3] == 0,
                    "Recording an accepted source trigger zone preserves the nearest blocker, item and face.");
        }
    }

    private static void VerifyPackets(Action<bool, string> assert)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(0x20006u); writer.Write(12u); writer.Write(0u);
        writer.Write(7u); writer.Write(4u); writer.Write(12345u);
        writer.Write(10u); writer.Write(20u + 16u + 24u);
        writer.Write(-100); writer.Write(-200); writer.Write(100); writer.Write(0); writer.Write((short)2); writer.Write((short)1);
        writer.Write(99u); writer.Write(101u); writer.Write(0u); writer.Write(0u);
        writer.Write(17u); writer.Write(42u); writer.Write(2u); writer.Write(0u); writer.Write(2u); writer.Write(0u);
        writer.Write(uint.MaxValue);
        PsxZoneGrid grid = PsxZoneGridReader.Read(stream.ToArray(), 3)!;
        assert(grid.MinimumX == -100 && grid.MinimumZ == -200 && grid.MaximumX == 100 && grid.MaximumZ == 0 &&
            grid.CellWidth == 100 && grid.Width == 2 && grid.Height == 1 && grid.Cell(0, 0).Count == 0 &&
            grid.Cell(1, 0).SequenceEqual([0, 2]), "Original packet10 retains cell metadata/count/null layout and authentic index0.");
        byte[] truncated = stream.ToArray()[..^5];
        bool rejected = false;
        try { PsxZoneGridReader.Read(truncated, 3); } catch (InvalidDataException) { rejected = true; }
        assert(rejected, "Truncated original zone cells are rejected before any source-contact dispatch.");
        rejected = false;
        try { PsxZoneGridReader.Read(stream.ToArray(), 2); } catch (InvalidDataException) { rejected = true; }
        assert(rejected, "Original zone index validation rejects missing source instances.");
    }

    private static FixedVector3 Vector(IReadOnlyList<int> values, int offset) => new(values[offset], values[offset + 1], values[offset + 2]);
    private sealed class SweepHost : IPlayerTriggerZoneSweepHost
    {
        public PlayerTriggerZoneRay? Ray { get; private set; }
        public void RecordTriggerZoneHits(PlayerTriggerZoneRay ray) => Ray = ray;
    }
}
