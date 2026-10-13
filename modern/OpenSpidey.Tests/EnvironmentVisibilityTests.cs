using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;

internal static class EnvironmentVisibilityTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(EnvironmentVisibilityTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.environment-visibility.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            string prefix = test.GetProperty("name").GetString()!;
            byte[] name = Encoding.Latin1.GetBytes(prefix);
            int words = (name.Length + 2) / 2;
            byte[] arguments = new byte[words * 2 + 6];
            name.CopyTo(arguments, 0);
            BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(words * 2), test.GetProperty("first").GetUInt16());
            BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(words * 2 + 2), test.GetProperty("last").GetUInt16());
            BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(words * 2 + 4), test.GetProperty("visible").GetUInt16());
            var host = new VisibilityHost(test.GetProperty("flags").GetUInt16(), test.GetProperty("mode").GetInt32());
            new EnvironmentVisibilityCommandHandler(host).Execute(new(new TriggerNode(0, 6, 0, ReadOnlyMemory<byte>.Empty), 0),
                new((ushort)MissionOpcode.SetEnvironmentVisibilityByName, 0, arguments));
            JsonElement[] expected = test.GetProperty("expected").EnumerateArray().ToArray();
            assert(host.Results.Count == expected.Length, "Named ENV ranges are inclusive and a reversed range performs no lookup, matching actual PC dispatch.");
            for (int index = 0; index < expected.Length; index++)
                assert(host.Results[index] == (expected[index][0].GetUInt32(), expected[index][1].GetInt32() != 0, expected[index][2].GetUInt16()),
                    "Original case-sensitive Latin1 prefix, exactly two truncated decimal suffix bytes, unsigned words, missing items and flag0-only mutation match actual PC command191.");
        }
        assert(SourceAssetChecksum.Compute(Encoding.Latin1.GetBytes("VaultDoor01")) == 0x2A023428 &&
            SourceAssetChecksum.Compute(Encoding.Latin1.GetBytes("VaultDoor")) == 0xC2582125,
            "The static VaultDoor01 checksum differs from the dynamic moving VaultDoor model checksum.");
        bool rejected = false;
        try { EnvironmentVisibilityPolicy.SetNamed(new VisibilityHost(0, 1), "1234567890123"u8, 1, 1, false); }
        catch (InvalidDataException) { rejected = true; }
        assert(rejected, "Modern named visibility rejects prefixes that would overrun the original sixteen-byte stack buffer.");
    }

    private sealed class VisibilityHost(ushort flags, int mode) : IEnvironmentVisibilityHost
    {
        public List<(uint Checksum, bool Found, ushort Flags)> Results { get; } = [];
        public void SetVisibility(uint checksum, bool visible)
        {
            bool found = mode == 1 || mode == 2 && Results.Count % 2 == 0;
            Results.Add((checksum, found, found ? EnvironmentVisibilityPolicy.SetVisible(flags, visible) : flags));
        }
    }
}
