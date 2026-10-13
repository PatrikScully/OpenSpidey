using System.Text.Json;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

internal static class EnvironmentImpactTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(EnvironmentImpactTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.environment-impact.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            int[] expected = test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            if (test.GetProperty("mode").GetInt32() == 0)
            {
                EnvironmentImpactAction result = EnvironmentImpactPolicy.Evaluate(input[0] != 0,
                    (ushort)input[1], input[2] != 0, (ushort)input[3], input[4] != 0, input[5]);
                assert((int)result == (expected.Length == 0 ? 0 : expected[0]),
                    "ENV membership, hidden flag, model bit2, optional face and exact65535 crate damage match actual PC Exp_HitEnvItem.");
                continue;
            }
            var destruction = new EnvironmentCrateDestruction(input[0]);
            var host = new CrateHost(input[1] != 0, input[2] != 0, input[3]);
            bool destroyed = destruction.Destroy(new Crate(), host);
            assert(host.Events.SequenceEqual(expected),
                "Crate destruction preserves actual PC shatter, environment lookup, command checksum, low-memory and small/big explosion call order.");
            assert(destruction.Recursion == input[0] && destroyed == expected.Contains(3),
                "PC recursion count unwinds after both successful and failed shatter, including signed wrap and recursion guard.");
        }
    }

    private sealed class Crate : IEnvironmentCrate
    {
        public FixedVector3 Position => new(100, 200, 300);
        public uint ModelChecksum => 0x12345678;
    }
    private sealed class CrateHost(bool shatter, bool lowMemory, int random) : IEnvironmentCrateHost
    {
        public List<int> Events { get; } = [];
        public bool LowMemory => lowMemory;
        public bool Shatter(IEnvironmentCrate crate, bool checkSeen, bool markProcessed)
        {
            Events.AddRange([1, checkSeen ? 1 : 0, markProcessed ? 1 : 0]);
            return shatter;
        }
        public void TriggerCommandPoint(uint checksum)
        {
            // Native host performs Spool_GetEnvIndex(3) and then the actual checksum lookup.
            Events.AddRange([2, 3, 3, unchecked((int)checksum), 0]);
        }
        public int Random(int range) { Events.AddRange([4, range]); return random; }
        public void CreateCrateExplosion(FixedVector3 position, bool small) =>
            Events.AddRange([small ? 5 : 6, position.X, position.Y, position.Z]);
    }
}
