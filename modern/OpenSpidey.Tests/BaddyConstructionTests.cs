using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Hostages;
using OpenSpidey.Core.Actors.Thugs;

internal static class BaddyConstructionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(BaddyConstructionTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.baddy-construction.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var count = new BaddyConstructionCount(test.GetProperty("initial").GetInt32());
            var leases = new List<BaddyConstructionLease>();
            JsonElement expected = test.GetProperty("expected");
            int index = 0;
            foreach (JsonElement operation in test.GetProperty("operations").EnumerateArray())
            {
                BaddyConstructionLease lease;
                int command = operation.GetInt32();
                if (command == 0)
                {
                    lease = count.BeginConstruction();
                    leases.Add(lease);
                    var thug = new ThugState(0, 0x101, sourceConstructionOrdinal: lease.Ordinal);
                    var hostage = new HostageState(0, lease.Ordinal);
                    assert(thug.ActorPhase == lease.Phase && hostage.ActorPhase == lease.Phase &&
                        thug.SourceConstructionOrdinal == lease.Ordinal && hostage.SourceConstructionOrdinal == lease.Ordinal,
                        "Both real CBaddy-derived NPC states receive the constructor's captured phase before their derived initialization.");
                }
                else
                {
                    lease = leases[command - 1];
                    lease.Dispose();
                }
                JsonElement result = expected[index++];
                assert(count.Count == result[0].GetInt32() && lease.Ordinal == result[1].GetInt32() &&
                    lease.Phase == result[2].GetInt32(),
                    "Construction ordinal, byte phase and signed count match actual PC arithmetic, including wrap and out-of-order deletion.");
                if (command != 0)
                {
                    int releasedCount = count.Count;
                    lease.Dispose();
                    assert(lease.Released && count.Count == releasedCount,
                        "Logical source deletion and the later native destructor release one construction count exactly once.");
                }
            }
            foreach (BaddyConstructionLease lease in leases) lease.Dispose();
            assert(count.Count == test.GetProperty("initial").GetInt32(),
                "Releasing all actual base-constructor leases restores the original signed count, including overflow cases.");
        }
        var shared = new BaddyConstructionCount();
        using BaddyConstructionLease outer = shared.BeginConstruction();
        using (BaddyConstructionLease nested = shared.BeginConstruction())
            assert(outer.Ordinal == 0 && nested.Ordinal == 1 && shared.Count == 2,
                "Nested actual constructors share the live count rather than independent or monotonic identity counters.");
        using BaddyConstructionLease replacement = shared.BeginConstruction();
        assert(replacement.Ordinal == 1 && replacement.Phase == 1,
            "After source destruction, the next constructor reuses the live-count ordinal; it is not an actor identity.");
    }
}
