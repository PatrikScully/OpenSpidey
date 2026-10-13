using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;

internal static class LinkedEnvironmentVisibilityTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(LinkedEnvironmentVisibilityTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.linked-environment-visibility.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            ushort kind = test.GetProperty("kind").GetUInt16();
            int count = test.GetProperty("links").GetInt32(), alignment = test.GetProperty("alignment").GetInt32();
            byte[] contextData = new byte[12];
            ushort[] contextWords = [3, 4, 1, 2, 1, 3];
            for (int word = 0; word < contextWords.Length; word++)
                BinaryPrimitives.WriteUInt16LittleEndian(contextData.AsSpan(word * 2), contextWords[word]);
            var context = new TriggerNode(0, 3, 100, contextData);
            TriggerNode[] nodes = [context, Reference(1, 0x11111111), Reference(2, 0x22222222), Reference(3, 0x11111111)];
            var host = new Host(test.GetProperty("flags").GetUInt16(), test.GetProperty("mode").GetInt32());
            byte[] operand = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(operand, test.GetProperty("visible").GetUInt16());
            new LinkedEnvironmentVisibilityCommandHandler(new(nodes), host).Execute(new(context, 0), new(13, 200, operand));
            JsonElement expected = test.GetProperty("expected");
            assert(host.Calls.SequenceEqual(expected[0].EnumerateArray().Select(value => value.GetUInt32())),
                $"Actual PC SendVisible13 case{index}: original link order and repeated checksum identity with absolute source alignment.");
            var wanted = expected[1].EnumerateArray().Select(value => (Checksum: value[0].GetUInt32(), Flags: value[1].GetUInt16()));
            assert(host.Items.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value)).SequenceEqual(wanted),
                $"Actual PC SendVisible13 case{index++}: nonzero visibility, first matching item, missing lookups and preservation of every other flag.");

            TriggerNode Reference(ushort id, uint checksum)
            {
                int offset = 4 + count * 2;
                offset += (4 - ((alignment + offset) & 3)) & 3;
                byte[] bytes = new byte[offset + 4];
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, kind);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), (ushort)count);
                for (int link = 0; link < count; link++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4 + link * 2), 3);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), checksum);
                return new(id, kind, 1000 + alignment, bytes);
            }
        }
    }

    private sealed class Host(ushort flags, int mode) : IEnvironmentVisibilityHost
    {
        public List<uint> Calls { get; } = [];
        public Dictionary<uint, ushort> Items { get; } = [];
        public void SetVisibility(uint checksum, bool visible)
        {
            Calls.Add(checksum);
            if (mode == 0 || (mode == 2 && checksum == 0x22222222)) return;
            Items[checksum] = EnvironmentVisibilityPolicy.SetVisible(Items.GetValueOrDefault(checksum, flags), visible);
        }
    }
}
