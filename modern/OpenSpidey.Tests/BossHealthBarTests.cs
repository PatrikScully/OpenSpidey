using System.Text.Json;
using OpenSpidey.Core.Presentation;

internal static class BossHealthBarTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(BossHealthBarTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.boss-health-bars.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        Dictionary<string, TextureIdentity> textures = document.RootElement.GetProperty("textures").EnumerateArray()
            .Select((name, index) => (Name: name.GetString()!, Texture: new TextureIdentity(index + 1)))
            .ToDictionary(value => value.Name, value => value.Texture, StringComparer.Ordinal);
        int sequence = 0;
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            Body[] bodies = Enumerable.Range(1, 4).Select(id => new Body(id)).ToArray();
            var bars = new BossHealthBars<Body, TextureIdentity>(body => body.Health);
            int step = 0;
            foreach (JsonElement input in test.GetProperty("input").EnumerateArray())
            {
                var events = new List<long[]>();
                if (input[0].GetInt32() == 0)
                {
                    Body body = bodies[input[1].GetInt32()];
                    body.Health = input[3].GetInt16();
                    bars.Create(body, input[2].GetInt32(), name =>
                    {
                        TextureIdentity texture = textures[name];
                        events.Add(new long[] { texture.Id }.Concat(Snapshot(bars)).ToArray());
                        return texture;
                    });
                }
                else bars.Destroy();
                JsonElement expected = test.GetProperty("expected")[step];
                assert(Snapshot(bars).SequenceEqual(Numbers(expected.GetProperty("state"))),
                    $"Whole original boss health-bar sequence{sequence}, step{step} preserves both body identities, signed initial health, primary type and retained texture slots.");
                assert(events.Count == expected.GetProperty("events").GetArrayLength() &&
                    events.Zip(expected.GetProperty("events").EnumerateArray()).All(pair => pair.First.SequenceEqual(Numbers(pair.Second))),
                    $"Whole original boss health-bar sequence{sequence}, step{step++} publishes canonical body/health state before each ordered texture lookup.");
            }
            sequence++;
        }
    }

    private static long[] Snapshot(BossHealthBars<Body, TextureIdentity> bars) =>
        new long[] { bars.Primary?.Id ?? 0, bars.Secondary?.Id ?? 0, bars.PrimaryType, bars.PrimaryInitialHealth, bars.SecondaryInitialHealth }
            .Concat(bars.Textures.Select(texture => (long)(texture?.Id ?? 0))).ToArray();
    private static IEnumerable<long> Numbers(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64());
    private sealed class Body(int id)
    {
        public int Id { get; } = id;
        public short Health { get; set; }
    }
    private sealed record TextureIdentity(int Id);
}
