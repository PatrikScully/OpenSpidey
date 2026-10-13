using System.Text.Json;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

internal static class ThugFrameSoundTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugFrameSoundTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-frame-sounds.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            int[] expected = test.GetProperty("events").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var host = new Host();
            for (int tick = 0; tick < input[4]; tick++)
                ThugFrameSounds.Update(input[0], (ushort)input[1], (ushort)input[2], new(-123456, 987654, -56789), host,
                    bound => { host.Events.AddRange([1, bound]); return input[3]; });
            assert(host.Events.SequenceEqual(expected), $"Thug frame sound case {index++}: input [{string.Join(',', input)}], actual [{string.Join(',', host.Events)}], expected [{string.Join(',', expected)}]");
        }
    }

    private sealed class Host : IThugFrameSoundHost
    {
        public List<int> Events { get; } = [];
        public void PlayFrameSound(uint sound, FixedVector3 position, int pitchOffset) =>
            Events.AddRange([2, (int)sound, position.X, position.Y, position.Z, pitchOffset]);
        public void MakeSpriteRing(FixedVector3 localOffset) => Events.AddRange([3, localOffset.X, localOffset.Y, localOffset.Z]);
    }
}
