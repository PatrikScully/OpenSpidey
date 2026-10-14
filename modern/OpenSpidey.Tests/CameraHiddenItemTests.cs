using System.Text.Json;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;

internal static class CameraHiddenItemTests
{
    private static readonly FixedVector3 Focus = new(-19268 << 12, 4851 << 12, -40748 << 12);

    public static void Run(Action<bool, string> assert)
    {
        using JsonDocument document = JsonDocument.Parse(typeof(CameraHiddenItemTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.camera-hidden-items.json")!);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            Item[] items = test.GetProperty("items").EnumerateArray().Select((item, ordinal) =>
                new Item(ordinal + 1) { Flags = (ushort)item[0].GetInt32(), Model = (ushort)item[1].GetInt32(), Region = (byte)item[2].GetInt32() }).ToArray();
            int[] rotation = Values(test.GetProperty("rotation"));
            var quaternion = new FixedQuaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
            var camera = new CameraModeState
            {
                Mode = 16, CurrentRotation = quaternion, DesiredRotation = quaternion, SmoothedDistance = 400, ItemHidingEnabled = 1
            };
            var projection = new CameraNormalProjectionCache { Distance = 400 };
            var hidden = new CameraHiddenItems<Item>(item => new(item.Region, item.Model, item.Flags), (item, flags) => item.Flags = flags);
            JsonElement[] frames = test.GetProperty("frames").EnumerateArray().ToArray(), expected = test.GetProperty("expected").EnumerateArray().ToArray();
            for (int frame = 0; frame < frames.Length; frame++)
            {
                JsonElement input = frames[frame];
                if (input.TryGetProperty("changes", out JsonElement changes))
                    foreach (var (item, change) in items.Zip(changes.EnumerateArray()))
                    { item.Model = (ushort)change[0].GetInt32(); item.Region = (byte)change[1].GetInt32(); }
                Queue<int[]> hits = new(input.GetProperty("hits").EnumerateArray().Select(Values));
                int held = input.GetProperty("held").GetInt32(), overflows = 0;
                var events = new List<int>();
                CameraUserPlacement.Move(camera, projection, Focus, default, true, true, false, () => { }, () => hidden.Update(camera.Position, Focus,
                    () => camera.DisplayRotation, (start, end) =>
                    {
                        events.AddRange([start.X, start.Y, start.Z, end.X, end.Y, end.Z]);
                        int[] hit = hits.Count != 0 ? hits.Dequeue() : [0, 0, 0, 0, 0];
                        return hit[0] == 0 ? null : new(items[hit[0] - 1], new(hit[2], hit[3], hit[4]), hit[1]);
                    }, () => held == 0 ? null : items[held - 1], () => overflows++));
                int[] wanted = expected[frame].EnumerateArray().Where(value => value.ValueKind == JsonValueKind.Number).Select(value => value.GetInt32()).ToArray();
                int[] wantedEvents = Values(expected[frame][expected[frame].GetArrayLength() - 1]);
                int[] slots = Enumerable.Range(0, CameraHiddenItems<Item>.Capacity).Select(slot => hidden.GetItem(slot)?.Identity ?? 0).ToArray();
                assert(overflows == wanted[0] && slots.SequenceEqual(wanted[1..9]),
                    $"Original PC camera item case{index} frame{frame}: slots[{string.Join(',', slots)}] overflow{overflows} expected[{string.Join(',', wanted[0..9])}].");
                assert(items.Select(item => (int)item.Flags).SequenceEqual(wanted[25..35]),
                    $"Original PC camera item case{index} frame{frame}: flags[{string.Join(',', items.Select(item => item.Flags))}] expected[{string.Join(',', wanted[25..35])}].");
                assert(events.SequenceEqual(wantedEvents), $"Original PC camera item case{index} frame{frame}: ray order and chained start points match.");
            }
            index++;
        }
    }

    private sealed class Item(int identity)
    {
        public int Identity { get; } = identity;
        public ushort Flags { get; set; }
        public ushort Model { get; set; }
        public byte Region { get; set; }
    }
    private static int[] Values(JsonElement value) => value.EnumerateArray().Select(item => item.GetInt32()).ToArray();
}
