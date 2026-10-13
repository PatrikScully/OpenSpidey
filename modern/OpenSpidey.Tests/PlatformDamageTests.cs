using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

internal static class PlatformDamageTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(PlatformDamageTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.platform-damage.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var variables = new ActorScriptVariables(new(), new VariableWorld())
            {
                Health = (short)input[0], ByteParameter = (byte)input[2], ItemFlags = (ushort)input[3]
            };
            using var bytes = new MemoryStream();
            using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write((ushort)1);
                writer.Write((short)input[1]);
            }
            variables.Write(0x2125, new(bytes.ToArray()));
            var host = new DamageHost(variables, input[5]);
            bool accepted = PlatformDamage.Hit(variables, new(0, 0, 0, (ushort)input[4], default), host);
            JsonElement expected = test.GetProperty("expected");
            int[] actual = [accepted ? 1 : 0, variables.ByteParameter, variables.Health, variables.ItemFlags];
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(4).Select(value => value.GetInt32())),
                "Platform hit acceptance, byte counter and signed-short damage match actual PC instructions.");
            assert(host.Events.SequenceEqual(expected[4].EnumerateArray().Select(value => value.GetInt32())),
                "Platform death, RNG, sound and shatter preserve source call order and visible flags.");
        }
    }

    private sealed class DamageHost(ActorScriptVariables variables, int random) : IPlatformDamageHost
    {
        public List<int> Events { get; } = [];
        public void Die()
        {
            Events.AddRange([1, 0, variables.Health, variables.ItemFlags]);
            variables.ItemFlags |= 1;
        }
        public int Random(int range) { Events.AddRange([2, range]); return random; }
        public void PlayBreakSound(int sound) => Events.AddRange([3, sound, 0]);
        public void Shatter(bool checkSeen, bool markProcessed) =>
            Events.AddRange([4, checkSeen ? 1 : 0, markProcessed ? 1 : 0, variables.ItemFlags]);
    }

    private sealed class VariableWorld : IActorScriptVariableWorld
    {
        public int Difficulty => throw new NotSupportedException();
        public int MovieInputPolls => throw new NotSupportedException();
        public FixedVector3? PlayerPosition => throw new NotSupportedException();
        public bool HasLineOfSight(FixedVector3 from, FixedVector3 to) => throw new NotSupportedException();
        public int Random(int range) => throw new NotSupportedException();
        public ushort FirstLink(ushort nodeId) => throw new NotSupportedException();
        public void SelectModel(uint checksum) => throw new NotSupportedException();
    }
}
