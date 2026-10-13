using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerWebDripTests
{
    private sealed class Host(int[] input) : IPlayerWebDripHost
    {
        private int cursor = input[5];
        public List<int> Events { get; } = [];
        public List<LineParticleMotion> Motions { get; } = [];
        public uint Clock => 123;
        public FixedVector3 ForwardDirection => new(input[2], input[3], input[4]);
        public int Random(int bound)
        {
            int value = (cursor++ * 7 + 1) % bound;
            Events.AddRange([1, bound, value]);
            return value;
        }
        public FixedVector3 GetPlayerHookPosition(int hook)
        {
            Events.AddRange([2, hook]);
            return new(12345 + hook * 4097, -54321 - hook * 8193, 67890 + hook * 12289);
        }
        public void EmitLine(PlayerImpactLineParticle appearance, LineParticleMotion motion)
        {
            Events.AddRange([3, appearance.Position.X, appearance.Position.Y, appearance.Position.Z,
                appearance.Velocity.X, appearance.Velocity.Y, appearance.Velocity.Z, appearance.Lifetime,
                appearance.VerticalAcceleration >> 12, 4, (byte)appearance.StartRgb,
                (byte)(appearance.StartRgb >> 8), (byte)(appearance.StartRgb >> 16),
                5, (byte)appearance.EndRgb, (byte)(appearance.EndRgb >> 8), (byte)(appearance.EndRgb >> 16)]);
            if (appearance.CodeFlags != 0x02000000 || appearance.VerticalAcceleration != motion.VerticalAcceleration ||
                motion.Head != appearance.Position || motion.Tail != appearance.Position || motion.Velocity != appearance.Velocity)
                throw new InvalidDataException("Original web-drip appearance and source ballistic owner disagree.");
            Motions.Add(motion);
        }
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerWebDripTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-web-drips.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var host = new Host(input);
            PlayerWebDrips.Create(input[0] != 0, input[1] != 0, host, host.Random);
            assert(host.Events.SequenceEqual(test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt32())),
                $"Web-drip source case {index}: hook1 before0, four/two particles, three component RNG calls, signed wrapped velocity and96→0gradient.");
            assert(host.Motions.Count == (input[0] != 0 || input[1] != 0 ? 4 : 0) &&
                host.Motions.All(motion => motion.RemainingTicks == 30 && motion.LastClock == 123 && motion.VerticalAcceleration == 4096),
                $"Web-drip source case {index}: original30tick lifetime, source global creation clock and gravity1 translated toQ12.");
            index++;
        }
    }
}
